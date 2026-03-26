using AutoUpdaterDotNET;
using Microsoft.Win32;
using Quick_Media_Controls.Models;
using Quick_Media_Controls.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Wpf.Ui.Appearance;
using Wpf.Ui.Tray.Controls;

namespace Quick_Media_Controls
{
    /// <summary>
    ///  Application entry point managing media session integration and system tray icon.
    /// </summary>
    public partial class App : Application
    {
        private NotifyIcon _trayIcon;
        private MediaFlyout? _mediaFlyout;

        private readonly DispatcherTimer _displayChangeReloadTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };

        private const string UpdateManifestUrl = "https://raw.githubusercontent.com/AnasAttaullah/Quick-Media-Controls/main/update.xml";

        private ImageSource noMediaLightIcon;
        private ImageSource noMediaDarkIcon;
        private ImageSource playLightIcon;
        private ImageSource playDarkIcon;
        private ImageSource pauseLightIcon;
        private ImageSource pauseDarkIcon;

        private AppSettings _appSettings = null;
        private MediaSessionService _mediaService;
        private GlobalHotkeyService _globalHotkeyService;
        private StartupRegistrationService _startupRegistrationService;
        private AppSettingsService _appSettingsService = new();
        private AppDistributionService _appDistributionService = new();

        public ApplicationTheme currentAppTheme;

        public bool IsPackagedDistribution => _appDistributionService.IsPackaged;
        public AppSettings GetSettingsSnapshot() => _appSettings.Clone();

        public App()
        {
            _startupRegistrationService = new StartupRegistrationService(
                appDistributionService: _appDistributionService);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            currentAppTheme = ApplicationThemeManager.GetAppTheme();
            _trayIcon = (NotifyIcon)FindResource("trayIcon");

            try
            {
                _mediaService = new MediaSessionService();
                await _mediaService.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error");
                Shutdown();
            }
            PreloadIconAssets();

            _trayIcon.LeftClick += TrayIcon_LeftClickAsync;
            _trayIcon.LeftDoubleClick += TrayIcon_LeftDoubleClickAsync;
            _trayIcon.RightClick += TrayIcon_RightClickAsync;
            _trayIcon.MiddleClick += TrayIcon_MiddleClickAsync;

            _mediaService.SessionChanged += MediaService_SessionChanged;
            _mediaService.PlaybackInfoChanged += MediaService_PlaybackInfoChanged;
            _mediaService.MediaPropertiesChanged += MediaService_MediaPropertiesChanged;

            ApplicationThemeManager.Changed += ApplicationThemeManager_Changed;

            _displayChangeReloadTimer.Tick += DisplayChangeReloadTimer_TickAsync;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            // Hidden window to provide message pump for tray icon
            MainWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                ShowActivated = false,
                AllowsTransparency = false,
                Visibility = Visibility.Hidden,
                Left = -10000,
                Top = -10000
            };

            MainWindow.Show();
            MainWindow.Hide();

            InitializeAppSettings();

            RegisterTrayIcon();
            UpdateTrayIcon();

            if (_appSettings.General.CheckForUpdatesOnStartup && !_appDistributionService.IsPackaged)
            {
                ConfigureAutoUpdater();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ApplicationThemeManager.Changed -= ApplicationThemeManager_Changed;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

            _displayChangeReloadTimer.Tick -= DisplayChangeReloadTimer_TickAsync;
            _displayChangeReloadTimer.Stop();

            if (_globalHotkeyService != null)
            {
                _globalHotkeyService.HotkeyPressed -= GlobalHotkeyService_HotkeyPressed;
                _globalHotkeyService.Dispose();
            }

            if (_trayIcon != null)
            {
                _trayIcon.LeftClick -= TrayIcon_LeftClickAsync;
                _trayIcon.LeftDoubleClick -= TrayIcon_LeftDoubleClickAsync;
                _trayIcon.RightClick -= TrayIcon_RightClickAsync;
                _trayIcon.MiddleClick -= TrayIcon_MiddleClickAsync;

                if (_trayIcon.IsRegistered)
                {
                    _trayIcon.Unregister();
                }
                _trayIcon.Dispose();
            }

            if (_mediaService != null)
            {
                _mediaService.SessionChanged -= MediaService_SessionChanged;
                _mediaService.PlaybackInfoChanged -= MediaService_PlaybackInfoChanged;
                _mediaService.MediaPropertiesChanged -= MediaService_MediaPropertiesChanged;
                _mediaService.Dispose();
            }
            if (_mediaFlyout != null)
            {
                _mediaFlyout.Close();
                _mediaFlyout = null;
            }

            MainWindow?.Close();
            base.OnExit(e);
        }

