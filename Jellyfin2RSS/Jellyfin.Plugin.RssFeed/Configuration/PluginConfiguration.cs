using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RssFeed.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public List<Guid> EnabledLibraryIds { get; set; }
        public string SecurityToken { get; set; }
        public int RetentionHours { get; set; }

        public PluginConfiguration()
        {
            EnabledLibraryIds = new List<Guid>();
            // This auto-generates the token the very first time the plugin runs
            SecurityToken = Guid.NewGuid().ToString("N"); 
            RetentionHours = 48;
        }
    }
}
