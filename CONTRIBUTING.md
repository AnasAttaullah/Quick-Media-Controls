# Contributing Guidelines

Thanks for considering contributing to Quick Media Controls. This document outlines the process for reporting bugs, proposing new features, and submitting code changes.

## Reporting Issues

Before opening a new issue, please search existing [issues](https://github.com/AnasAttaullah/Quick-Media-Controls/issues) to see if it has already been reported.

- **Bug Reports**: Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.yml). Include your Windows version, app version, media player used (Spotify, browser, etc.), and steps to reproduce.
- **Feature Requests**: Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml). Explain the problem you are trying to solve and describe the proposed behavior.

For general questions or feedback, use [GitHub Discussions](https://github.com/AnasAttaullah/Quick-Media-Controls/discussions).

## Getting Started

### Prerequisites

- Windows 10 (version 1809 or higher) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (version 17.8 or higher) with the **.NET desktop development** workload, or VS Code with C# Dev Kit

### Building and Running Locally

1. Clone the repository:
   ```bash
   git clone https://github.com/AnasAttaullah/Quick-Media-Controls.git
   cd Quick-Media-Controls
   ```

2. Restore dependencies and build:
   ```bash
   dotnet build "Quick Media Controls/Quick Media Controls.csproj"
   ```

3. Run the application:
   ```bash
   dotnet run --project "Quick Media Controls/Quick Media Controls.csproj"
   ```

You can also open `Quick-Media-Controls.sln` in Visual Studio and build/debug directly with `F5`.

## Project Architecture

The repository is structured into two main projects:

- **`Quick Media Controls/`**: The core WPF desktop application.
  - `Views/`: XAML views for the tray flyout (`MediaFlyout.xaml`) and settings UI (`SettingsWindow.xaml`, `Pages/`).
  - `Services/`: Core application logic:
    - `MediaSessionService.cs`: Manages Windows Global System Media Transport Controls (`GSMTC`) session tracking and media control commands.
    - `GlobalHotkeyService.cs`: Win32 low-level hooks for keyboard and mouse shortcuts.
    - `ColorExtractorService.cs`: Extracts dominant accent colors from album artwork for dynamic themes.
    - `StartupRegistrationService.cs`: Handles Windows startup task registration across standalone and packaged builds.
    - `AppSettingsService.cs`: Handles user settings persistence via local JSON.
  - `Models/`: Data structures representing settings, themes, and media session metadata.
- **`QuickMediaControls.Store/`**: Windows Application Packaging project (MSIX) for Microsoft Store releases.

## Development Guidelines

### Performance and Footprint
Quick Media Controls is designed to be lightweight and responsive:
- Keep the idle memory footprint low (~10–12 MB). Avoid unnecessary background allocations or polling loops.
- Handle media session events asynchronously without blocking the UI thread.
- Ensure resources (e.g., bitmaps, hooks) are properly disposed of when sessions close or the app exits.

### Code Style
- Follow standard C# coding conventions and naming rules.
- Use file-scoped namespaces where applicable.
- Keep UI styling consistent with Windows Fluent Design via `WPF-UI`.
- The application runs as a standard user process; do not introduce features requiring administrative privileges.

## Submitting Pull Requests

1. **Discuss first**: For non-trivial features or refactors, open an issue to discuss your proposal before implementing it.
2. **Branch from `main`**: Create a branch with a descriptive name:
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-description
   ```
3. **Test your changes**:
   - Verify functionality with multiple active media sessions (e.g., Spotify, Chrome/Edge YouTube, Apple Music).
   - Test flyout positioning across different taskbar alignments (bottom, top, left, right) and DPI scales.
   - Verify both Dark and Light theme modes.
4. **Submit PR**:
   - Fill out the [Pull Request template](.github/pull_request_template.md).
   - Reference any related issues (e.g., `Closes #12`).
   - Keep pull requests focused on a single change or fix.
