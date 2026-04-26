using Jellyfin.Plugin.RssFeed.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RssFeed
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register as singleton so the in-memory cache is shared between
            // the background listener and the API controller
            serviceCollection.AddSingleton<RssFeedService>();

            // Tell ASP.NET Core's hosted service infrastructure to start/stop it
            serviceCollection.AddHostedService(sp => sp.GetRequiredService<RssFeedService>());
        }
    }
}
