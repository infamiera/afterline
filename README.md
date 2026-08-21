# Afterline

Afterline is an independent Windows utility for capturing, autosaving, archiving and searching text roleplay chat.

## Features

- Automatic chat capture while a supported game client is active.
- Continuous crash-safe session autosave with a second local recovery copy.
- Automatic recovery of unfinished sessions after an unexpected shutdown.
- Graceful shutdown finalization when the application is exited normally.
- Same-day chatlog continuation: later logins append to the existing daily log with a clear `NEW LOGIN - HH:mm:ss` divider.
- Custom chatlog storage directory.
- Year and month archive organization.
- Optional live chat display inside Afterline.
- Archive browsing with recent logs.
- Multi-term text search for conversations, character names and keywords.
- Plain-text chatlogs that remain usable outside Afterline.
- Optional launch at Windows sign-in while capture remains idle until the game is running.

## Windows builds

Release builds are produced by GitHub Actions as a self-contained Windows x64 application. The downloadable artifact contains only `Afterline.exe`; the .NET SDK is not required to run it.

## Data and privacy

Afterline stores chatlogs and application data locally. Chatlogs are ordinary text files in the directory selected by the user. Search is read-only, and the application does not include an automatic executable downloader or self-updater.

## Independence

Afterline is an independent text roleplay chat parser and archive utility. It is not affiliated with, endorsed by, or developed on behalf of any roleplay server or community.
