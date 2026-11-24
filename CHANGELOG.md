# Changelog

All notable changes to LazyTyper are documented here.

## v1.0.0 — 2026-11-25

Initial public release.

### Added

- Global `F8` per-key mode: each ordinary key emits the next clipboard character.
- Global `F9` auto mode: types clipboard content at roughly 22ms per character.
- Unicode / Chinese / newline / tab output via `SendInput`.
- WinForms HUD with mode, progress, next character, and live hotkey hints.
- Target-window focus guard: automatically stops when the foreground changes.
- DONE completion hold: prevents extra keystrokes from contaminating output; `Esc` releases the keyboard.
- System tray support, hide-to-tray, left-click tray toggle, and progress tooltip.
- `Ctrl+Alt+H` HUD show/hide hotkey.
- Tray-only startup via `LazyTyper.exe --hidden`.
- Automatic fallback hotkeys (`Ctrl+Alt+L` / `Ctrl+Alt+A`) when F8/F9 are occupied.
- Single-file C# source, zero-dependency `scripts/build.ps1`, and GitHub Actions build workflow.

### Tested

- Windows 10/11 (development machine)
- WinForms text target: per-key and auto modes, Chinese/Unicode, newlines, focus guard, DONE guard.
- Hidden startup, tray background operation, F8/F9 while HUD hidden, Ctrl+Alt+H toggle.