        private void InitializeAppSettings()
        {
            _appSettings = _appSettingsService.Load();
            _appSettings.Keybinds.KeyboardShortcuts ??= KeyboardShortcutSettings.CreateDefault();
            _appSettings.Keybinds.MouseShortcuts ??= MouseShortcutSettings.CreateDefault();

            _globalHotkeyService = new GlobalHotkeyService(MainWindow);
            _globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;

            try
            {
                _globalHotkeyService.Apply(_appSettings.Keybinds);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Hotkeys Registration Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (!_appSettings.General.StartupRegistrationInitialized)
            {
                try
                {
                    _startupRegistrationService.Apply(_appSettings.General.RunAtStartup);
                }
                catch
                {
                }

                _appSettings.General.StartupRegistrationInitialized = true;
            }

            _appSettings.General.RunAtStartup = _startupRegistrationService.IsRegistered();
            _appSettingsService.Save(_appSettings);
        }

        public bool TrySaveSettings(AppSettings updatedSettings, out string? error)
        {
            error = null;

            updatedSettings.Keybinds.KeyboardShortcuts ??= KeyboardShortcutSettings.CreateDefault();
            updatedSettings.Keybinds.MouseShortcuts ??= MouseShortcutSettings.CreateDefault();

            if (!TryValidateMouseShortcutMappings(updatedSettings.Keybinds.MouseShortcuts, out error))
            {
                return false;
            }

            try
            {
                _globalHotkeyService.Apply(updatedSettings.Keybinds);
                _startupRegistrationService.Apply(updatedSettings.General.RunAtStartup);

                _appSettings = updatedSettings.Clone();
                _appSettingsService.Save(_appSettings);
                _mediaFlyout?.ApplySettings(_appSettings);

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to save settings: {ex.Message}";
                return false;
            }
        }

        private void RegisterTrayIcon()
        {
            if (!_trayIcon.IsRegistered)
            {
                _trayIcon.Register();
            }
        }

        private static ImageSource LoadTrayIcon(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);

            var image = BitmapFrame.Create(
                uri,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);

            image.Freeze();

            return image;
        }

        private void UpdateTrayIcon()
        {
            if (_mediaService == null) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(UpdateTrayIcon);
                return;
            }

            bool isPlaying = _mediaService.IsPlaying();
            bool isDarkMode = currentAppTheme == ApplicationTheme.Dark;

            if (_mediaService.CurrentSession is null)
            {
                _trayIcon.Icon = isDarkMode ? noMediaDarkIcon : noMediaLightIcon;
                _trayIcon.TooltipText = "No Media Playing";
                return;
            }

            _trayIcon.Icon = isPlaying
                ? (isDarkMode ? pauseDarkIcon : pauseLightIcon)
                : (isDarkMode ? playDarkIcon : playLightIcon);

            var mediaTitle = _mediaService.CurrentMediaProperties?.Title;
            var mediaArtist = _mediaService.CurrentMediaProperties?.Artist;
            mediaTitle  = mediaTitle?.Length > 35 ? mediaTitle[..32] + "..." : mediaTitle;

            _trayIcon.TooltipText = mediaTitle + $" | {mediaArtist}" ?? "Unknown";

