using Quick_Media_Controls.Models;
using Quick_Media_Controls.Views.Pages;
using System;
using System.Windows;
using Wpf.Ui.Controls;
namespace Quick_Media_Controls
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly App _app;
        private readonly Snackbar _snackbar;

        public AppSettings DraftSettings { get; private set; }

        public SettingsWindow()
        {
            InitializeComponent();

            _app = (App)Application.Current;
            DraftSettings = _app.GetSettingsSnapshot();
            _snackbar = new Snackbar(SnackbarPresenter);
        }

        public void SetDraftKeybinds(KeybindSettings keybinds)
        {
            DraftSettings.Keybinds = keybinds.Clone();
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsNavigation.Navigate(typeof(Home));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_app.TrySaveSettings(DraftSettings, out var error))
            {
                ShowSnackbar(
                    "Settings saved",
                    "Your changes were applied successfully.",
                    ControlAppearance.Secondary,
                    SymbolRegular.CheckmarkCircle20);

                return;
            }

            ShowSnackbar(
                "Save failed",
                error ?? "Failed to save settings.",
                ControlAppearance.Danger,
                SymbolRegular.DismissCircle20);
        }

        private void ShowSnackbar(string title, string message, ControlAppearance appearance, SymbolRegular icon)
        {
            _snackbar.Title = title;
            _snackbar.Content = message;
            _snackbar.Appearance = appearance;
            _snackbar.Icon = new SymbolIcon(icon , FontSize = 32);
            _snackbar.Timeout = TimeSpan.FromSeconds(5);
            _snackbar.Show(true);
        }
    }
}
