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
        private bool _isUpdatingMouseComboSelection;

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
            _keybindsSettings.KeyboardShortcuts ??= KeyboardShortcutSettings.CreateDefault();
            _keybindsSettings.MouseShortcuts ??= MouseShortcutSettings.CreateDefault();

            BindKeyboardShortcutText();
            BindMouseShortcutSelections();
        }

        private void BindKeyboardShortcutText()
        {
            var keyboard = _keybindsSettings.KeyboardShortcuts;

            PlayPauseHotkeyTextBox.Text = keyboard.PlayPause.ToDisplayString();
            NextTrackHotkeyTextBox.Text = keyboard.NextTrack.ToDisplayString();
            PreviousTrackHotkeyTextBox.Text = keyboard.PreviousTrack.ToDisplayString();
            OpenFlyoutHotkeyTextBox.Text = keyboard.OpenFlyout.ToDisplayString();
        }

        private void BindMouseShortcutSelections()
        {
            _isUpdatingMouseComboSelection = true;
            try
            {
                var mouse = _keybindsSettings.MouseShortcuts;
                SetComboSelection(LeftClickActionComboBox, mouse.LeftClick);
                SetComboSelection(DoubleLeftClickActionComboBox, mouse.DoubleLeftClick);
                SetComboSelection(RightClickActionComboBox, mouse.RightClick);
                SetComboSelection(MiddleClickActionComboBox, mouse.MiddleClick);
            }
            finally
            {
                _isUpdatingMouseComboSelection = false;
            }

            ClearMouseValidationMessage();
        }

        private static void SetComboSelection(ComboBox comboBox, ShortcutAction? action)
        {
            foreach (var item in comboBox.Items)
            {
                if (item is not ComboBoxItem comboBoxItem)
                    continue;

                if (!action.HasValue && comboBoxItem.Tag is null)
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }

                if (comboBoxItem.Tag is ShortcutAction itemAction && action.HasValue && itemAction == action.Value)
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }

        private static ShortcutAction? GetSelectedAction(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem { Tag: ShortcutAction action })
            {
                return action;
            }

            return null;
        }

        private void MouseShortcutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingMouseComboSelection || _settingsWindow is null || sender is not ComboBox changedComboBox)
            {
                return;
            }

            var proposedAction = GetSelectedAction(changedComboBox);
            var previousAction = GetMappedAction(changedComboBox);

            if (HasDuplicateAction(changedComboBox, proposedAction))
            {
                ShowMouseValidationMessage("That action is already assigned. Each mouse action must be unique.");
                RevertComboSelection(changedComboBox, previousAction);
                return;
            }

            SetMappedAction(changedComboBox, proposedAction);
            ClearMouseValidationMessage();
            _settingsWindow.SetDraftKeybinds(_keybindsSettings);
        }

        private ShortcutAction? GetMappedAction(ComboBox comboBox)
        {
            var mouse = _keybindsSettings.MouseShortcuts;

            if (comboBox == LeftClickActionComboBox) return mouse.LeftClick;
            if (comboBox == DoubleLeftClickActionComboBox) return mouse.DoubleLeftClick;
            if (comboBox == RightClickActionComboBox) return mouse.RightClick;
            return mouse.MiddleClick;
        }

        private void SetMappedAction(ComboBox comboBox, ShortcutAction? action)
        {
            var mouse = _keybindsSettings.MouseShortcuts;

            if (comboBox == LeftClickActionComboBox) mouse.LeftClick = action;
            else if (comboBox == DoubleLeftClickActionComboBox) mouse.DoubleLeftClick = action;
            else if (comboBox == RightClickActionComboBox) mouse.RightClick = action;
            else mouse.MiddleClick = action;
        }

        private bool HasDuplicateAction(ComboBox changedComboBox, ShortcutAction? proposedAction)
        {
            if (!proposedAction.HasValue)
                return false;

            var action = proposedAction.Value;
            var mouse = _keybindsSettings.MouseShortcuts;

            if (changedComboBox != LeftClickActionComboBox && mouse.LeftClick == action) return true;
            if (changedComboBox != DoubleLeftClickActionComboBox && mouse.DoubleLeftClick == action) return true;
            if (changedComboBox != RightClickActionComboBox && mouse.RightClick == action) return true;
            if (changedComboBox != MiddleClickActionComboBox && mouse.MiddleClick == action) return true;

            return false;
        }

        private void RevertComboSelection(ComboBox comboBox, ShortcutAction? previousAction)
        {
            _isUpdatingMouseComboSelection = true;
            try
            {
                SetComboSelection(comboBox, previousAction);
            }
            finally
            {
                _isUpdatingMouseComboSelection = false;
            }
        }

        private void ShowMouseValidationMessage(string message)
        {
            MouseShortcutValidationTextBlock.Text = message;
            MouseShortcutValidationTextBlock.Visibility = Visibility.Visible;
        }

        private void ClearMouseValidationMessage()
        {
            MouseShortcutValidationTextBlock.Text = string.Empty;
            MouseShortcutValidationTextBlock.Visibility = Visibility.Collapsed;
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

            var keyboard = _keybindsSettings.KeyboardShortcuts;

            if (sender == PlayPauseHotkeyTextBox)
                keyboard.PlayPause = gesture;
            else if (sender == NextTrackHotkeyTextBox)
                keyboard.NextTrack = gesture;
            else if (sender == PreviousTrackHotkeyTextBox)
                keyboard.PreviousTrack = gesture;
            else if (sender == OpenFlyoutHotkeyTextBox)
                keyboard.OpenFlyout = gesture;

            BindKeyboardShortcutText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }

        private void ResetPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.KeyboardShortcuts.PlayPause = _defaultKeybinds.KeyboardShortcuts.PlayPause.Clone();
            ResetAndSync();
        }

        private void ResetNextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.KeyboardShortcuts.NextTrack = _defaultKeybinds.KeyboardShortcuts.NextTrack.Clone();
            ResetAndSync();
        }

        private void ResetPreviousTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.KeyboardShortcuts.PreviousTrack = _defaultKeybinds.KeyboardShortcuts.PreviousTrack.Clone();
            ResetAndSync();
        }

        private void ResetMouseShortcutsButton_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingMouseComboSelection = true;
            try
            {
                _keybindsSettings.MouseShortcuts = _defaultKeybinds.MouseShortcuts.Clone();

                var defaultMouseShortcut = _keybindsSettings.MouseShortcuts;
                SetComboSelection(LeftClickActionComboBox, defaultMouseShortcut.LeftClick);
                SetComboSelection(DoubleLeftClickActionComboBox, defaultMouseShortcut.DoubleLeftClick);
                SetComboSelection(RightClickActionComboBox, defaultMouseShortcut.RightClick);
                SetComboSelection(MiddleClickActionComboBox, defaultMouseShortcut.MiddleClick);
            }
            finally
            {
                _isUpdatingMouseComboSelection = false;
            }

            ClearMouseValidationMessage();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }

        private void ResetOpenFlyoutButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.KeyboardShortcuts.OpenFlyout = _defaultKeybinds.KeyboardShortcuts.OpenFlyout.Clone();
            ResetAndSync();
        }

        private void ResetAndSync()
        {
            HotkeyValidationTextBlock.Text = string.Empty;
            HotkeyValidationTextBlock.Visibility = Visibility.Collapsed;

            BindKeyboardShortcutText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }

    }
}

