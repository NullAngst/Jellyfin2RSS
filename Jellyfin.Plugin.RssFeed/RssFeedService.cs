using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RssFeed
{
    public class RssItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LinkId { get; set; } = string.Empty;
        public DateTime PubDate { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        // The top-level library ID this item belongs to - used for per-library feed filtering
        public string LibraryId { get; set; } = string.Empty;
    }

    // IHostedService is the correct interface in Jellyfin 10.9+.
    // IServerEntryPoint was removed. Register this via PluginServiceRegistrator.
    public class RssFeedService : IHostedService, IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<RssFeedService> _logger;

        private static readonly List<RssItem> _feedCache = new();
        private static readonly object _lock = new();

        public RssFeedService(ILibraryManager libraryManager, ILogger<RssFeedService> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        // IHostedService.StartAsync - called when Jellyfin starts
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded += OnItemAdded;
            _logger.LogInformation("RSS Feed plugin started, listening for new items.");
            return Task.CompletedTask;
        }

        // IHostedService.StopAsync - called when Jellyfin shuts down
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded -= OnItemAdded;
            return Task.CompletedTask;
        }

        private void OnItemAdded(object? sender, ItemChangeEventArgs e)
        {
            var item = e.Item;
            var config = Plugin.Instance!.Configuration;

            // Log everything that comes through so you can see what Jellyfin is firing
            _logger.LogDebug("RSS Feed: ItemAdded fired for '{Name}', type={Type}", item.Name, item.GetType().Name);

            // Accept movies, episodes, music albums, and individual audio tracks.
            // MusicAlbum is the meaningful unit for music - one RSS entry per album, not per track.
            // Individual Audio (tracks) are also accepted in case the album event doesn't fire.
            if (item is not (Movie or Episode or MusicAlbum or Audio))
            {
                _logger.LogDebug("RSS Feed: skipping '{Name}' - type {Type} not handled.", item.Name, item.GetType().Name);
                return;
            }

            // For individual audio tracks, skip them if the parent album was already added.
            // This prevents one entry per track when an album is scanned.
            if (item is Audio track)
            {
                var albumId = track.ParentId.ToString("N");
                lock (_lock)
                {
                    if (_feedCache.Any(x => x.LinkId == albumId))
                    {
                        _logger.LogDebug("RSS Feed: skipping track '{Name}' - parent album already in feed.", track.Name);
                        return;
                    }
                }
            }

            var libraryId = GetTopParentId(item);
            _logger.LogDebug("RSS Feed: '{Name}' resolved top parent library ID = '{LibraryId}'", item.Name, libraryId ?? "(null)");

            if (libraryId is null || !config.EnabledLibraryIds.Contains(libraryId))
            {
                _logger.LogDebug("RSS Feed: skipping '{Name}' - library '{LibraryId}' not enabled. Enabled: [{Enabled}]",
                    item.Name, libraryId ?? "(null)", string.Join(", ", config.EnabledLibraryIds));
                return;
            }

            var rssItem = new RssItem
            {
                Title = GetPrettyTitle(item),
                Description = item.Overview ?? "No description available.",
                LinkId = item.Id.ToString(),
                PubDate = DateTime.UtcNow,
                LibraryId = libraryId,
                ImageUrl = item.HasImage(MediaBrowser.Model.Entities.ImageType.Primary)
                    ? $"/Items/{item.Id}/Images/Primary"
                    : string.Empty
            };

            lock (_lock)
            {
                _feedCache.Insert(0, rssItem);
                PruneCache(config.RetentionHours);
            }

            _logger.LogInformation("RSS Feed: added '{Title}'", rssItem.Title);
        }

        // Walk up the tree to find the top-level virtual folder (the library root).
        private static string? GetTopParentId(BaseItem item)
        {
            var top = item.GetTopParent();
            // Use "N" format (no dashes) to match the IDs the Jellyfin web API returns
            // via getVirtualFolders(), which is what gets stored in EnabledLibraryIds.
            // Default ToString() produces "D" format (with dashes), causing a silent mismatch.
            return top?.Id.ToString("N");
        }

        private static string GetPrettyTitle(BaseItem item)
        {
            if (item is Episode ep)
            {
                return $"{ep.SeriesName} - S{ep.ParentIndexNumber:00}E{ep.IndexNumber:00} - {ep.Name}";
            }

            if (item is MusicAlbum album)
            {
                var artist = album.AlbumArtist;
                if (string.IsNullOrEmpty(artist) && album.AlbumArtists?.Count > 0)
                {
                    artist = album.AlbumArtists[0];
                }

                return string.IsNullOrEmpty(artist)
                    ? $"{album.Name} ({album.ProductionYear})"
                    : $"{artist} - {album.Name} ({album.ProductionYear})";
            }

            return $"{item.Name} ({item.ProductionYear})";
        }

        private static void PruneCache(int hours)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours);
            _feedCache.RemoveAll(x => x.PubDate < cutoff);
        }

        // Returns all items, or filtered to a specific library if libraryId is provided
        public List<RssItem> GetFeed(string? libraryId = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(libraryId))
                {
                    return new List<RssItem>(_feedCache);
                }

                return _feedCache.Where(x => x.LibraryId == libraryId).ToList();
            }
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnItemAdded;
            GC.SuppressFinalize(this);
        }
    }
}
