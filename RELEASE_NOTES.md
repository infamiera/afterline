# Afterline 0.6.7 — Advanced Editor & Live Chat Update

This release turns the Image Editor into a proper layered workspace, makes Live Chat colors and recovery considerably more reliable, and reduces Afterline's resource usage while idle.

## Image Editor

- Added independently editable image layers with direct dragging, mouse resizing, exact pixel sizing, four-edge snapping, locking, visible lock badges, and drag-to-reorder stacking.
- Added per-layer paint and transparent eraser tools with undo and redo.
- Added top and left rulers with a configurable keybind.
- Added collapsible Filter Presets and Filters & Adjustments panels, a taller Layers panel, smaller controls, automatic main-sidebar collapse, and an explicit Close Editor button.
- Fixed invisible Base Images, black previews during ruler setup, Marquee mode blocking layer dragging, layer-only exports, and layers extending beyond the original canvas.
- Fixed `.afterlineproj` image serialization and preserved edited pixels, dimensions, position, visibility, order, and lock state.
- Added `Documents\Afterline Projects` as the default project folder, configurable in Editor Settings.
- Moved full-resolution filtering off the UI thread and removed the blocking first-use preview warm-up.

## Live Chat and Log Reader

- Captures the actual rendered colors from FiveM's local chat UI when available.
- Stores exact colors in optional `.colors.jsonl` sidecars while keeping normal `.txt` logs unchanged and readable.
- Falls back to Afterline's automatic formatter when a FiveM row is neutral, incomplete, older, or unsupported.
- Repairs missing colors for recognized messages without overwriting genuine colors supplied by FiveM.
- Added reliable tattoo-name, attachment-command, and Panda Points activity coloring.
- Added arrows beside Clear Display to jump directly to the top or bottom of Live Chat.
- Added color-preserving HTML export to Live Chat and Log Reader, including active filters, timestamps, line numbers, and safe HTML escaping.

## Recovery and updates

- Parse Current Chat now restores persisted cache data even while FiveM is reachable.
- Crashes, power outages, updater restarts, and ordinary Afterline restarts resume the active journal without creating false `[NEW LOGIN]` markers.
- Replaced rate-limited Canary polling with a lightweight release manifest and one authoritative build-aware update check.
- Prevents the installed build or an older cached Canary build from being offered as an update.
- Retains the previous executable until the replacement remains healthy and keeps Retry and Check Again available after failures.

## Performance and stability

- Reduced disconnected FiveM process detection from twice per second to once every three seconds.
- Progressively backs off unchanged live-chat polling while retaining fast capture when new lines arrive.
- Avoids rewriting unchanged raw snapshots, active-session snapshots, and archive indexes.
- Stops UI refresh timers while minimized to tray and limits page-specific status work to visible pages.
- Reuses frozen chat brushes to reduce rendering allocations in long chat views.
- The packaged Windows build is gated by startup, Editor rendering, project persistence, interrupted recovery, chat-color, HTML-safety, updater-manifest, idle CPU, and working-set tests.

## Privacy

All chatlogs, color sidecars, projects, settings, caches, and recovery files remain stored locally and are never uploaded by Afterline. Update checks contact GitHub; friendly-name fallback may read the active FiveM server's standard public information endpoint. Executables are downloaded only after the user chooses to install an update.
