# Quick Media Controls

A lightweight Windows tray utility that puts media controls one click away using the system media session (GSMTC). Built with WPF on `.NET 8`.

## Features

- **Tray icon media control**
  - **Single left-click**: Play / Pause
  - **Double left-click**: Next track
  - **Right-click**: Open a compact **Media Flyout**
- **Media Flyout**
  - Shows **track title**, **artist**, and **album art** (when available)
  - Playback buttons: **Previous / Play-Pause / Next**
  - Auto-hides when it loses focus (deactivated)
- **Theme-aware tray icons**
  - Automatically adapts to **Light/Dark** app theme
  - Displays a **“No Media Playing”** state when no session is active
- **Fast, background-friendly**
  - Runs primarily from the tray with a hidden window to support the message pump

## Screenshots

> Add screenshots to a `docs/screenshots/` folder and update the links below.

### Tray Icon + Flyout
![Tray and Flyout](docs/screenshots/tray-and-flyout.png)

### No Media State
![No Media](docs/screenshots/no-media.png)

## How It Works (High Level)

- Uses Windows **Global System Media Transport Controls** session APIs to read current media state and control playback.
- A WPF tray icon provides quick actions:
  - Click actions map directly to media commands.
  - Right-click shows a flyout window for richer context and controls.

## Getting Started

### Prerequisites
- Windows 10/11
- Visual Studio 2022
- `.NET 8` SDK

### Build & Run
1. Open the solution in Visual Studio.
2. Set the tray app project as Startup Project (if needed).
3. Build and run.
4. Look for the app in the **system tray**.

## Usage

- **Left-click tray icon** → Toggle **Play/Pause**
- **Double left-click tray icon** → **Next track**
- **Right-click tray icon** → Open **Media Flyout**
- In the flyout:
  - Use **Previous / Play-Pause / Next**
  - Click **Exit** to close the app

## Project Structure (Key Files)

- `App.xaml.cs` — Tray icon lifecycle, theme tracking, click handlers, icon updates
- `MediaFlyout.xaml(.cs)` — Flyout UI, media info/thumbnail rendering, playback buttons
- `Services/MediaSessionService` — Media session integration and playback commands

## Roadmap / Ideas

- Configurable hotkeys
- Startup on login
- Per-app session selection (if multiple sessions are available)
- Better text handling (scrolling/marquee for long titles)
- Optional animations and richer theme customization

## Contributing

Contributions are welcome:
- Bug reports and feature requests via Issues
- PRs for fixes and improvements

## License

Add your preferred license (e.g., MIT) in `LICENSE`.

