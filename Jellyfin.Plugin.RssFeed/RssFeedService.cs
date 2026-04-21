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
        // The top-level library ID this item belongs to — used for per-library feed filtering
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

        // IHostedService.StartAsync — called when Jellyfin starts
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded += OnItemAdded;
            _logger.LogInformation("RSS Feed plugin started, listening for new items.");
            return Task.CompletedTask;
        }

        // IHostedService.StopAsync — called when Jellyfin shuts down
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded -= OnItemAdded;
            return Task.CompletedTask;
        }

        private void OnItemAdded(object? sender, ItemChangeEventArgs e)
        {
            var item = e.Item;
            var config = Plugin.Instance!.Configuration;

            // Only handle concrete media types — ignore folders, seasons, series, etc.
            if (item is not (Movie or Episode or Audio))
            {
                return;
            }

            var libraryId = GetTopParentId(item);
            if (libraryId is null || !config.EnabledLibraryIds.Contains(libraryId))
            {
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
        // GetTopParent() is the reliable Jellyfin API for this — no manual parent walking needed.
        private static string? GetTopParentId(BaseItem item)
        {
            var top = item.GetTopParent();
            return top?.Id.ToString();
        }

        private static string GetPrettyTitle(BaseItem item)
        {
            if (item is Episode ep)
            {
                return $"{ep.SeriesName} - S{ep.ParentIndexNumber:00}E{ep.IndexNumber:00} - {ep.Name}";
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
