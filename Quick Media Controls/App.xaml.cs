using AutoUpdaterDotNET;
using Quick_Media_Controls.Models;
using Quick_Media_Controls.Services;
using System;
using System.Diagnostics;
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

        private ImageSource noMediaLightIcon;
        private ImageSource noMediaDarkIcon;
        private ImageSource playLightIcon;
        private ImageSource playDarkIcon;
        private ImageSource pauseLightIcon;
        private ImageSource pauseDarkIcon;

        private MediaSessionService _mediaService;
        private AppSettings _appSettings = null;
        private GlobalHotkeyService _globalHotkeyService;
        private AppSettingsService _appSettingsService = new();

        public ApplicationTheme currentAppTheme;
        
        public AppSettings GetSettingsSnapshot() => _appSettings.Clone();

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

            _mediaService.SessionChanged += MediaService_SessionChanged;
            _mediaService.PlaybackInfoChanged += MediaService_PlaybackInfoChanged;
            _mediaService.MediaPropertiesChanged += MediaService_MediaPropertiesChanged;

            ApplicationThemeManager.Changed += ApplicationThemeManager_Changed;

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
            ConfigureAutoUpdater();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ApplicationThemeManager.Changed -= ApplicationThemeManager_Changed;

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

            _globalHotkeyService = new GlobalHotkeyService(MainWindow);
            _globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;

            try
            {
                _globalHotkeyService.Apply(_appSettings.Keybinds);

            }
            catch (Exception ex)
            {
                _appSettings = AppSettings.CreateDefault();
                _appSettingsService.Save(_appSettings);
                _globalHotkeyService.Apply(_appSettings.Keybinds);

                MessageBox.Show($"Invalid saved keybinds. Defaults restored.\n\n{ex.Message}", "Keybinds");
            }
        }

        public bool TrySaveSettings(AppSettings updatedSettings, out string? error)
        {
            error = null;

            try
            {
                _globalHotkeyService.Apply(updatedSettings.Keybinds);

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

        private void ConfigureAutoUpdater()
        {
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.Mandatory = false;
            AutoUpdater.UpdateMode = Mode.Normal;

            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                AutoUpdater.Start("https://raw.githubusercontent.com/AnasAttaullah/Quick-Media-Controls/main/update.xml");
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


        private async void TrayIcon_LeftClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void TrayIcon_LeftDoubleClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
             await _mediaService.SkipNextAsync();
        }

        private async void TrayIcon_RightClickAsync(NotifyIcon sender, RoutedEventArgs e)
        {
            _mediaFlyout ??= new MediaFlyout(_mediaService, _appSettings);
            UpdatePlaybackButtonsStatus();
            await _mediaFlyout.ShowFlyoutAsync();
        }

        private void MediaService_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            _mediaFlyout?.UpdateMediaInfo();
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

        private async void GlobalHotkeyService_HotkeyPressed(object? sender, GlobalHotkeyAction action)
        {
            switch (action)
            {
                case GlobalHotkeyAction.PlayPause:
                    await _mediaService.TogglePlayPauseAsync();
                    break;
                case GlobalHotkeyAction.NextTrack:
                    await _mediaService.SkipNextAsync();
                    break;
                case GlobalHotkeyAction.PreviousTrack:
                    await _mediaService.SkipPreviousAsync();
                    break;
                case GlobalHotkeyAction.OpenFlyout:
                    if (_mediaFlyout == null)
                    {
                        _mediaFlyout = new MediaFlyout(_mediaService, _appSettings);
                    }
                    if (_mediaFlyout.IsVisible)
                    {
                        _mediaFlyout.AnimateClose();
                        return;
                    }
                    await _mediaFlyout.ShowFlyoutAsync();
                    break;
            }
        }

        private void ApplicationThemeManager_Changed(ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent)
        {
            currentAppTheme = currentApplicationTheme;
            UpdateTrayIcon();
        }
    }
}
