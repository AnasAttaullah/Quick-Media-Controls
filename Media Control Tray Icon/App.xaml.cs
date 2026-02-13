using Media_Control_Tray_Icon.Services;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;

namespace Media_Control_Tray_Icon
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private NotifyIcon trayIcon;

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
            currentAppTheme = ApplicationThemeManager.GetAppTheme();
            trayIcon = (NotifyIcon)FindResource("trayIcon");

            try
            {
                _mediaService = new MediaSessionService();
                await _mediaService.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Startup Error");
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


            // Registering the TrayIcon
            if (MainWindow is not null)
            {
                MainWindow.Loaded += MainWindow_Loaded;
            }
            else
            {
                Dispatcher.BeginInvoke(RegisterTrayIcon, DispatcherPriority.ApplicationIdle);
            }
            UpdateTrayIcon();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.LeftClick -= TrayIcon_LeftClickAsync;
                trayIcon.LeftDoubleClick -= TrayIcon_LeftDoubleClickAsync;
                trayIcon.RightClick -= TrayIcon_RightClick;
            }

            if (_mediaService != null)
            {
                _mediaService.SessionChanged -= MediaService_SessionChanged;
                _mediaService.PlaybackInfoChanged -= MediaService_PlaybackInfoChanged;
                _mediaService.MediaPropertiesChanged -= MediaService_MediaPropertiesChanged;
                _mediaService.Dispose();
            }

            ApplicationThemeManager.Changed -= ApplicationThemeManager_Changed;

            if (MainWindow != null)
            {
                MainWindow.Loaded -= MainWindow_Loaded;
            }
            trayIcon?.Dispose();
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
         
        private void TrayIcon_RightClick([System.Diagnostics.CodeAnalysis.NotNull] NotifyIcon sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Right mouse click");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterTrayIcon();
        }
        private void MediaService_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            // update the details on the popup
            // new thumbnails and stuuff
        }
        private void MediaService_SessionChanged(object? sender, GlobalSystemMediaTransportControlsSessionManager e)
        {
            UpdateTrayIcon();
        }

        private void MediaService_PlaybackInfoChanged(object? sender, Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackInfo e)
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
