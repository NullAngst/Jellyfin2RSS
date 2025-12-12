using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Audio;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RssFeed
{
    public class RssItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string LinkId { get; set; }
        public DateTime PubDate { get; set; }
        public string ImageUrl { get; set; }
    }

    public class RssFeedService : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<RssFeedService> _logger;
        
        // This is where we store the RSS items in RAM
        private static readonly List<RssItem> _feedCache = new();
        private static readonly object _lock = new();

        public RssFeedService(ILibraryManager libraryManager, ILogger<RssFeedService> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        // Called when Jellyfin starts
        public Task RunAsync()
        {
            _libraryManager.ItemAdded += OnItemAdded;
            return Task.CompletedTask;
        }

        // Called whenever ANY new file is added to Jellyfin
        private void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            var item = e.Item;
            var config = Plugin.Instance.Configuration;

            if (!IsLibraryEnabled(item, config)) return;
            if (!(item is Movie || item is Episode || item is Audio)) return;

            var rssItem = new RssItem
            {
                Title = GetPrettyTitle(item),
                Description = item.Overview ?? "No description available.",
                LinkId = item.Id.ToString(),
                PubDate = DateTime.UtcNow,
                ImageUrl = item.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) 
                           ? $"/Items/{item.Id}/Images/Primary" 
                           : ""
            };

            lock (_lock)
            {
                _feedCache.Insert(0, rssItem); // Add to top of list
                PruneCache(config.RetentionHours);
            }
        }

        private bool IsLibraryEnabled(BaseItem item, Configuration.PluginConfiguration config)
        {
            // Check if the item belongs to a library we checked in the settings
            var parent = item;
            while (parent.ParentId != Guid.Empty && !(parent is AggregateFolder))
            {
                if (config.EnabledLibraryIds.Contains(parent.Id)) return true;
                if (parent.Parent == null) break;
                parent = parent.Parent;
            }
            return false;
        }

        private string GetPrettyTitle(BaseItem item)
        {
            if (item is Episode ep)
            {
                return $"{ep.SeriesName} - S{ep.ParentIndexNumber:00}E{ep.IndexNumber:00} - {ep.Name}";
            }
            return $"{item.Name} ({item.ProductionYear})";
        }

        private void PruneCache(int hours)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours);
            _feedCache.RemoveAll(x => x.PubDate < cutoff);
        }

        public List<RssItem> GetFeed()
        {
            lock (_lock)
            {
                return new List<RssItem>(_feedCache);
            }
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnItemAdded;
        }
    }
}
