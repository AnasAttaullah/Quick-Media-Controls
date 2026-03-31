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

<table align="center">
  <tr>
    <th>Action</th>
    <th>Mouse Input</th>
    <th>Keyboard Shortcut</th>
  </tr>
  <tr>
    <td>Play or Pause</td>
    <td>Left Click</td>
    <td><code>Alt + P</code></td>
  </tr>
  <tr>
    <td>Next Track</td>
    <td>Double Click</td>
    <td><code>Alt + N</code></td>
  </tr>
  <tr>
    <td>Previous Track</td>
    <td>—</td>
    <td><code>Alt + Shift + P</code></td>
  </tr>
  <tr>
    <td>Open Flyout</td>
    <td>Right Click</td>
    <td><code>Alt + O</code></td>
  </tr>
</table>

> All keybindings are fully customizable from the settings window.
---

## Download ⬇️

<a href="https://github.com/AnasAttaullah/Quick-Media-Controls/releases/latest">
  <img src="docs/images/download_installerx64.png" alt="Download Now" width="260" style="display:block; margin-left:0;" />
</a>

<p></p>

> The installer bundles .NET 8, which increases the download size from **~14 MB** to **~65 MB**.

Prefer building it yourself? Jump to **[Build from source](#Requirements)**.

---

## Installation 🖥️

1. Download the setup file from Releases (example: `QuickMediaControls-Setup-...exe`)
2. Run the installer
3. Launch **Quick Media Controls**
4. Find it in the **system tray** (near the clock)  
   - If you don’t see it, click the **^** arrow to show hidden tray icons

**💡Tip:** On first use, **pin the tray icon** so it’s always visible.

---

## Features ✨

- **Native experience** on Windows 10 & 11  
- **Light & Dark mode** with Windows accent color  
- **Universal media support** – works with apps, browsers, and players  
- **Flyout panel**: shows title, artist, album art, and playback controls  
- **Smart tray icon**: displays play/pause state or “No Media Playing”  
- **Keyboard & mouse shortcuts** with custom keybinds  
- **Multi-display & DPI aware**  
- **Customizable settings window**  
- **Automatic updates** to keep you up to date

---

## 🖼️ Showcase


https://github.com/user-attachments/assets/12bbd85d-fec1-4cc5-937d-b9ece97e7994




<p align="center">
<table>
  <tr>
    <th></th>
    <th>Dark Mode</th>
    <th>Light Mode</th>
  </tr>
  <tr>
    <td><b>Media Playing</b></td>
    <td><img src="docs/images/Screenshots/windows11_dark.png" width="420"/></td>
    <td><img src="docs/images/Screenshots/windows11_light.png" width="420"/></td>
  </tr>
  <tr>
    <td><b>No Media Playing</b></td>
    <td><img src="docs/images/Screenshots/windows11NoMedia_dark.png" width="420"/></td>
    <td><img src="docs/images/Screenshots/windows11NoMedia_light.png" width="420"/></td>
  </tr>
</table>
</p>

<p align="center">
  <img src="docs/images/mouse_clicks_showcase.gif" width="500"/>
  <img src="docs/images/settings_window_showcase.gif" width="500"/>
</p>

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
