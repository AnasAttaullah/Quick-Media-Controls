using Media_Control_Tray_Icon.Services;
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

namespace Media_Control_Tray_Icon
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private NotifyIcon trayIcon;
        private MediaFlyout mediaFlyout;

        private ImageSource noMediaLightIcon;
        private ImageSource noMediaDarkIcon;
        private ImageSource playLightIcon;
        private ImageSource playDarkIcon;
        private ImageSource pauseLightIcon;
        private ImageSource pauseDarkIcon;

        private MediaSessionService _mediaService;
        public ApplicationTheme currentAppTheme;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Create a hidden window to provide message pump for tray icon
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

            currentAppTheme = ApplicationThemeManager.GetAppTheme();
            trayIcon = (NotifyIcon)FindResource("trayIcon");

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

            // Events
            trayIcon.LeftClick += TrayIcon_LeftClickAsync;
            trayIcon.LeftDoubleClick += TrayIcon_LeftDoubleClickAsync;
            trayIcon.RightClick += TrayIcon_RightClick;

            _mediaService.SessionChanged += MediaService_SessionChanged;
            _mediaService.PlaybackInfoChanged += MediaService_PlaybackInfoChanged;
            _mediaService.MediaPropertiesChanged += MediaService_MediaPropertiesChanged;

            ApplicationThemeManager.Changed += ApplicationThemeManager_Changed;

            // Register the TrayIcon
            RegisterTrayIcon();
            UpdateTrayIcon();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ApplicationThemeManager.Changed -= ApplicationThemeManager_Changed;

            if (trayIcon != null)
            {
                trayIcon.LeftClick -= TrayIcon_LeftClickAsync;
                trayIcon.LeftDoubleClick -= TrayIcon_LeftDoubleClickAsync;
                trayIcon.RightClick -= TrayIcon_RightClick;
                
                if (trayIcon.IsRegistered)
                {
                    trayIcon.Unregister();
                }
                trayIcon.Dispose();
            }

            if (_mediaService != null)
            {
                _mediaService.SessionChanged -= MediaService_SessionChanged;
                _mediaService.PlaybackInfoChanged -= MediaService_PlaybackInfoChanged;
                _mediaService.MediaPropertiesChanged -= MediaService_MediaPropertiesChanged;
                _mediaService.Dispose();
            }
            if (mediaFlyout != null)
            {
                mediaFlyout.Close();
                mediaFlyout = null;
            }

            MainWindow?.Close();

            base.OnExit(e);
        }

        // Methods
        private void RegisterTrayIcon()
        {
            if (!trayIcon.IsRegistered)
            {
                trayIcon.Register();
            }
        }
        private static ImageSource LoadTrayIcon(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);

            var image = BitmapFrame.Create(
                uri,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);

            image.Freeze(); // important for cross-thread usage

            return image;
        }
        private void PreloadIconAssets()
        {
            playLightIcon = LoadTrayIcon("icons/playLight.ico");
            playDarkIcon = LoadTrayIcon("icons/playDark.ico");
            pauseLightIcon = LoadTrayIcon("icons/pauseLight.ico");
            pauseDarkIcon = LoadTrayIcon("icons/pauseDark.ico");
            noMediaLightIcon = LoadTrayIcon("icons/noMediaLight.ico");
            noMediaDarkIcon = LoadTrayIcon("icons/noMediaDark.ico");
        }


        private void UpdateTrayIcon()
        {
            // Check if we're already on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateTrayIcon);
                return;
            }

            bool isPlaying = _mediaService.IsPlaying();
                         bool isDarkMode = currentAppTheme == ApplicationTheme.Dark;

                if (_mediaService.CurrentSession is null)
                {
                    trayIcon.Icon = isDarkMode ? noMediaDarkIcon : noMediaLightIcon;
                    trayIcon.TooltipText = "No Media Playing";
                    return;
                }

                trayIcon.Icon = isPlaying
                    ? (isDarkMode ? pauseDarkIcon : pauseLightIcon)
                    : (isDarkMode ? playDarkIcon : playLightIcon);

            trayIcon.TooltipText = _mediaService.CurrentPlaybackInfo?.PlaybackStatus.ToString() ?? "Unknown";
            
            if (mediaFlyout != null)
            {
                mediaFlyout.UpdateIcon();
            }
        }

        // EVENT HANDLERS

        private async void TrayIcon_LeftClickAsync([System.Diagnostics.CodeAnalysis.NotNull] NotifyIcon sender, RoutedEventArgs e)
        {
             await _mediaService.TogglePlayPauseAsync();
        }

        private async void TrayIcon_LeftDoubleClickAsync([System.Diagnostics.CodeAnalysis.NotNull] NotifyIcon sender, RoutedEventArgs e)
        {
            await _mediaService.SkipNextAsync();
        }
         
        private async void TrayIcon_RightClick([System.Diagnostics.CodeAnalysis.NotNull] NotifyIcon sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Right mouse click");
            if(mediaFlyout == null)
            {
                mediaFlyout = new MediaFlyout(_mediaService);
            }
            _mediaService.FetchMediaAsync();
            mediaFlyout.showFlyout();
            
        }

        private void MediaService_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            // update the details on the popup
            // new thumbnails and stuuff
            if (mediaFlyout != null)
            {
                mediaFlyout.UpdateMediaInfo();
            }
            }
        private void MediaService_SessionChanged(object? sender, GlobalSystemMediaTransportControlsSessionManager e)
        {
            // when the session is closed 
            UpdateTrayIcon();        
        }

        private void MediaService_PlaybackInfoChanged(object? sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo e)
        {
            UpdateTrayIcon();
            Debug.WriteLine(e.PlaybackStatus.ToString());
        }
        private void ApplicationThemeManager_Changed(ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent)
        {
            currentAppTheme = currentApplicationTheme;
            UpdateTrayIcon();
        }
    }
}
