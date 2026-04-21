using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RssFeed.Api
{
    [ApiController]
    [Route("RSS")]
    public class RssController : ControllerBase
    {
        private readonly RssFeedService _service;

        // RssFeedService is injected because PluginServiceRegistrator registered it as a singleton
        public RssController(RssFeedService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get the RSS feed.
        /// </summary>
        /// <param name="token">Your secret RSS token from the plugin settings page.</param>
        /// <param name="libraryId">Optional. Filter to a specific library by its item ID.</param>
        /// <returns>An RSS 2.0 XML feed.</returns>
        [HttpGet("Feed.xml")]
        [AllowAnonymous]
        [Produces("application/rss+xml")]
        public IActionResult GetFeed([FromQuery] string token, [FromQuery] string? libraryId = null)
        {
            var config = Plugin.Instance!.Configuration;

            if (string.IsNullOrEmpty(token) || token != config.SecurityToken)
            {
                return Unauthorized("Invalid RSS token.");
            }

            var items = _service.GetFeed(libraryId);
            var host = $"{Request.Scheme}://{Request.Host}";

            var feedTitle = string.IsNullOrEmpty(libraryId)
                ? "Jellyfin — All New Media"
                : $"Jellyfin — Library {libraryId}";

            var rss = new XElement("rss",
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", feedTitle),
                    new XElement("description", "Recently added media from Jellyfin"),
                    new XElement("link", host),
                    new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),

                    from item in items
                    select new XElement("item",
                        new XElement("title", item.Title),
                        // XCData is required — writing "<![CDATA[...]]>" as a plain string
                        // causes XLinq to XML-escape it into literal text. XCData renders it correctly.
                        new XElement("description", new XCData(item.Description)),
                        new XElement("link", $"{host}/web/index.html#!/details?id={item.LinkId}"),
                        new XElement("guid", new XAttribute("isPermaLink", "false"), item.LinkId),
                        new XElement("pubDate", item.PubDate.ToString("R"))
                    )
                )
            );

            return Content(rss.ToString(), "application/rss+xml", Encoding.UTF8);
        }
    }
}
