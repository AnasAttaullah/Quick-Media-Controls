using AutoUpdaterDotNET;
using Microsoft.Win32;
using Quick_Media_Controls.Models;
using Quick_Media_Controls.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
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
        private Window _hiddenWindow;
        private HwndSource? _hiddenWindowHwndSource;

        private const uint MSGFLT_ALLOW = 1;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

        private static readonly uint WmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");


        private static Mutex? mutex;
        private static bool _isMutexOwned;

        private readonly DispatcherTimer _displayChangeReloadTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };

        private const string UpdateManifestUrl = "https://raw.githubusercontent.com/AnasAttaullah/Quick-Media-Controls/main/update.xml";

        private ImageSource noMediaLightIcon = default!;
        private ImageSource noMediaDarkIcon = default!;
        private ImageSource playLightIcon = default!;
        private ImageSource playDarkIcon = default!;
        private ImageSource pauseLightIcon = default!;
        private ImageSource pauseDarkIcon = default!;

        private AppSettings _appSettings = default!;
        private MediaSessionService _mediaService = default!;
        private GlobalHotkeyService _globalHotkeyService = default!;
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

            const string mutexName = @"Global/QuickMediaControls";

            bool createdNew;
            mutex = new Mutex(true, mutexName, out createdNew);
            _isMutexOwned = createdNew;

            if (!_isMutexOwned)
            {
                MessageBox.Show("This application is already running.", "Quick Media Controls", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            _appSettings = _appSettingsService.Load();
            _appSettings.Theme ??= ThemeSettings.CreateDefault();
            _appSettings.Keybinds.Shortcuts ??= ShortcutSettings.CreateDefault();
            _appSettings.Keybinds.TrayIconShortcuts ??= TrayIconShortcutSettings.CreateDefault();

            ApplyApplicationTheme(_appSettings.Theme.AppTheme);

            PreloadIconAssets();
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
                return;
            }

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

            _hiddenWindow = new Window
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

            _hiddenWindow.Show();

            var windowHandle = new WindowInteropHelper(_hiddenWindow).EnsureHandle();
            if (WmTaskbarCreated != 0)
            {
                ChangeWindowMessageFilterEx(windowHandle, WmTaskbarCreated, MSGFLT_ALLOW, IntPtr.Zero);
            }

            _hiddenWindowHwndSource = HwndSource.FromHwnd(windowHandle);
            _hiddenWindowHwndSource?.AddHook(HwndMessageHook);

            InitializeAppSettings();

            _mediaFlyout = new MediaFlyout(_mediaService, _appSettings);
            _mediaFlyout.Owner = _hiddenWindow;
            _ = _mediaFlyout.UpdateMediaInfo();

            MainWindow = _hiddenWindow;

            MainWindow.Show();
            MainWindow.Hide();
            RegisterTrayIcon();
            UpdateTrayIcon();

            if (_appSettings.General.CheckForUpdatesOnStartup && !_appDistributionService.IsPackaged)
            {
                ConfigureAutoUpdater();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_hiddenWindowHwndSource != null)
            {
                _hiddenWindowHwndSource.RemoveHook(HwndMessageHook);
                _hiddenWindowHwndSource = null;
            }

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

            if (_isMutexOwned && mutex != null)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                }
            }
            mutex?.Dispose();

            MainWindow?.Close();
            base.OnExit(e);
        }

        public void ApplyApplicationTheme(ApplicationThemeSetting themeSetting)
        {
            switch (themeSetting)
            {
                case ApplicationThemeSetting.Light:
                    ApplicationThemeManager.Apply(ApplicationTheme.Light, updateAccent: false);
                    break;
                case ApplicationThemeSetting.Dark:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark, updateAccent: false);
                    break;
                case ApplicationThemeSetting.System:
                default:
                    ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
                    break;
            }

            currentAppTheme = ApplicationThemeManager.GetAppTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            UpdateTrayIcon();
        }

        private void InitializeAppSettings()
        {
            _appSettings ??= _appSettingsService.Load();
            _appSettings.Theme ??= ThemeSettings.CreateDefault();
            _appSettings.Keybinds.Shortcuts ??= ShortcutSettings.CreateDefault();
            _appSettings.Keybinds.TrayIconShortcuts ??= TrayIconShortcutSettings.CreateDefault();

            ApplyApplicationTheme(_appSettings.Theme.AppTheme);

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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize startup registration: {ex.Message}");
                }

                _appSettings.General.StartupRegistrationInitialized = true;
            }

            _appSettings.General.RunAtStartup = _startupRegistrationService.IsRegistered();
            _appSettingsService.Save(_appSettings);
        }

        public bool TrySaveSettings(AppSettings updatedSettings, out string? error)
        {
            error = null;

            updatedSettings.Theme ??= ThemeSettings.CreateDefault();
            updatedSettings.Keybinds.Shortcuts ??= ShortcutSettings.CreateDefault();
            updatedSettings.Keybinds.TrayIconShortcuts ??= TrayIconShortcutSettings.CreateDefault();

            if (!TryValidateMouseShortcutMappings(updatedSettings.Keybinds.TrayIconShortcuts, out error))
            {
                return false;
            }

            var currentMouseShortcuts = _appSettings.Keybinds.TrayIconShortcuts ?? TrayIconShortcutSettings.CreateDefault();
            var updatedMouseShortcuts = updatedSettings.Keybinds.TrayIconShortcuts;
            var shouldPromptRestart = HasOpenFlyoutMouseBindingChanged(currentMouseShortcuts, updatedMouseShortcuts);

            bool themeChanged = _appSettings.Theme.AppTheme != updatedSettings.Theme.AppTheme ||
                                _appSettings.Theme.TrayIconTheme != updatedSettings.Theme.TrayIconTheme ||
                                _appSettings.Theme.FlyoutTheme != updatedSettings.Theme.FlyoutTheme;

            try
            {
                _globalHotkeyService.Apply(updatedSettings.Keybinds);
                _startupRegistrationService.Apply(updatedSettings.General.RunAtStartup);

                _appSettings = updatedSettings.Clone();
                _appSettingsService.Save(_appSettings);

                if (themeChanged)
                {
                    ApplyApplicationTheme(_appSettings.Theme.AppTheme);
                    _ = ReloadFlyoutAsync();
                }
                else
                {
                    _mediaFlyout?.ApplySettings(_appSettings);
                }

                if (shouldPromptRestart)
                {
                    var result = MessageBox.Show(
                        "The application must restart to apply the updated Open Flyout mouse-click action.\n\nWould you like to restart now?",
                        "Quick Media Controls",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);

                    if (result == MessageBoxResult.Yes)
                    {
                        RestartApplication();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to save settings: {ex.Message}";
                return false;
            }
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

        private void RegisterTrayIcon()
        {
            if (!_trayIcon.IsRegistered)
            {
                _trayIcon.Register();
            }
        }

        private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (WmTaskbarCreated != 0 && (uint)msg == WmTaskbarCreated)
            {
                OnTaskbarCreated();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private async void OnTaskbarCreated()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(OnTaskbarCreated);
                return;
            }

            RecreateTrayIcon();
            await Task.Delay(500);
            UpdateTrayIcon();
        }

        private void RecreateTrayIcon()
        {
            if (_trayIcon == null) return;

            try
            {
                if (_trayIcon.IsRegistered)
                {
                    _trayIcon.Unregister();
                }

                _trayIcon.Register();
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to recreate tray icon on TaskbarCreated: {ex.Message}");
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
            bool isDarkMode = _appSettings.Theme.TrayIconTheme switch
            {
                TrayIconThemeSetting.Light => false,
                TrayIconThemeSetting.Dark => true,
                _ => currentAppTheme == ApplicationTheme.Dark
            };

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
            mediaTitle = mediaTitle?.Length > 35 ? mediaTitle[..32] + "..." : mediaTitle;

            _trayIcon.TooltipText = $"{mediaTitle ?? "Unknown"} | {mediaArtist ?? "Unknown"}";

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
            if (_mediaFlyout != null)
            {
                _mediaFlyout.UpdateIcons();
            }
        }

        public async Task ToggleFlyoutAsync()
        {
            if (_mediaFlyout == null)
            {
                _mediaFlyout = new MediaFlyout(_mediaService, _appSettings);
                _mediaFlyout.Owner = MainWindow;
                await _mediaFlyout.UpdateMediaInfo();
            }
            if (_mediaFlyout.IsVisible)
            {
                _mediaFlyout.AnimateClose();
                return;
            }

            _mediaFlyout.UpdateIcons();
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
            await _mediaFlyout.UpdateMediaInfo();
            _mediaFlyout.UpdateIcons();

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

        private static bool TryValidateMouseShortcutMappings(TrayIconShortcutSettings settings, out string? error)
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

        private static bool HasOpenFlyoutMouseBindingChanged(TrayIconShortcutSettings current, TrayIconShortcutSettings updated)
        {
            static bool ChangedToOrFromOpenFlyout(ShortcutAction? before, ShortcutAction? after) =>
                before != after && (before == ShortcutAction.OpenFlyout || after == ShortcutAction.OpenFlyout);

            return
                ChangedToOrFromOpenFlyout(current.LeftClick, updated.LeftClick) ||
                ChangedToOrFromOpenFlyout(current.DoubleLeftClick, updated.DoubleLeftClick) ||
                ChangedToOrFromOpenFlyout(current.RightClick, updated.RightClick) ||
                ChangedToOrFromOpenFlyout(current.MiddleClick, updated.MiddleClick);
        }

        private static void ConfigureAutoUpdaterOptions()
        {
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.Mandatory = false;
            AutoUpdater.UpdateMode = Mode.Normal;
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

        public void CheckForUpdatesNow()
        {
            ConfigureAutoUpdaterOptions();
            AutoUpdater.Start(UpdateManifestUrl);
        }

        public void RestartApplication()
        {
            var executablePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                Shutdown();
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            });

            Shutdown();
        }


        private async void TrayIcon_LeftClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.TrayIconShortcuts.LeftClick);
        }

        private async void TrayIcon_LeftDoubleClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.TrayIconShortcuts.DoubleLeftClick);
        }

        private async void TrayIcon_RightClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.TrayIconShortcuts.RightClick);
        }

        private async void TrayIcon_MiddleClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await ExecuteMouseShortcutAsync(_appSettings.Keybinds.TrayIconShortcuts.MiddleClick);
        }

        private void MediaService_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            _ = _mediaFlyout?.UpdateMediaInfo();
        }

        private void MediaService_SessionChanged(object? sender, GlobalSystemMediaTransportControlsSessionManager? e)
        {
            UpdateTrayIcon();
            _ = _mediaFlyout?.UpdateMediaInfo();
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
            ApplicationAccentColorManager.ApplySystemAccent();
            UpdateTrayIcon();
            _ = _mediaFlyout?.ApplyFlyoutThemeAsync(_mediaFlyout.CurrentThumbnail);
        }
    }
}
