using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.RssFeed.Api
{
    [ApiController]
    [Route("RSS")] // This sets the URL to http://jellyfin/RSS/...
    public class RssController : ControllerBase
    {
        private readonly RssFeedService _service;

        public RssController(RssFeedService service)
        {
            _service = service;
        }

        [HttpGet("Feed.xml")]
        [AllowAnonymous] // This allows your RSS reader to see the page without logging in
        [Produces("application/rss+xml")]
        public IActionResult GetFeed([FromQuery] string token)
        {
            var config = Plugin.Instance.Configuration;
            
            // Security Check
            if (string.IsNullOrEmpty(token) || token != config.SecurityToken)
            {
                return Unauthorized("Invalid RSS Token");
            }

            var items = _service.GetFeed();
            var host = $"{Request.Scheme}://{Request.Host}";

            var rss = new XElement("rss", 
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", "Jellyfin New Media"),
                    new XElement("description", "Recently added media from Jellyfin"),
                    new XElement("link", host),
                    new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),
                    
                    from item in items
                    select new XElement("item",
                        new XElement("title", item.Title),
                        new XElement("description", $"<![CDATA[{item.Description}]]>"),
                        new XElement("link", $"{host}/web/index.html#!/details?id={item.LinkId}"),
                        new XElement("guid", item.LinkId),
                        new XElement("pubDate", item.PubDate.ToString("R"))
                    )
                )
            );

            return Content(rss.ToString(), "application/rss+xml", Encoding.UTF8);
        }
    }
}
