# Afterline

Afterline is an independent Windows utility for capturing, autosaving, archiving, searching and presenting text roleplay chat.

## Features

- Automatic chat capture while FiveM is active and connected to a server.
- Best-effort server detection, including a friendly server name when FiveM exposes enough connection metadata to resolve one.
- Server-aware chatlog boundaries: leaving or switching servers immediately finalizes the current server log.
- One readable chatlog per server per calendar day, stored as `Chatlog [Server Name] [21-August-2026].txt`.
- Automatic daily rollover after midnight without interrupting the active server session.
- Rejoining the same server later on the same day continues that server/day file with a `[NEW LOGIN]` divider.
- `[DISCONNECTED]`, `[NEW LOGIN]`, day-end and date-rollover markers keep archive boundaries readable.
- Continuous crash-safe session autosave with a second local recovery copy and pre-parse raw capture failsafe.
- Automatic recovery of unfinished sessions after an unexpected shutdown.
- Graceful shutdown finalization when the application is exited normally.
- Custom chatlog storage directory with year and month organization.
- Optional Live Chat display inside Afterline with timestamps, automatic RP colors, search and context tools.
- Manual `Parse current chat` action for messages still retained by FiveM's current chat UI.
- One-click export of the current captured server session to the user's Downloads folder.
- Archive browsing, pinned/recent logs and multi-term text search.
- Plain-text chatlogs that remain usable outside Afterline.
- Optional launch at Windows sign-in while capture remains idle until FiveM is running.
- RP Screenshot Editor with chat overlays, per-line color overrides, text effects, image tone controls and markup.
- Static PNG/JPEG/BMP and animated GIF input in the Editor.
- Animated GIF preview and export with the same chat, image adjustments, crop and markup applied to every frame.
- Non-destructive crop framing with exact output dimensions and common presets such as 1920×1080, 1280×720, 1080×1080 and 1080×1350.
- Reusable Editor settings for chat styling, image tone and output sizing.
- In-app update checking through GitHub Releases with explicit `Update now` confirmation and SHA-256 verification before installation.

## Chatlog layout

The folder structure remains organized by year and month:

```text
Afterline Chatlogs
└── 2026
    └── 08 - August
        ├── Chatlog [Server A] [21-August-2026].txt
        └── Chatlog [Server B] [21-August-2026].txt
```

A later login to the same server on the same date continues the existing file instead of creating another copy. Switching to a different server finalizes the current file and uses the other server's own daily file. A connection that remains active across midnight starts writing to the new day's file when the first post-midnight chat line arrives.

Server names are sanitized automatically before being used in Windows filenames. If a friendly name cannot be resolved, Afterline falls back to `Unknown Server` while continuing to capture normally.

## Windows builds and releases

Development builds are produced by GitHub Actions as a self-contained Windows x64 application. The downloadable artifact contains only `Afterline.exe`; the .NET SDK is not required to run it.

Public releases are published from version tags such as `v0.6.0`. The release workflow publishes both the Windows executable and a matching `.sha256` checksum file. Afterline's in-app updater reads the latest public GitHub Release, downloads only after the user chooses `Update now`, verifies the executable against that checksum, safely replaces the running build and restarts the application. If replacement fails, the updater attempts to restore the previous executable.

## Data and privacy

Afterline stores chatlogs and application data locally. Chatlogs are ordinary text files in the directory selected by the user. Captured chat is not uploaded by Afterline, and the application does not include telemetry or cloud chat syncing.

Update checks contact the public GitHub Releases API for this repository. No GitHub token is embedded in Afterline. An executable is downloaded only after the user explicitly chooses to install an available update.

## FiveM interaction and server rules

Afterline is designed as a passive, read-only companion utility. It reads chat that has already been rendered by the local FiveM NUI and uses normal server information endpoints to resolve friendly server names. It does not send NUI callbacks, trigger gameplay or server events, inject resources, automate gameplay, modify game memory, or bypass anti-cheat systems.

Users are responsible for checking and following the rules of each server or community before using external chat-logging or archival tools.

## Independence

Afterline is an independent third-party text roleplay chat parser and archive utility. It is not affiliated with, endorsed by, sponsored by, or approved by Rockstar Games, Cfx.re, FiveM, or any roleplay server or community. FiveM is referenced solely to describe compatibility.
