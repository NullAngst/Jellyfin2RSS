using Jellyfin.Plugin.RssFeed.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RssFeed
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "RSS Feed Syndicator";

        public override Guid Id => Guid.Parse("b6a52192-83d3-4938-afbd-f20ce331e1a1");

        public static Plugin? Instance { get; private set; }

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "rssfeed",
                    // Must match: <RootNamespace>.<folder>.<filename>
                    EmbeddedResourcePath = $"{GetType().Namespace}.Web.config.html"
                }
            };
        }
    }
}
