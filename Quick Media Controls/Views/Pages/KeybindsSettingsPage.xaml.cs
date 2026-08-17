using Quick_Media_Controls.Models;
using Quick_Media_Controls.Services;
using System.Diagnostics;
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

            var textBoxes = new[]
            {
                PlayPauseHotkeyTextBox, PlayPauseSecondaryHotkeyTextBox,
                NextTrackHotkeyTextBox, NextTrackSecondaryHotkeyTextBox,
                PreviousTrackHotkeyTextBox, PreviousTrackSecondaryHotkeyTextBox,
                OpenFlyoutHotkeyTextBox, OpenFlyoutSecondaryHotkeyTextBox
            };

            foreach (var tb in textBoxes)
            {
                tb.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
                tb.PreviewMouseDown += HotkeyTextBox_PreviewMouseDown;
                tb.GotFocus += HotkeyTextBox_GotFocus;
                tb.LostFocus += HotkeyTextBox_LostFocus;
            }
        }

        private void KeybindsSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (_settingsWindow is null) return;

            _keybindsSettings = _settingsWindow.DraftSettings.Keybinds.Clone();
            _keybindsSettings.Shortcuts ??= ShortcutSettings.CreateDefault();
            _keybindsSettings.TrayIconShortcuts ??= TrayIconShortcutSettings.CreateDefault();

            BindKeyboardShortcutText();
            BindMouseShortcutSelections();
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearHotkeyValidationMessage();
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ClearHotkeyValidationMessage();
        }

        private void ClearHotkeyValidationMessage()
        {
            HotkeyValidationTextBlock.Text = string.Empty;
            HotkeyValidationTextBlock.Visibility = Visibility.Collapsed;
        }

        private void ShowHotkeyValidationMessage(string message)
        {
            HotkeyValidationTextBlock.Text = message;
            HotkeyValidationTextBlock.Visibility = Visibility.Visible;
        }

        private void BindKeyboardShortcutText()
        {
            var keyboard = _keybindsSettings.Shortcuts;

            PlayPauseHotkeyTextBox.Text = keyboard.PlayPause?.ToDisplayString() ?? string.Empty;
            PlayPauseSecondaryHotkeyTextBox.Text = keyboard.PlayPauseSecondary?.ToDisplayString() ?? string.Empty;

            NextTrackHotkeyTextBox.Text = keyboard.NextTrack?.ToDisplayString() ?? string.Empty;
            NextTrackSecondaryHotkeyTextBox.Text = keyboard.NextTrackSecondary?.ToDisplayString() ?? string.Empty;

            PreviousTrackHotkeyTextBox.Text = keyboard.PreviousTrack?.ToDisplayString() ?? string.Empty;
            PreviousTrackSecondaryHotkeyTextBox.Text = keyboard.PreviousTrackSecondary?.ToDisplayString() ?? string.Empty;

            OpenFlyoutHotkeyTextBox.Text = keyboard.OpenFlyout?.ToDisplayString() ?? string.Empty;
            OpenFlyoutSecondaryHotkeyTextBox.Text = keyboard.OpenFlyoutSecondary?.ToDisplayString() ?? string.Empty;
        }

        private void BindMouseShortcutSelections()
        {
            _isUpdatingMouseComboSelection = true;
            try
            {
                var mouse = _keybindsSettings.TrayIconShortcuts;
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

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var keyboard = _keybindsSettings.Shortcuts;

            if ((e.Key is Key.Back or Key.Delete) && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (sender == PlayPauseSecondaryHotkeyTextBox) keyboard.PlayPauseSecondary = null;
                else if (sender == NextTrackSecondaryHotkeyTextBox) keyboard.NextTrackSecondary = null;
                else if (sender == PreviousTrackSecondaryHotkeyTextBox) keyboard.PreviousTrackSecondary = null;
                else if (sender == OpenFlyoutSecondaryHotkeyTextBox) keyboard.OpenFlyoutSecondary = null;

                ClearHotkeyValidationMessage();
                BindKeyboardShortcutText();
                _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
                return;
            }

            var rawKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (rawKey is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            {
                return;
            }

            if (!HotkeyGesture.TryFromKeyEvent(e, out var gesture) || gesture is null)
            {
                ShowHotkeyValidationMessage("Invalid hotkey. Use at least one modifier key (Ctrl/Alt/Shift/Win) + a non-modifier key.");
                return;
            }

            ClearHotkeyValidationMessage();

            if (sender == PlayPauseHotkeyTextBox) keyboard.PlayPause = gesture;
            else if (sender == PlayPauseSecondaryHotkeyTextBox) keyboard.PlayPauseSecondary = gesture;
            else if (sender == NextTrackHotkeyTextBox) keyboard.NextTrack = gesture;
            else if (sender == NextTrackSecondaryHotkeyTextBox) keyboard.NextTrackSecondary = gesture;
            else if (sender == PreviousTrackHotkeyTextBox) keyboard.PreviousTrack = gesture;
            else if (sender == PreviousTrackSecondaryHotkeyTextBox) keyboard.PreviousTrackSecondary = gesture;
            else if (sender == OpenFlyoutHotkeyTextBox) keyboard.OpenFlyout = gesture;
            else if (sender == OpenFlyoutSecondaryHotkeyTextBox) keyboard.OpenFlyoutSecondary = gesture;

            BindKeyboardShortcutText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }

        private void HotkeyTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var modifiers = ModifierStateService.GetModifiers();
            if (modifiers == ModifierKeys.None)
            {
                return;
            }

            e.Handled = true;

            if (!HotkeyGesture.TryFromMouseEvent(e, out var gesture) || gesture is null)
            {
                ShowHotkeyValidationMessage("Invalid hotkey. Use at least one modifier key (Ctrl/Alt/Shift/Win) + a non-modifier key or mouse button.");
                return;
            }

            ClearHotkeyValidationMessage();

            var keyboard = _keybindsSettings.Shortcuts;

            if (sender == PlayPauseHotkeyTextBox) keyboard.PlayPause = gesture;
            else if (sender == PlayPauseSecondaryHotkeyTextBox) keyboard.PlayPauseSecondary = gesture;
            else if (sender == NextTrackHotkeyTextBox) keyboard.NextTrack = gesture;
            else if (sender == NextTrackSecondaryHotkeyTextBox) keyboard.NextTrackSecondary = gesture;
            else if (sender == PreviousTrackHotkeyTextBox) keyboard.PreviousTrack = gesture;
            else if (sender == PreviousTrackSecondaryHotkeyTextBox) keyboard.PreviousTrackSecondary = gesture;
            else if (sender == OpenFlyoutHotkeyTextBox) keyboard.OpenFlyout = gesture;
            else if (sender == OpenFlyoutSecondaryHotkeyTextBox) keyboard.OpenFlyoutSecondary = gesture;

            BindKeyboardShortcutText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
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

        private void ResetPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.Shortcuts.PlayPause = _defaultKeybinds.Shortcuts.PlayPause.Clone();
            _keybindsSettings.Shortcuts.PlayPauseSecondary = _defaultKeybinds.Shortcuts.PlayPauseSecondary?.Clone();
            ResetAndSync();
        }

        private void ResetNextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.Shortcuts.NextTrack = _defaultKeybinds.Shortcuts.NextTrack.Clone();
            _keybindsSettings.Shortcuts.NextTrackSecondary = _defaultKeybinds.Shortcuts.NextTrackSecondary?.Clone();
            ResetAndSync();
        }

        private void ResetPreviousTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.Shortcuts.PreviousTrack = _defaultKeybinds.Shortcuts.PreviousTrack.Clone();
            _keybindsSettings.Shortcuts.PreviousTrackSecondary = _defaultKeybinds.Shortcuts.PreviousTrackSecondary?.Clone();
            ResetAndSync();
        }

        private void ResetOpenFlyoutButton_Click(object sender, RoutedEventArgs e)
        {
            _keybindsSettings.Shortcuts.OpenFlyout = _defaultKeybinds.Shortcuts.OpenFlyout.Clone();
            _keybindsSettings.Shortcuts.OpenFlyoutSecondary = _defaultKeybinds.Shortcuts.OpenFlyoutSecondary?.Clone();
            ResetAndSync();
        }

        private void ResetMouseShortcutsButton_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingMouseComboSelection = true;
            try
            {
                _keybindsSettings.TrayIconShortcuts = _defaultKeybinds.TrayIconShortcuts.Clone();

                var defaultMouseShortcut = _keybindsSettings.TrayIconShortcuts;
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

        private ShortcutAction? GetMappedAction(ComboBox comboBox)
        {
            var mouse = _keybindsSettings.TrayIconShortcuts;

            if (comboBox == LeftClickActionComboBox) return mouse.LeftClick;
            if (comboBox == DoubleLeftClickActionComboBox) return mouse.DoubleLeftClick;
            if (comboBox == RightClickActionComboBox) return mouse.RightClick;
            return mouse.MiddleClick;
        }

        private void SetMappedAction(ComboBox comboBox, ShortcutAction? action)
        {
            var mouse = _keybindsSettings.TrayIconShortcuts;

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
            var mouse = _keybindsSettings.TrayIconShortcuts;

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

        private void ResetAndSync()
        {
            HotkeyValidationTextBlock.Text = string.Empty;
            HotkeyValidationTextBlock.Visibility = Visibility.Collapsed;

            BindKeyboardShortcutText();
            _settingsWindow?.SetDraftKeybinds(_keybindsSettings);
        }
    }
}

