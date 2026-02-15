using Media_Control_Tray_Icon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.Media.Control;
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
        public MediaFlyout(MediaSessionService sessionManager)
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            _sessionManager = sessionManager;
            Left = SystemParameters.WorkArea.Right - 300 - 110;
            Top = SystemParameters.WorkArea.Bottom - 130;
            InitializeComponent();
            UpdateIcon();
        }

        public void UpdateIcon()
        {
            if(_sessionManager.CurrentSession == null)
            {
                //PlaybackButtonsGrid.IsEnabled = false;
                return;
            }
            playPauseIcon.Symbol = (_sessionManager.IsPlaying()) ? SymbolRegular.Pause12 : SymbolRegular.Play12;
        }



        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.ButtonState == MouseButtonState.Pressed)
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
    }
}
