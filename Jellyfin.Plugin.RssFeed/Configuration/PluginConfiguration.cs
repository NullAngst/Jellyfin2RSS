using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RssFeed.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Stored as strings to match the IDs the Jellyfin web API returns (and JS sends back).
        // Using Guid caused silent mismatches because JS gives us plain strings.
        public List<string> EnabledLibraryIds { get; set; }

        public string SecurityToken { get; set; }

        public int RetentionHours { get; set; }

        public PluginConfiguration()
        {
            EnabledLibraryIds = new List<string>();
            SecurityToken = Guid.NewGuid().ToString("N");
            RetentionHours = 48;
        }
    }
}