            if (_mediaFlyout != null)
            {
                _mediaFlyout.UpdateIcons();
            }
        }


        public void UpdatePlaybackButtonsStatus()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(UpdatePlaybackButtonsStatus);
                return;
            }
            if (_mediaFlyout != null && _mediaService.CurrentPlaybackInfo != null)
            {
                _mediaFlyout.NextTrackButton.IsEnabled = _mediaService.IsNextEnabled();
                _mediaFlyout.PreviousTrackButton.IsEnabled = _mediaService.IsPreviousEnabled();
            }
        }

        private static void ConfigureAutoUpdaterOptions()
        {
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.Mandatory = false;
            AutoUpdater.UpdateMode = Mode.Normal;
        }

        public void CheckForUpdatesNow()
        {
            ConfigureAutoUpdaterOptions();
            AutoUpdater.Start(UpdateManifestUrl);
        }

        private void ConfigureAutoUpdater()
        {
            ConfigureAutoUpdaterOptions();

            _ = Task.Run(async () =>
            {
                await Task.Delay(20000);
                AutoUpdater.Start(UpdateManifestUrl);
            });
        }

        private void PreloadIconAssets()
        {
            playLightIcon = LoadTrayIcon("Assets\\Icons\\playLight.ico");
            playDarkIcon = LoadTrayIcon("Assets\\Icons\\playDark.ico");
            pauseLightIcon = LoadTrayIcon("Assets\\Icons\\pauseLight.ico");
            pauseDarkIcon = LoadTrayIcon("Assets\\Icons\\pauseDark.ico");
            noMediaLightIcon = LoadTrayIcon("Assets\\Icons\\noMediaLight.ico");
            noMediaDarkIcon = LoadTrayIcon("Assets\\Icons\\noMediaDark.ico");
        }

        public async Task ToggleFlyoutAsync()
        {
                if (_mediaFlyout == null)
                {
                    _mediaFlyout = new MediaFlyout(_mediaService, _appSettings);
                    _mediaFlyout.UpdateIcons();
                    _mediaFlyout.Owner = MainWindow;
                }
                if (_mediaFlyout.IsVisible)
                {
                    _mediaFlyout.AnimateClose();
                    return;
                }

                await _mediaFlyout.ShowFlyoutAsync();
        }

        private void QueueWindowReinitialize()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(QueueWindowReinitialize);
                return;
            }

            _displayChangeReloadTimer.Stop();
            _displayChangeReloadTimer.Start();
        }

        private async void DisplayChangeReloadTimer_TickAsync(object? sender, EventArgs e)
        {
            _displayChangeReloadTimer.Stop();
            await ReloadFlyoutAsync();
        }

        private async Task ReloadFlyoutAsync()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(ReloadFlyoutAsync);
                return;
            }

            if (_mediaFlyout is null) return;

            var wasVisible = _mediaFlyout.IsVisible;
            _mediaFlyout.Close();
            _mediaFlyout = new MediaFlyout(_mediaService, _appSettings);
            _mediaFlyout.Owner = MainWindow;

            if (wasVisible)
            {
                await _mediaFlyout.ShowFlyoutAsync();
            }
        }

        private async Task ExecuteShortcutActionAsync(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.PlayPause:
                    await _mediaService.TogglePlayPauseAsync();
                    break;
                case ShortcutAction.NextTrack:
                    await _mediaService.SkipNextAsync();
                    break;
                case ShortcutAction.PreviousTrack:
                    await _mediaService.SkipPreviousAsync();
                    break;
                case ShortcutAction.OpenFlyout:
                    await ToggleFlyoutAsync();
                    break;
            }
        }

        private static bool TryValidateMouseShortcutMappings(MouseShortcutSettings settings, out string? error)
        {
            string? localError = null;
            var seen = new HashSet<ShortcutAction>();

            bool Add(ShortcutAction? action)
            {
                if (!action.HasValue)
                    return true;

                if (!seen.Add(action.Value))
                {
                    localError = $"Mouse shortcut conflict: \"{action.Value}\" is assigned more than once. " +
                                 "Each non-None mouse action must be unique.";
                    return false;
                }

                return true;
            }

            var ok =
                Add(settings.LeftClick) &&
                Add(settings.DoubleLeftClick) &&
                Add(settings.RightClick) &&
                Add(settings.MiddleClick);

            error = localError;
            return ok;
        }

        private async void TrayIcon_LeftClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.MouseShortcuts.LeftClick);
        }

        private async void TrayIcon_LeftDoubleClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.MouseShortcuts.DoubleLeftClick);
        }

        private async void TrayIcon_RightClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.MouseShortcuts.RightClick);
        }

        private async void TrayIcon_MiddleClickAsync([System.Diagnostics.CodeAnalysis.NotNull] NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.MouseShortcuts.MiddleClick);
        }

        private void MediaService_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            _ = _mediaFlyout?.UpdateMediaInfo();
        }

        private void MediaService_SessionChanged(object? sender, GlobalSystemMediaTransportControlsSessionManager e)
        { 
            UpdateTrayIcon();
        }

        private void MediaService_PlaybackInfoChanged(object? sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo e)
        {
            UpdateTrayIcon();
            UpdatePlaybackButtonsStatus();
        }

        private async void GlobalHotkeyService_HotkeyPressed(object? sender, ShortcutAction action)
        {
            await ExecuteShortcutActionAsync(action);
        }

        private async Task ExecuteMouseShortcutAsync(ShortcutAction? action)
        {
            if (!action.HasValue)
            {
                return;
            }

            await ExecuteShortcutActionAsync(action.Value);
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            QueueWindowReinitialize();
        }

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
            {
                QueueWindowReinitialize();
            }
        }

        private void ApplicationThemeManager_Changed(ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent)
        {
            currentAppTheme = currentApplicationTheme;
            UpdateTrayIcon();
        }
    }
}
