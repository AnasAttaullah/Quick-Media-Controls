using Quick_Media_Controls.Models;
using Quick_Media_Controls.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;

namespace Quick_Media_Controls
{
    public partial class MediaFlyout : FluentWindow
    {
        private readonly MediaSessionService _sessionManager;
        private AppSettings _appSettings;
        private bool _IsDragEnabled;
        private bool _isAnimatingClose;
        private double _homeTop;
        private const double FlyoutScreenMargin = 11;
        private const double MinFlyoutWidth = 300;
        private const double MaxFlyoutWidth = 360;
        private const double MinFlyoutHeight = 96;
        private const double MaxFlyoutHeight = 112;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCACTIVATE = 0x0086;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        /// <summary>
        /// Releases unused physical memory pages back to the OS.
        /// </summary>
        private static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
                GC.WaitForPendingFinalizers();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to trim working set: {ex.Message}");
            }
        }

        public MediaFlyout(MediaSessionService sessionManager, AppSettings appSettings)
        {
            if (Application.Current is App app)
            {
                app.ApplyApplicationTheme(appSettings.Theme.AppTheme);
            }
            else
            {
                ApplicationThemeManager.ApplySystemTheme();
            }
            ApplicationAccentColorManager.ApplySystemAccent();

            _appSettings = appSettings;

            _sessionManager = sessionManager;
            _IsDragEnabled = appSettings.General.MoveFlyoutByDefault;

            Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;

            InitializeComponent();
            PositionFlyoutOnPrimaryScreen();

            MoveFlyoutToggle.IsChecked = _IsDragEnabled;
            SourceInitialized += OnSourceInitialized;
        }

        private async void OnSourceInitialized(object? sender, EventArgs e)
        {
            // Disables Default WPF Window Animations
            var hwnd = new WindowInteropHelper(this).Handle;
            int disabled = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disabled, sizeof(int));

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            PositionFlyoutOnPrimaryScreen();
            await UpdateMediaInfo();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCACTIVATE)
            {
                if (wParam == IntPtr.Zero)
                {
                    handled = true;
                    return DefWindowProc(hwnd, WM_NCACTIVATE, new IntPtr(1), lParam);
                }
            }
            return IntPtr.Zero;
        }

        private void PositionFlyoutOnPrimaryScreen()
        {
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen == null)
            {
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            var workAreaPx = primaryScreen.WorkingArea;

            var workLeftDip = workAreaPx.Left / dpi.DpiScaleX;
            var workTopDip = workAreaPx.Top / dpi.DpiScaleY;
            var workWidthDip = workAreaPx.Width / dpi.DpiScaleX;
            var workHeightDip = workAreaPx.Height / dpi.DpiScaleY;

            // Responsive sizing: ~25% width and ~15.5% height of work area, clamped
            Width = Math.Clamp(workWidthDip * 0.25, MinFlyoutWidth, MaxFlyoutWidth);
            Height = Math.Clamp(workHeightDip * 0.155, MinFlyoutHeight, MaxFlyoutHeight);

            double defaultLeft = workLeftDip + workWidthDip - Width - FlyoutScreenMargin;
            double defaultTop = workTopDip + workHeightDip - Height - FlyoutScreenMargin;

            if (_appSettings.General.FlyoutPositionX.HasValue && _appSettings.General.FlyoutPositionY.HasValue)
            {
                double savedLeft = _appSettings.General.FlyoutPositionX.Value;
                double savedTop = _appSettings.General.FlyoutPositionY.Value;

                if (IsPositionOnScreen(savedLeft, savedTop, Width, Height))
                {
                    Left = savedLeft;
                    _homeTop = Top = savedTop;
                    return;
                }
            }

            Left = defaultLeft;
            _homeTop = Top = defaultTop;
        }

        private static bool IsPositionOnScreen(double left, double top, double width, double height)
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualWidth = SystemParameters.VirtualScreenWidth;
            var virtualHeight = SystemParameters.VirtualScreenHeight;

            bool isWithinHorizontal = (left + width > virtualLeft + 20) && (left < virtualLeft + virtualWidth - 20);
            bool isWithinVertical = (top + height > virtualTop + 20) && (top < virtualTop + virtualHeight - 20);

            return isWithinHorizontal && isWithinVertical;
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
            LockIcon.Symbol = _sessionManager.IsLocked ? SymbolRegular.LockClosed16 : SymbolRegular.LockOpen16;

            if (_sessionManager.CanCycle)
            {
                CycleSessionButton.Opacity = 1.0;
                CycleSessionButton.IsHitTestVisible = true;
            }
            else
            {
                CycleSessionButton.Opacity = 0.4;
                CycleSessionButton.IsHitTestVisible = false;
            }
        }

        public async Task UpdateMediaInfo()
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(async () => await UpdateMediaInfo()).Task.Unwrap();
                return;
            }

            if (_sessionManager.CurrentMediaProperties != null)
            {
                if (mediaPlayingGrid.Visibility != Visibility.Visible)
                {
                    mediaPlayingGrid.Visibility = Visibility.Visible;
                    noMediaPlayingGrid.Visibility = Visibility.Collapsed;
                }

                var thumbnail = await LoadMediaThumbnailAsync(_sessionManager.CurrentMediaProperties.Thumbnail);
                playingMediaThumbnail.Source = thumbnail;

                var mediaTitle = _sessionManager.CurrentMediaProperties.Title;
                playingMediaTitle.Text = mediaTitle.Length > 28 ? mediaTitle[..28] + "..." : mediaTitle;
                playingMediaTitle.ToolTip = (mediaTitle.Length > 28) ? mediaTitle : null;
                playingMediaArtist.Text = _sessionManager.CurrentMediaProperties.Artist;
            }
            else
            {
                mediaPlayingGrid.Visibility = Visibility.Collapsed;
                noMediaPlayingGrid.Visibility = Visibility.Visible;
            }

            if (!IsVisible)
            {
                TrimWorkingSet();
            }
        }

        private async Task<BitmapImage?> LoadMediaThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnailRef)
        {
            if (thumbnailRef == null)
                return null;

            try
            {
                using var stream = await thumbnailRef.OpenReadAsync();
                if (stream == null || stream.Size == 0)
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 160;  // Decode at 160px
                bitmap.DecodePixelHeight = 160;

                using var memStream = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(memStream);
                memStream.Position = 0;

                bitmap.StreamSource = memStream;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load thumbnail: {ex.Message}");
                return null;
            }
        }

        private bool IsInUpperHalfOfScreen()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var screen = handle != IntPtr.Zero ? Screen.FromHandle(handle) : Screen.PrimaryScreen;
            if (screen == null) return false;

            var dpi = VisualTreeHelper.GetDpi(this);
            double workTopDip = screen.WorkingArea.Top / dpi.DpiScaleY;
            double workHeightDip = screen.WorkingArea.Height / dpi.DpiScaleY;
            double screenCenterY = workTopDip + (workHeightDip / 2.0);

            double flyoutCenterY = Top + (Height / 2.0);
            return flyoutCenterY < screenCenterY;
        }

        public async Task ShowFlyoutAsync()
        {
            Root.Opacity = 0;
            _isAnimatingClose = false;
            BeginAnimation(Window.TopProperty, null);
            Root.BeginAnimation(OpacityProperty, null);

            Top = _homeTop;
            Visibility = Visibility.Visible;

            // Force topmost activation workaround for WPF
            Topmost = true;
            Topmost = false;
            Topmost = true;

            Activate();
            Focus();
            Keyboard.Focus(this);

            if (!_appSettings.General.EnableFlyoutAnimations)
            {
                Root.Opacity = 1;
                return;
            }

            bool upperHalf = IsInUpperHalfOfScreen();
            double startTop = upperHalf ? _homeTop - 15 : _homeTop + 15;

            var duration = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var slide = new DoubleAnimation(startTop, _homeTop, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            slide.Completed += (_, _) => Top = _homeTop;
            BeginAnimation(Window.TopProperty, slide);

            var fadeIn = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) => Root.Opacity = 1;
            Root.BeginAnimation(OpacityProperty, fadeIn);
        }

        public void AnimateClose()
        {
            if (_isAnimatingClose) return;

            if (!_appSettings.General.EnableFlyoutAnimations)
            {
                Hide();
                Root.Opacity = 1;
                Top = _homeTop;
                TrimWorkingSet();
                return;
            }

            _isAnimatingClose = true;

            bool upperHalf = IsInUpperHalfOfScreen();
            double endTop = upperHalf ? _homeTop - 15 : _homeTop + 15;

            var duration = new Duration(TimeSpan.FromMilliseconds(180));
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var slide = new DoubleAnimation(_homeTop, endTop, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            BeginAnimation(Window.TopProperty, slide);

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
                Root.Opacity = 1;
                Top = _homeTop;
                TrimWorkingSet();
            };
            Root.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Flyout_Deactivated(object sender, EventArgs e)
        {
            if (!_appSettings.General.AutoHideFlyout) return;
            AnimateClose();
        }

        public void ApplySettings(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _IsDragEnabled = appSettings.General.MoveFlyoutByDefault;
            Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
            MoveFlyoutToggle.IsChecked = _IsDragEnabled;
        }


        private async void PlayPauseButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                await _sessionManager.TogglePlayPauseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void NextButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                await _sessionManager.SkipNextAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void PreviousButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                await _sessionManager.SkipPreviousAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void LockButton_Click(object sender, RoutedEventArgs e)
        {
            await _sessionManager.ToggleLockAsync();
            await UpdateMediaInfo();
            UpdateIcons();
        }

        private async void CycleSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_sessionManager.CanCycle) return;
            await _sessionManager.CycleSessionAsync();
            await UpdateMediaInfo();
            UpdateIcons();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed && _IsDragEnabled)
            {
                DragMove();
                _homeTop = this.Top;

                _appSettings.General.FlyoutPositionX = Left;
                _appSettings.General.FlyoutPositionY = Top;
                new AppSettingsService().Save(_appSettings);
            }
        }

        private void MoveFlyoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem)
                return;

            _IsDragEnabled = menuItem.IsChecked;
            Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
        }

        private void ResetPositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _appSettings.General.FlyoutPositionX = null;
            _appSettings.General.FlyoutPositionY = null;
            new AppSettingsService().Save(_appSettings);

            PositionFlyoutOnPrimaryScreen();
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

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var setting = new SettingsWindow();
            setting.Show();
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}