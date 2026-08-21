# Afterline

Afterline is an independent Windows utility for capturing, autosaving, archiving and searching text roleplay chat.

## Features

- Automatic chat capture while a supported game client is active.
- Manual **Parse current chat** action for importing messages that are still retained by the game's current chat UI when Afterline is started after login.
- Continuous crash-safe session autosave with a second local recovery copy.
- Automatic recovery of unfinished sessions after an unexpected shutdown.
- Immediate disk checkpoint and `[DISCONNECTED]` marker when the game process closes, regardless of reconnect-grace settings.
- Graceful shutdown finalization when the application is exited normally.
- Same-day chatlog continuation: later logins append to the existing daily log with a clear `NEW LOGIN - HH:mm:ss` divider.
- Login and disconnect divider messages are also surfaced in the Live Chat view.
- Custom chatlog storage directory.
- Year and month archive organization.
- Optional live chat display inside Afterline.
- Optional Live Chat timestamps.
- Optional RP-line highlighting: messages whose content begins with `*` or `>` use `#C2A2DA`.
- One-click independent export of the current captured login to the user's Downloads folder.
- Archive browsing with recent logs.
- Multi-term text search for conversations, character names and keywords.
- Plain-text chatlogs that remain usable outside Afterline.
- Optional launch at Windows sign-in while capture remains idle until the game is running.

> **Current-chat import limitation:** Afterline can only import messages still present in the game's NUI/DOM. Messages the game has already discarded cannot be reconstructed by Afterline.

## Windows builds

Release builds are produced by GitHub Actions as a self-contained Windows x64 application. The downloadable artifact contains only `Afterline.exe`; the .NET SDK is not required to run it.

## Data and privacy

Afterline stores chatlogs and application data locally. Chatlogs are ordinary text files in the directory selected by the user. Search is read-only, and the application does not include an automatic executable downloader or self-updater.

## Independence

Afterline is an independent text roleplay chat parser and archive utility. It is not affiliated with, endorsed by, or developed on behalf of any roleplay server or community.
