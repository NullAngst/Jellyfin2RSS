# Jellyfin RSS Feed Syndicator

A Jellyfin server plugin that generates a rolling RSS feed of recently added media. Subscribe to all your libraries at once, or get a separate feed URL per library — useful for notifying a Discord server, an RSS reader, or any other automation that can consume RSS.

---

## How it works

The plugin listens for new items being added to your Jellyfin library in real time. When something lands in an enabled library, it gets prepended to an in-memory feed. Items older than your configured retention window (default: 48 hours) are automatically pruned. The feed is served as a standard RSS 2.0 XML file over HTTP, protected by a secret token.

---

## Requirements

- Jellyfin **10.9.0 or later** (tested against 10.11.x)
- .NET 8 SDK (to build from source)

---

## Building from source

```bash
git clone https://github.com/NullAngst/Jellyfin2RSS
cd Jellyfin2RSS

dotnet publish Jellyfin.Plugin.RssFeed/Jellyfin.Plugin.RssFeed.csproj \
  --configuration Release \
  --output ./publish
```

This produces `Jellyfin.Plugin.RssFeed.dll` (and any dependencies) in `./publish`.

---

## Installation

1. On your Jellyfin server, locate the plugins directory. The default paths are:
   - **Linux:** `/var/lib/jellyfin/plugins/`
   - **Docker (linuxserver image):** `/config/plugins/`
   - **Windows:** `%APPDATA%\Jellyfin\plugins\`

2. Create a folder for the plugin inside the plugins directory:
   ```
   plugins/
   └── RssFeedSyndicator/
       └── Jellyfin.Plugin.RssFeed.dll
   ```

3. Copy `Jellyfin.Plugin.RssFeed.dll` from `./publish` into that folder.

4. Restart Jellyfin.

---

## Configuration

1. Open the Jellyfin Dashboard → **Plugins** → **RSS Feed Syndicator** → **Settings**.

2. Check the libraries you want to include in the feed.

3. Click **Save**.

The settings page will display:

- A **combined feed URL** covering all enabled libraries
- An individual **per-library feed URL** for each enabled library

Your security token is auto-generated on first run and shown on the settings page. Keep it secret — it's the only thing protecting access to the feed.

---

## Feed URLs

### All enabled libraries
```
https://your-jellyfin-server/RSS/Feed.xml?token=YOUR_TOKEN
```

### Single library
```
https://your-jellyfin-server/RSS/Feed.xml?token=YOUR_TOKEN&libraryId=LIBRARY_ITEM_ID
```

The `libraryId` values are shown on the settings page next to each library's URL. You can find them manually in the Jellyfin API at `/Library/VirtualFolders` if needed.

---

## Feed format

The feed is standard RSS 2.0. Each item contains:

| Field | Content |
|---|---|
| `title` | Movie: `Name (Year)` · Episode: `Series - S01E01 - Title` |
| `description` | The item's overview/synopsis |
| `link` | Direct link to the item in your Jellyfin web UI |
| `guid` | The Jellyfin item ID |
| `pubDate` | UTC time the item was added |

---

## Settings reference

| Setting | Default | Description |
|---|---|---|
| Enabled Libraries | *(none)* | Libraries whose new items will appear in the feed |
| Security Token | *(auto-generated)* | Token required in the feed URL query string |
| Retention Hours | `48` | Items older than this are removed from the feed |

Retention hours can be changed by editing the plugin's XML config file directly (located in the Jellyfin config directory under `plugins/configurations/Jellyfin.Plugin.RssFeed.xml`) until a UI field is added for it.

---

## Notes

- The feed is **in-memory only** — it does not persist across Jellyfin restarts. If Jellyfin restarts, the feed starts fresh and fills as new items arrive.
- The plugin only captures items added **after** the plugin starts. It does not backfill from your existing library.
- Only `Movie`, `Episode`, and `Audio` item types are included. Folders, seasons, series, and other container types are ignored.
