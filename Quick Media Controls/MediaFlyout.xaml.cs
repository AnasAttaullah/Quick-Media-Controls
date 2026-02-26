using Quick_Media_Controls.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Quick_Media_Controls
{
    public partial class MediaFlyout : FluentWindow
    {
        private readonly MediaSessionService _sessionManager;
        private bool _IsDragEnabled;
        private bool _isAnimatingClose;
        private double _homeTop;

        private string? _cachedThumbnailKey;
        private BitmapImage? _cachedThumbnail;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        public MediaFlyout(MediaSessionService sessionManager)
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();

            _IsDragEnabled = false;
            _sessionManager = sessionManager;

            Left = SystemParameters.WorkArea.Right - 300 - 110;
            _homeTop = Top = SystemParameters.WorkArea.Bottom - 130;

            InitializeComponent();
            UpdateIcons();

            // Disables Default WPF Window Animations
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int disabled = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disabled, sizeof(int));
        }

        public void UpdateIcons()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(UpdateIcons);
                return;
            }
            if (_sessionManager.CurrentSession == null) return;
            playPauseIcon.Symbol = _sessionManager.IsPlaying() ? SymbolRegular.Pause12 : SymbolRegular.Play12;
        }

        public void ShowFlyout()
        {
            this.Opacity = 0;
            _isAnimatingClose = false;
            BeginAnimation(Window.TopProperty, null);
            BeginAnimation(OpacityProperty, null);

            this.Top = _homeTop;
            this.Visibility = Visibility.Visible;

            // Force topmost activation workaround for WPF
            this.Topmost = true;
            this.Topmost = false;
            this.Topmost = true;

            this.Activate();
            this.Focus();
            Keyboard.Focus(this);

            // Fade-up: slide up 15px + fade in over 220ms with ease-out curve
            var duration = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var slideUp = new DoubleAnimation(_homeTop + 15, _homeTop, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            slideUp.Completed += (_, _) => this.Top = _homeTop;
            BeginAnimation(Window.TopProperty, slideUp);

            var fadeIn = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) => this.Opacity = 1;
            BeginAnimation(OpacityProperty, fadeIn);

            UpdateMediaInfo();
        }

        private void AnimateClose()
        {
            if (_isAnimatingClose) return;
            _isAnimatingClose = true;

            var duration = new Duration(TimeSpan.FromMilliseconds(180));
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var slideDown = new DoubleAnimation(_homeTop, _homeTop + 15, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            BeginAnimation(Window.TopProperty, slideDown);

            var fadeOut = new DoubleAnimation(1, 0, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };
            fadeOut.Completed += (_, _) =>
            {
                if (!_isAnimatingClose) return;
                _isAnimatingClose = false;
                Hide();
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Flyout_Deactivated(object sender, EventArgs e)
        {
            AnimateClose();
        }

        public async void UpdateMediaInfo()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(UpdateMediaInfo);
                return;
            }

            // Skip all work (including async thumbnail I/O) when not visible
            if (Visibility != Visibility.Visible) return;

            if (_sessionManager.CurrentMediaProperties != null)
            {
                if (mediaPlayingGrid.Visibility != Visibility.Visible)
                {
                    mediaPlayingGrid.Visibility = Visibility.Visible;
                    noMediaPlayingGrid.Visibility = Visibility.Collapsed;
                }

                var mediaTitle = _sessionManager.CurrentMediaProperties.Title;
                playingMediaTitle.Text = mediaTitle.Length > 35 ? mediaTitle[..32] + "..." : mediaTitle;
                playingMediaArtist.Text = _sessionManager.CurrentMediaProperties.Artist;

                var thumbnail = await LoadMediaThumbnailAsync(_sessionManager.CurrentMediaProperties.Thumbnail);
                playingMediaThumbnail.Source = thumbnail;
            }
            else
            {
                mediaPlayingGrid.Visibility = Visibility.Collapsed;
                noMediaPlayingGrid.Visibility = Visibility.Visible;
            }
        }

        private async Task<BitmapImage?> LoadMediaThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnailRef)
        {
            if (thumbnailRef == null)
                return null;

            var key = $"{_sessionManager.CurrentMediaProperties?.Title}|{_sessionManager.CurrentMediaProperties?.Artist}";
            if (_cachedThumbnailKey == key && _cachedThumbnail != null)
                return _cachedThumbnail;

            try
            {
                using var stream = await thumbnailRef.OpenReadAsync();
                if (stream == null || stream.Size == 0)
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelHeight = 200; // Decode at 200px 

                using var memStream = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(memStream);
                memStream.Position = 0;

                bitmap.StreamSource = memStream;
                bitmap.EndInit();
                bitmap.Freeze();

                _cachedThumbnailKey = key;
                _cachedThumbnail = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load thumbnail: {ex.Message}");
                return null;
            }
        }

        private async void PlayPauseButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.TogglePlayPauseAsync();
        }

        private async void NextButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.SkipNextAsync();
        }

        private async void PreviousButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.SkipPreviousAsync();
        }

        private void GithubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            const string url = "https://github.com/AnasAttaullah/Quick-Media-Controls";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open GitHub link: {ex}");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed && _IsDragEnabled)
            {
                DragMove();
                _homeTop = this.Top; // Update home after user repositions the flyout
            }
        }

        private void MoveWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _IsDragEnabled = !_IsDragEnabled;
            Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
        }
    }
}