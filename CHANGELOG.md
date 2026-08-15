# Changelog

## [0.1.6] - 2026-08-15

Use a compact status bar when the connection label is hidden.

- Shrink the status bar from 190px to 112px when `2.4G表示` is unchecked.
- Restore the full width when the connection label is shown again.
- Keep the resized bar within the active display and persist its adjusted position.

## [0.1.5] - 2026-08-15

Add an option to hide the connection label from the status bar.

- Add a checked `2.4G表示` tray-menu item.
- Persist the connection-label visibility choice across restarts.
- Keep the battery percentage visible when the connection label is hidden.

## [0.1.4] - 2026-08-15

Keep the status bar above the taskbar after the Windows shell changes topmost window order.

- Reassert the status bar's topmost position every 400 ms without activating it.
- Stop the timer cleanly when the status bar is disposed.

## [0.1.3] - 2026-08-15

Make the status bar usable as a movable, persistent desktop overlay.

- Allow the bar to be moved into the taskbar area while keeping it within the physical display.
- Remember the bar position across restarts and add a tray-menu reset command.
- Keep the bar visible when the notification-area menu or another window takes focus.
- Show the bar with a left click on the tray icon.
- Base receiver-versus-USB ordering on Product IDs instead of display labels.

## [0.1.2] - 2026-08-14

Prefer the verified 2.4GHz receiver when both USB and receiver interfaces are connected.

- Prefer `Usage Page 0xFF60` over `0x008C`.
- Prefer `Keychron Link-KM` (`PID 0xD026`) over the wired device (`PID 0x0440`).
- Make candidate ordering deterministic for diagnostics and battery polling.
- Make HID reader shutdown wait for the reader thread before closing wait handles.
- Extend the probe self-test to cover the complete candidate priority order.

## [0.1.1] - 2026-08-14

Stability and status-bar interaction fix.

- Prevent an HID query exception from terminating the tray process.
- Prevent UI callbacks from running on a worker thread after shutdown.
- Log unexpected UI and application exceptions to the app log.
- Make the status-bar menu label reflect the current action: show or hide.
- Make a tray-icon double-click show the status bar instead of accidentally hiding it.

## [0.1.0] - 2026-08-14

Initial public release.

- Battery level polling over the Keychron Nape Pro vendor HID interface.
- 2.4GHz receiver support verified with `Keychron Link-KM` (`VID 0x3434`, `PID 0xD026`).
- CodexBar-style top status bar.
- Windows notification-area icon with the current percentage.
- Manual refresh, auto-start, launcher shortcut, and diagnostic CLI.
