<img src="docs/images/banner.jpg" alt="Quick Media Controls banner" />

<p align="center">
  <!-- Badges -->
  <a href="https://github.com/AnasAttaullah/Quick-Media-Controls/releases/latest">
     <img alt="Latest release" src="https://img.shields.io/github/v/release/AnasAttaullah/Quick-Media-Controls?label=Release&logo=github" />
  </a>
  <a href="https://github.com/AnasAttaullah/Quick-Media-Controls/blob/main/LICENSE.txt">
    <img alt="License: GPL v3" src="https://img.shields.io/badge/License-GPLv3-orange?logo=gnu" />
  </a>
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" />
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4" />
  <a href="https://github.com/AnasAttaullah/Quick-Media-Controls/issues">
    <img alt="Issues" src="https://img.shields.io/github/issues/AnasAttaullah/Quick-Media-Controls?logo=github" />
  </a>
  <a href="https://github.com/AnasAttaullah/Quick-Media-Controls/releases/latest">
    <img alt="Download" src="https://img.shields.io/badge/Download-Installer-0078D4" />
  </a>
</p>


A Windows tray app that lets you control whatever is currently playing on your PC without opening the player.

**It's super quick ⚡:**

* **Single Left Click** → Play / Pause ▶️
* **Double Left Click** → Next track ⏭️
* **Right Click** → Open the Flyout 📤
---

## Download (Installer) ⬇️

<a href="https://github.com/AnasAttaullah/Quick-Media-Controls/releases/latest">
  <img src="docs/images/download_installerx64.png" alt="Download Now" width="260" style="display:block; margin-left:0;" />
</a>

<p></p>

> The installer bundles .NET 8, which increases the download size from **~14 MB** to **~65 MB**.

Prefer building it yourself? Jump to **[Build from source](#Requirements)**.

---

## Install & run 🖥️

1. Download the setup file from Releases (example: `QuickMediaControls-Setup-...exe`)
2. Run the installer
3. Launch **Quick Media Controls**
4. Find it in the **system tray** (near the clock)  
   - If you don’t see it, click the **^** arrow to show hidden tray icons

**💡Tip:** On first use, **pin the tray icon** so it’s always visible.

---

## Features ✨

- **Feels native on Windows 10 & 11**
- **Light + Dark mode** support
- Uses your **Windows accent color** for a clean, consistent look
- Works with **whatever Windows is currently playing** (music apps, browsers, players, etc.)
- **Flyout panel**
  - Title / artist / album art (when available)
  - Previous / Play-Pause / Next buttons
  - Smooth **open & close animations**
- **Smart tray icon**
  - Changes based on play/pause
  - Shows **No Media Playing** when nothing is active
- **Automatic updates**
  - Helps you stay up to date

---

## Screenshots 🖼️

|                     | Dark Mode | Light Mode |
|---------------------|-----------|------------|
| **Media Playing**   | <img src="docs/images/Screenshots/windows11_dark.png" width="420" alt="Windows 11 Dark" /> | <img src="docs/images/Screenshots/windows11_light.png" width="420" alt="Windows 11 Light" /> |
| **No Media Playing**| <img src="docs/images/Screenshots/windows11NoMedia_dark.png" width="420" alt="No Media Dark" /> | <img src="docs/images/Screenshots/windows11NoMedia_light.png" width="420" alt="No Media Light" /> |

---

## Build from source 🛠️

### Requirements
- Windows 10/11
- Visual Studio 2022+ (Visual Studio 2026 works)
- .NET 8 SDK

### Steps
1. Open the solution in Visual Studio
2. Build and run
3. The app will appear in the **system tray**

---

## Help / FAQ ❓

**Where is the app window?**  
It’s designed to live in the **tray**. Use the tray icon to control playback and open the flyout.

**Can it start with Windows?**  
Yes, there’s an installer option to launch on startup.

---


## Contributing 🤝

Ideas, issues, and PRs are welcome:  
- Issues: [GitHub Issues](https://github.com/AnasAttaullah/Quick-Media-Controls/issues)

---

## License 📜

Licensed under the **GNU GPL v3.0** — see `LICENSE.txt`.
