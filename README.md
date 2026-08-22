# Afterline

Afterline is an independent Windows utility for capturing, autosaving, archiving and searching text roleplay chat.

## Features

- Automatic chat capture while FiveM is active and connected to a server.
- Best-effort server detection, including a friendly server name when FiveM exposes enough connection metadata to resolve one.
- Server-aware chatlog boundaries: leaving or switching servers immediately finalizes the current server log.
- One readable chatlog per server per calendar day, stored as `Chatlog [Server Name] [21-August-2026].txt`.
- Rejoining the same server later on the same day continues that server/day file with a `[NEW LOGIN]` divider.
- `[DISCONNECTED]` and `[NEW LOGIN]` markers are written to the text log and shown in Live Chat.
- Continuous crash-safe session autosave with a second local recovery copy.
- Automatic recovery of unfinished sessions after an unexpected shutdown.
- Graceful shutdown finalization when the application is exited normally.
- Custom chatlog storage directory with year and month organization.
- Optional Live Chat display inside Afterline.
- Optional Live Chat timestamps.
- Optional RP line coloring: lines whose content begins with `*` or `>` use `#C2A2DA`.
- Right-click any Live Chat line to copy the displayed line to the clipboard.
- Manual `Parse current chat` action for messages still retained by FiveM's current chat UI.
- One-click export of the current captured server session to the user's Downloads folder.
- Archive browsing with recent logs.
- Multi-term text search for conversations, character names and keywords.
- Plain-text chatlogs that remain usable outside Afterline.
- Optional launch at Windows sign-in while capture remains idle until FiveM is running.

## Chatlog layout

The folder structure remains organized by year and month:

```text
Afterline Chatlogs
└── 2026
    └── 08 - August
        ├── Chatlog [Server A] [21-August-2026].txt
        └── Chatlog [Server B] [21-August-2026].txt
```

A later login to the same server on the same date continues the existing file instead of creating another copy. Switching to a different server finalizes the current file and uses the other server's own daily file.

Server names are sanitized automatically before being used in Windows filenames. If a friendly name cannot be resolved, Afterline falls back to `Unknown Server` while continuing to capture normally.

## Windows builds

Release builds are produced by GitHub Actions as a self-contained Windows x64 application. The downloadable artifact contains only `Afterline.exe`; the .NET SDK is not required to run it.

## Data and privacy

Afterline stores chatlogs and application data locally. Chatlogs are ordinary text files in the directory selected by the user. Captured chat is not uploaded by Afterline, and the application does not include telemetry, cloud chat syncing, an automatic executable downloader, or a self-updater.

## FiveM interaction and server rules

Afterline is designed as a passive, read-only companion utility. It reads chat that has already been rendered by the local FiveM NUI and uses normal server information endpoints to resolve friendly server names. It does not send NUI callbacks, trigger gameplay or server events, inject resources, automate gameplay, modify game memory, or bypass anti-cheat systems.

Users are responsible for checking and following the rules of each server or community before using external chat-logging or archival tools.

## Independence

Afterline is an independent third-party text roleplay chat parser and archive utility. It is not affiliated with, endorsed by, sponsored by, or approved by Rockstar Games, Cfx.re, FiveM, or any roleplay server or community. FiveM is referenced solely to describe compatibility.
