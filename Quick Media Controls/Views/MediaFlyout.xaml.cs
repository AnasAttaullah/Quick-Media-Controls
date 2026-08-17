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
        private const double MinFlyoutWidth = 350;
        private const double MaxFlyoutWidth = 360;
        private const double MinFlyoutHeight = 96;
        private const double MaxFlyoutHeight = 112;

        private FlyoutPalette? _currentPalette;
        private BitmapImage? _currentThumbnail;

        public BitmapImage? CurrentThumbnail => _currentThumbnail;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCACTIVATE = 0x0086;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

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
                ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
            }
            ApplicationAccentColorManager.ApplySystemAccent();

            _appSettings = appSettings;

            _sessionManager = sessionManager;
            _IsDragEnabled = appSettings.General.MoveFlyoutByDefault;

            Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;

            InitializeComponent();
            PositionFlyoutOnPrimaryScreen();

            PlayPauseButton.MouseEnter += PlayPauseButton_MouseEnter;
            PlayPauseButton.MouseLeave += PlayPauseButton_MouseLeave;
            PlayPauseButton.PreviewMouseDown += PlayPauseButton_PreviewMouseDown;
            PlayPauseButton.PreviewMouseUp += PlayPauseButton_PreviewMouseUp;

            MoveFlyoutToggle.IsChecked = _IsDragEnabled;
            SourceInitialized += OnSourceInitialized;
        }

        private async void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int disabled = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disabled, sizeof(int));

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            PositionFlyoutOnPrimaryScreen();
            await UpdateMediaInfo();
            UpdateIcons();
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
            if (_sessionManager.CurrentSession == null)
            {
                CycleSessionButton.Opacity = 0.4;
                CycleSessionButton.IsHitTestVisible = false;
                NextTrackButton.IsEnabled = false;
                PreviousTrackButton.IsEnabled = false;
                return;
            }

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

            NextTrackButton.IsEnabled = _sessionManager.IsNextEnabled();
            PreviousTrackButton.IsEnabled = _sessionManager.IsPreviousEnabled();
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

                await ApplyFlyoutThemeAsync(thumbnail);
            }
            else
            {
                mediaPlayingGrid.Visibility = Visibility.Collapsed;
                noMediaPlayingGrid.Visibility = Visibility.Visible;
                await ApplyFlyoutThemeAsync(null);
            }

            UpdateIcons();

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
                bitmap.DecodePixelWidth = 160;
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
            UpdateIcons();
            Root.Opacity = 0;
            _isAnimatingClose = false;
            BeginAnimation(Window.TopProperty, null);
            Root.BeginAnimation(OpacityProperty, null);

            Top = _homeTop;
            Show();

            Topmost = true;
            Topmost = false;
            Topmost = true;

            try
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Activate failed: {ex.Message}");
            }

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
            _ = ApplyFlyoutThemeAsync(_currentThumbnail);
        }

        private bool IsCurrentFlyoutDarkMode()
        {
            return _appSettings.Theme.AppTheme switch
            {
                ApplicationThemeSetting.Light => false,
                ApplicationThemeSetting.Dark => true,
                _ => (Application.Current as App)?.currentAppTheme == ApplicationTheme.Dark
            };
        }

        public async Task ApplyFlyoutThemeAsync(BitmapImage? thumbnail)
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(async () => await ApplyFlyoutThemeAsync(thumbnail)).Task.Unwrap();
                return;
            }

            _currentThumbnail = thumbnail;
            bool isDarkMode = IsCurrentFlyoutDarkMode();
            var flyoutTheme = _appSettings.Theme.FlyoutTheme;

            switch (flyoutTheme)
            {
                case FlyoutThemeSetting.AmbientDynamic:
                    DynamicAmbientLayer.Visibility = Visibility.Visible;
                    BlurredArtworkLayer.Visibility = Visibility.Collapsed;
                    MinimalistGlassLayer.Visibility = Visibility.Collapsed;
                    WindowBackdropType = WindowBackdropType.Acrylic;
                    Root.Background = Brushes.Transparent;

                    ThumbnailDropShadow.Opacity = isDarkMode ? 0.32 : 0.18;
                    ThumbnailDropShadow.BlurRadius = 12;
                    ThumbnailContainer.BorderThickness = new Thickness(0);
                    ThumbnailContainer.BorderBrush = null;

                    _currentPalette = await ColorExtractorService.ExtractPaletteAsync(thumbnail, isDarkMode);

                    DynamicAmbientBackground.Background = _currentPalette.AmbientBrush;
                    DynamicAmbientOverlay.Background = new SolidColorBrush(isDarkMode
                        ? Color.FromArgb(75, 18, 18, 24)
                        : Color.FromArgb(30, 255, 255, 255));

                    PlayPauseButton.Style = (Style)FindResource("PlayPauseFlyoutButtonStyle");
                    PlayPauseButton.Background = _currentPalette.PlayButtonBrush;
                    PlayPauseButton.BorderBrush = _currentPalette.PlayButtonBorderBrush;
                    playPauseIcon.Foreground = _currentPalette.PlayButtonForegroundBrush;

                    var frostedButtonBg = new SolidColorBrush(isDarkMode ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0));
                    var frostedButtonBorder = new SolidColorBrush(isDarkMode ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0));
                    PreviousTrackButton.Appearance = ControlAppearance.Secondary;
                    PreviousTrackButton.Background = frostedButtonBg;
                    PreviousTrackButton.BorderBrush = frostedButtonBorder;
                    NextTrackButton.Appearance = ControlAppearance.Secondary;
                    NextTrackButton.Background = frostedButtonBg;
                    NextTrackButton.BorderBrush = frostedButtonBorder;
                    break;

                case FlyoutThemeSetting.BlurredArtwork:
                    DynamicAmbientLayer.Visibility = Visibility.Collapsed;
                    BlurredArtworkLayer.Visibility = Visibility.Visible;
                    MinimalistGlassLayer.Visibility = Visibility.Collapsed;
                    WindowBackdropType = WindowBackdropType.Acrylic;
                    Root.Background = Brushes.Transparent;

                    BlurredArtworkImage.Source = thumbnail;
                    BlurredArtworkImage.Opacity = isDarkMode ? 0.45 : 0.60;
                    if (thumbnail != null)
                    {
                        BlurredArtworkTintBorder.Background = new SolidColorBrush(isDarkMode
                            ? Color.FromArgb(120, 16, 16, 22)
                            : Color.FromArgb(50, 255, 255, 255));
                    }
                    else
                    {
                        BlurredArtworkTintBorder.Background = new SolidColorBrush(isDarkMode
                            ? Color.FromArgb(120, 20, 20, 25)
                            : Color.FromArgb(50, 245, 245, 250));
                    }

                    ThumbnailDropShadow.Opacity = isDarkMode ? 0.35 : 0.20;
                    ThumbnailDropShadow.BlurRadius = 14;
                    ThumbnailContainer.BorderThickness = new Thickness(0);
                    ThumbnailContainer.BorderBrush = null;

                    _currentPalette = await ColorExtractorService.ExtractPaletteAsync(thumbnail, isDarkMode);

                    PlayPauseButton.Style = (Style)FindResource("PlayPauseFlyoutButtonStyle");
                    PlayPauseButton.Background = _currentPalette.PlayButtonBrush;
                    PlayPauseButton.BorderBrush = _currentPalette.PlayButtonBorderBrush;
                    playPauseIcon.Foreground = _currentPalette.PlayButtonForegroundBrush;

                    var blurPillBg = new SolidColorBrush(isDarkMode ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0));
                    var blurPillBorder = new SolidColorBrush(isDarkMode ? Color.FromArgb(42, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0));
                    PreviousTrackButton.Appearance = ControlAppearance.Secondary;
                    PreviousTrackButton.Background = blurPillBg;
                    PreviousTrackButton.BorderBrush = blurPillBorder;
                    NextTrackButton.Appearance = ControlAppearance.Secondary;
                    NextTrackButton.Background = blurPillBg;
                    NextTrackButton.BorderBrush = blurPillBorder;
                    break;

                case FlyoutThemeSetting.MinimalistGlass:
                    DynamicAmbientLayer.Visibility = Visibility.Collapsed;
                    BlurredArtworkLayer.Visibility = Visibility.Collapsed;
                    MinimalistGlassLayer.Visibility = Visibility.Visible;
                    WindowBackdropType = WindowBackdropType.Acrylic;
                    Root.Background = Brushes.Transparent;

                    MinimalistGlassLayer.Background = new SolidColorBrush(isDarkMode
                        ? Color.FromArgb(125, 22, 22, 28)
                        : Color.FromArgb(50, 255, 255, 255));
                    MinimalistGlassLayer.BorderBrush = isDarkMode
                        ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                    MinimalistGlassLayer.BorderThickness = new Thickness(1);

                    ThumbnailDropShadow.Opacity = isDarkMode ? 0.25 : 0.12;
                    ThumbnailDropShadow.BlurRadius = 10;
                    ThumbnailContainer.BorderThickness = new Thickness(0);
                    ThumbnailContainer.BorderBrush = null;

                    PlayPauseButton.Style = (Style)FindResource("PlayPauseFlyoutButtonStyle");
                    PlayPauseButton.Background = new SolidColorBrush(isDarkMode ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0));
                    PlayPauseButton.BorderBrush = new SolidColorBrush(isDarkMode ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(35, 0, 0, 0));
                    playPauseIcon.Foreground = isDarkMode ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));

                    var glassPillBg = new SolidColorBrush(isDarkMode ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(15, 0, 0, 0));
                    var glassPillBorder = new SolidColorBrush(isDarkMode ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0));
                    PreviousTrackButton.Appearance = ControlAppearance.Secondary;
                    PreviousTrackButton.Background = glassPillBg;
                    PreviousTrackButton.BorderBrush = glassPillBorder;
                    NextTrackButton.Appearance = ControlAppearance.Secondary;
                    NextTrackButton.Background = glassPillBg;
                    NextTrackButton.BorderBrush = glassPillBorder;
                    break;

                case FlyoutThemeSetting.Default:
                default:
                    DynamicAmbientLayer.Visibility = Visibility.Collapsed;
                    BlurredArtworkLayer.Visibility = Visibility.Collapsed;
                    MinimalistGlassLayer.Visibility = Visibility.Collapsed;
                    WindowBackdropType = WindowBackdropType.Mica;
                    Root.ClearValue(System.Windows.Controls.Panel.BackgroundProperty);

                    ThumbnailDropShadow.Opacity = 0;
                    ThumbnailContainer.BorderThickness = new Thickness(0);
                    ThumbnailContainer.BorderBrush = null;

                    PlayPauseButton.ClearValue(StyleProperty);
                    PlayPauseButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                    PlayPauseButton.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                    PlayPauseButton.Appearance = ControlAppearance.Primary;
                    playPauseIcon.ClearValue(System.Windows.Controls.Control.ForegroundProperty);

                    PreviousTrackButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                    PreviousTrackButton.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                    PreviousTrackButton.Appearance = ControlAppearance.Secondary;

                    NextTrackButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                    NextTrackButton.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                    NextTrackButton.Appearance = ControlAppearance.Secondary;
                    break;
            }
        }

        private void PlayPauseButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if ((_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.AmbientDynamic || _appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.BlurredArtwork) && _currentPalette != null)
            {
                PlayPauseButton.Background = _currentPalette.PlayButtonHoverBrush;
                PlayPauseButton.BorderBrush = _currentPalette.PlayButtonHoverBorderBrush;
            }
            else if (_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.MinimalistGlass)
            {
                bool isDark = IsCurrentFlyoutDarkMode();
                PlayPauseButton.Background = isDark ? new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                PlayPauseButton.BorderBrush = isDark ? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
            }
        }

        private void PlayPauseButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if ((_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.AmbientDynamic || _appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.BlurredArtwork) && _currentPalette != null)
            {
                PlayPauseButton.Background = _currentPalette.PlayButtonBrush;
                PlayPauseButton.BorderBrush = _currentPalette.PlayButtonBorderBrush;
            }
            else if (_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.MinimalistGlass)
            {
                bool isDark = IsCurrentFlyoutDarkMode();
                PlayPauseButton.Background = isDark ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                PlayPauseButton.BorderBrush = isDark ? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
            }
        }

        private void PlayPauseButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.AmbientDynamic || _appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.BlurredArtwork) && _currentPalette != null)
            {
                PlayPauseButton.Background = _currentPalette.PlayButtonPressedBrush;
                PlayPauseButton.BorderBrush = _currentPalette.PlayButtonPressedBorderBrush;
            }
            else if (_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.MinimalistGlass)
            {
                bool isDark = IsCurrentFlyoutDarkMode();
                PlayPauseButton.Background = isDark ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(15, 0, 0, 0));
                PlayPauseButton.BorderBrush = isDark ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
            }
        }

        private void PlayPauseButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if ((_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.AmbientDynamic || _appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.BlurredArtwork) && _currentPalette != null)
            {
                PlayPauseButton.Background = PlayPauseButton.IsMouseOver ? _currentPalette.PlayButtonHoverBrush : _currentPalette.PlayButtonBrush;
                PlayPauseButton.BorderBrush = PlayPauseButton.IsMouseOver ? _currentPalette.PlayButtonHoverBorderBrush : _currentPalette.PlayButtonBorderBrush;
            }
            else if (_appSettings.Theme.FlyoutTheme == FlyoutThemeSetting.MinimalistGlass)
            {
                bool isDark = IsCurrentFlyoutDarkMode();
                PlayPauseButton.Background = PlayPauseButton.IsMouseOver
                    ? (isDark ? new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)))
                    : (isDark ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)));
                PlayPauseButton.BorderBrush = PlayPauseButton.IsMouseOver
                    ? (isDark ? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)))
                    : (isDark ? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)));
            }
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