using Jellyfin.Plugin.RssFeed.Api;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RssFeed
{
    // This class is how Jellyfin 10.9+ plugins register their services into the DI container.
    // Without this, RssFeedService never starts and RssController can't be injected.
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServiceProvider applicationServiceProvider)
        {
            // Register as singleton so the in-memory cache is shared between
            // the background listener and the API controller
            serviceCollection.AddSingleton<RssFeedService>();

            // Tell ASP.NET Core's hosted service infrastructure to start/stop it
            serviceCollection.AddHostedService(sp => sp.GetRequiredService<RssFeedService>());
        }
    }
}
