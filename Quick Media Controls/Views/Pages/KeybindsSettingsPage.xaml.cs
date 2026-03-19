using Quick_Media_Controls.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Quick_Media_Controls.Views.Pages
{
    public partial class KeybindsSettingsPage : Page
    {
        private static readonly KeybindSettings _defaultKeybinds = KeybindSettings.CreateDefault();

        private SettingsWindow? _settingsWindow;
        private KeybindSettings _keybindsSettings = KeybindSettings.CreateDefault();

        public KeybindsSettingsPage()
        {
            InitializeComponent();

            Loaded += KeybindsSettingsPage_Loaded;

            PlayPauseHotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
            NextTrackHotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
            PreviousTrackHotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
            OpenFlyoutHotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        }

        private void KeybindsSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (_settingsWindow is null) return;

            _keybindsSettings = _settingsWindow.DraftSettings.Keybinds.Clone();
            BindText();
        }

        private void BindText()
        {
            PlayPauseHotkeyTextBox.Text = _keybindsSettings.PlayPause.ToDisplayString();
            NextTrackHotkeyTextBox.Text = _keybindsSettings.NextTrack.ToDisplayString();
            PreviousTrackHotkeyTextBox.Text = _keybindsSettings.PreviousTrack.ToDisplayString();
            OpenFlyoutHotkeyTextBox.Text = _keybindsSettings.OpenFlyout.ToDisplayString();
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (!HotkeyGesture.TryFromKeyEvent(e, out var gesture) || gesture is null)
            {
                HotkeyValidationTextBlock.Text = "Invalid hotkey. Use at least one modifier key (Ctrl/Alt/Shift/Win) + a non-modifier key.";
                HotkeyValidationTextBlock.Visibility = Visibility.Visible;
                return;
            }

            HotkeyValidationTextBlock.Text = string.Empty;
            HotkeyValidationTextBlock.Visibility = Visibility.Collapsed;

            if (sender == PlayPauseHotkeyTextBox)
                _keybindsSettings.PlayPause = gesture;
            else if (sender == NextTrackHotkeyTextBox)
                _keybindsSettings.NextTrack = gesture;
            else if (sender == PreviousTrackHotkeyTextBox)
                _keybindsSettings.PreviousTrack = gesture;
            else if (sender == OpenFlyoutHotkeyTextBox)
                _keybindsSettings.OpenFlyout = gesture;

            BindText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }

        private void ResetPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.PlayPause = _defaultKeybinds.PlayPause.Clone();
            ResetAndSync();
        }

        private void ResetNextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.NextTrack = _defaultKeybinds.NextTrack.Clone();
            ResetAndSync();
        }

        private void ResetPreviousTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.PreviousTrack = _defaultKeybinds.PreviousTrack.Clone();
            ResetAndSync();
        }
        private void ResetOpenFlyoutButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.OpenFlyout = _defaultKeybinds.OpenFlyout.Clone();
            ResetAndSync();
        }

        private void ResetAndSync()
        {
            HotkeyValidationTextBlock.Text = string.Empty;
            HotkeyValidationTextBlock.Visibility = Visibility.Collapsed;

            BindText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }
    }
}

