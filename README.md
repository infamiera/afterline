# Afterline

Afterline is a Windows companion app for FiveM roleplay. It captures the chat already shown by the game, keeps it organised as readable text files, and gives you a practical set of tools for reviewing logs and creating roleplay screenshots.

Everything is designed to stay local. Afterline does not upload your chatlogs, screenshots, projects, or settings.

[Download the latest Stable release](https://github.com/infamiera/afterline/releases/latest) · [Try the Canary build](https://github.com/infamiera/afterline/releases/tag/canary) · [Report an issue](https://github.com/infamiera/afterline/issues)

## What Afterline does

### Chat capture and archives

- Captures chat while FiveM is running and connected to a server.
- Keeps the timestamp supplied by the server whenever one is available.
- Saves one plain-text chatlog per server and calendar day.
- Adds clear login, disconnect, day-end, and date-rollover markers.
- Continues the same daily file when you reconnect to the same server.
- Organises logs into year and month folders automatically.
- Maintains crash-safe recovery data without cluttering the chatlog folder.
- Flags suspicious duplicate ranges for review instead of deleting lines automatically.

Chatlogs remain ordinary `.txt` files, so they can always be opened without Afterline.

```text
Afterline Chatlogs
└── 2026
    └── 09 - September
        ├── Chatlog [Server A] [03-September-2026].txt
        └── Chatlog [Server B] [03-September-2026].txt
```

### Live Chat and Log Reader

- Shows captured chat inside Afterline with the exact rendered server colours when available.
- Includes search, timestamps, bookmarks, notes, and quick context actions.
- Lets you hide IC or OOC/system messages without changing the saved chatlog.
- Opens archived files in a dedicated Log Reader with filtering and line numbers.
- Exports either plain TXT or a self-contained coloured HTML file.
- Keeps login and disconnect markers visible when chat filters are used.

Display filters affect what you see and what a *visible* export contains. They never rewrite the original archive.

### Screenshot Editor

- Opens static images and animated GIFs.
- Supports multiple image layers, drag-and-drop from Explorer, resizing, repositioning, locking, reordering, opacity, and rounded corners.
- Allows layers to extend beyond the Base Image while keeping the Base Image as the export boundary.
- Includes chat overlays, captured chat colours, text styling, rulers, snapping, image adjustments, paint, erasing, selections, and background removal.
- Provides reusable collage layouts with live gap controls and independently repositioned frame crops.
- Supports transparent projects and transparent PNG export.
- Saves editable `.afterlineproj` projects with autosave and recent-project recovery.
- Includes Undo and Redo for document and layer changes.

### Screen capture and Gallery

- Captures a verified FiveM/GTA window at its source resolution.
- Supports a configurable global hotkey, optional sound, and optional Windows notification.
- Shows only the latest 20 thumbnails in the Gallery to keep the app responsive.
- Opens a capture directly in the Editor.
- Stores captures locally in the default Documents folder or a path you choose.
- Can be disabled completely, which hides the Gallery and unregisters capture resources.

Afterline does not fall back to capturing the desktop or an unrelated application.

## Stable and Canary

| Channel | Best for | What to expect |
| --- | --- | --- |
| **Stable** | Normal daily use | Tested releases with fewer updates. |
| **Canary** | Trying new features and fixes early | Frequent experimental builds that may still need testing. |

You can switch update channels from Afterline's settings. Canary builds show both Stable and Canary changes; Stable builds show Stable changes only.

## Installation

1. Download `Afterline.exe` from the [official Releases page](https://github.com/infamiera/afterline/releases).
2. Place it in a folder where it can remain between updates.
3. Run it and complete the short first-time setup.
4. Start FiveM. Afterline will remain idle until it finds a supported session.

Afterline is published as a self-contained Windows x64 executable. You do not need to install the .NET runtime separately.

Depending on your Windows security settings, SmartScreen or antivirus software may inspect a newly downloaded build. Only download Afterline from this repository and use the included SHA-256 checksum if you want to verify the executable.

## Where files are stored

- **Chatlogs:** the archive folder selected in Afterline.
- **Screenshots:** `Documents\Afterline\Screenshots` by default, or a custom folder.
- **Editor projects:** `Documents\Afterline Projects` by default, or a custom folder.
- **Manual exports:** the Windows Downloads folder.
- **Settings, cache, recovery, and diagnostics:** `%LocalAppData%\Afterline`.

The private cache may include temporary metadata needed to restore Live Chat exactly after a restart. It is kept out of user-facing archive and export folders.

## Recovery and performance

Afterline keeps capture and journal writing separate from heavier interface work. The application shell opens first, recent dashboard data comes from a bounded index, and full archive rebuilding only runs when requested.

Current release checks include packaged startup, Editor project round-trips, interrupted-session recovery, chat-colour replay, transactional updating, and a 10,000-chatlog archive stress test with simulated slow storage.

If Afterline is force-closed or stops responding, the Error Logs window can recover diagnostics from the previous run. Raw capture and session backups are also kept locally so an interrupted session can be restored.

## Updates

Afterline checks this repository for releases. An update is downloaded only after you approve it, verified against its SHA-256 checksum, staged, and then installed. The previous executable is retained until the replacement has been confirmed healthy.

Stable and Canary use separate release channels. Publishing a new Canary build does not replace the current download until its Windows build and validation workflow has passed.

## FiveM interaction

Afterline is a passive, read-only companion. It reads chat already rendered by the local FiveM interface and uses normal connection information when resolving a server name.

It does **not** inject into the game, modify memory, send gameplay events, automate actions, install a FiveM resource, or bypass anti-cheat systems.

Server rules differ. You are responsible for checking whether external chat logging or screenshot tools are allowed on the server you play.

## Support and development

- Use [GitHub Issues](https://github.com/infamiera/afterline/issues) for reproducible bugs and feature requests.
- For diagnostic help, join the [Afterline Discord](https://discord.gg/At2znTygfV) and post exported Error Logs in the `#afterline` forum channel.

To build Afterline yourself, use Windows with the .NET 8 SDK:

```powershell
dotnet publish src/Afterline/Afterline.csproj -c Release -r win-x64 --self-contained true
```

## Independence

Afterline is an independent third-party project. It is not affiliated with, endorsed by, sponsored by, or approved by Rockstar Games, Cfx.re, FiveM, or any roleplay server or community. Their names are used only to explain compatibility.
