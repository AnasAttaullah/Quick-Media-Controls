using Quick_Media_Controls.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Quick_Media_Controls
{
    /// <summary>
    /// Interaction logic for MediaFlyout.xaml
    /// </summary>
    public partial class MediaFlyout : FluentWindow
    {
        private readonly MediaSessionService _sessionManager;
        private bool _IsDragEnabled;

        public MediaFlyout(MediaSessionService sessionManager)
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            _IsDragEnabled = false;
            _sessionManager = sessionManager;
            Left = SystemParameters.WorkArea.Right - 300 - 110;
            Top = SystemParameters.WorkArea.Bottom - 130;
            InitializeComponent();
            UpdateIcon();
            UpdateMediaInfo();
            showFlyout();
        }

        public void UpdateIcon()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateIcon);
                return;
            }
            if (_sessionManager.CurrentSession == null)
            {
                PlaybackButtonsGrid.Visibility = Visibility.Collapsed;
                return;
            }
            if (PlaybackButtonsGrid.Visibility != Visibility.Visible)
            {
                PlaybackButtonsGrid.Visibility = Visibility.Visible;
            }
            playPauseIcon.Symbol = (_sessionManager.IsPlaying()) ? SymbolRegular.Pause12 : SymbolRegular.Play12;
        }

        public async void UpdateMediaInfo()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateMediaInfo);
                return;
            }
            if (_sessionManager.CurrentMediaProperties != null)
            {
                if (mediaPlayingGrid.Visibility != Visibility.Visible)
                {
                    mediaPlayingGrid.Visibility = Visibility.Visible;
                    noMediaPlayingGrid.Visibility = Visibility.Collapsed;
                }
                var mediaTitle = _sessionManager.CurrentMediaProperties.Title;
                playingMediaTitle.Text = (mediaTitle.Length > 35) ? mediaTitle.Substring(0, 32) + "..." : mediaTitle;
                playingMediaArtist.Text = _sessionManager.CurrentMediaProperties.Artist;

                // Update thumbnail
                var thumbnail = await LoadMediaThumbnailAsync(_sessionManager.CurrentMediaProperties.Thumbnail);
                playingMediaThumbnail.Source = thumbnail;
            }
            else
            {
                mediaPlayingGrid.Visibility = Visibility.Collapsed;
                noMediaPlayingGrid.Visibility = Visibility.Visible;
                playingMediaTitle.Text = "It's a bit quiet in here...";
                playingMediaArtist.Text = "Time to break the silence";
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

                // Copy to memory stream to avoid stream disposal issues
                using var memStream = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(memStream);
                memStream.Position = 0;

                bitmap.StreamSource = memStream;
                bitmap.EndInit();
                bitmap.Freeze(); // Important for cross-thread usage

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load thumbnail: {ex.Message}");
                return null;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.ButtonState == MouseButtonState.Pressed && _IsDragEnabled)
                DragMove();
        }

        private async void PreviousButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.SkipPreviousAsync();
        }

        private async void PlayPauseButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.TogglePlayPauseAsync();
        }

        private async void NextButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await _sessionManager.SkipNextAsync();
        }

        private void Flyout_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        internal void showFlyout()
        {
            UpdateMediaInfo();
            // Make visible first
            this.Visibility = Visibility.Visible;

            // Temporarily toggle Topmost to force Windows to treat it as foreground
            this.Topmost = true;
            this.Topmost = false;
            this.Topmost = true;

            // Activate + Focus
            this.Activate();
            this.Focus();

            Keyboard.Focus(this);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MoveWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _IsDragEnabled = _IsDragEnabled ? false : true;
            this.Cursor = _IsDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
        }
    }
}