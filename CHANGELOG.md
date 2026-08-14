# Changelog

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
