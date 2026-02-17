using Media_Control_Tray_Icon.Services;
using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Media_Control_Tray_Icon
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
                //PlaybackButtonsGrid.IsEnabled = false;
                PlaybackButtonsGrid.Visibility = Visibility.Collapsed;
                return;
            }
            if (PlaybackButtonsGrid.Visibility != Visibility.Visible)
            {
                PlaybackButtonsGrid.Visibility = Visibility.Visible;
            }
            playPauseIcon.Symbol = (_sessionManager.IsPlaying()) ? SymbolRegular.Pause12 : SymbolRegular.Play12;
        }

        public void UpdateMediaInfo()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateMediaInfo);
                return;
            }
            if (_sessionManager.CurrentMediaProperties != null)
            {
                var mediaTitle = _sessionManager.CurrentMediaProperties.Title;
                playingMediaTitle.Text = (mediaTitle.Length > 35) ? mediaTitle.Substring(0, 32) + "..." : mediaTitle;
                playingMediaArtist.Text = _sessionManager.CurrentMediaProperties.Artist;
                //playingMediaThumbnail.Source = _sessionManager.CurrentMediaThumbnail;
            }
            else
            {
                playingMediaTitle.Text = "No media playing";
                playingMediaArtist.Text = "";
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
        }

    }
}