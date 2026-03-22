using Quick_Media_Controls.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Quick_Media_Controls.Views.Pages
{
    /// <summary>
    /// Interaction logic for GeneralSettingsPage.xaml
    /// </summary>
    public partial class GeneralSettingsPage : Page
    {
        private SettingsWindow? _settingWindows;
        private GeneralSettings _generalSettings = GeneralSettings.CreateDefault();
        private bool _isBinding;
        public GeneralSettingsPage()
        {
            InitializeComponent();
            Loaded += GeneralSettingsPage_Loaded;
        }

        private void GeneralSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settingWindows = Window.GetWindow(this) as SettingsWindow;
            if (_settingWindows is null) return;

            _generalSettings = _settingWindows.DraftSettings.General.Clone();
            BindToggles();
        }

        private void BindToggles()
        {
            _isBinding = true;

            RunAtStartupToggle.IsChecked = _generalSettings.RunAtStartup;
            CheckForUpdatesOnStartupToggle.IsChecked = _generalSettings.CheckForUpdatesOnStartup;
            AutoHideFlyoutToggle.IsChecked = _generalSettings.AutoHideFlyout;
            MoveFlyoutByDefaultToggle.IsChecked = _generalSettings.MoveFlyoutByDefault;
            EnableFlyoutAnimationsToggle.IsChecked = _generalSettings.EnableFlyoutAnimations;

            _isBinding = false;
        }

        private void GeneralSettingToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_settingWindows is null || _isBinding) return;

            _generalSettings.RunAtStartup = RunAtStartupToggle.IsChecked ?? false;
            _generalSettings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartupToggle.IsChecked ?? false;
            _generalSettings.AutoHideFlyout = AutoHideFlyoutToggle.IsChecked ?? false;
            _generalSettings.MoveFlyoutByDefault = MoveFlyoutByDefaultToggle.IsChecked ?? false;
            _generalSettings.EnableFlyoutAnimations = EnableFlyoutAnimationsToggle.IsChecked ?? false;

            _settingWindows.SetDraftGeneralSettings(_generalSettings);
        }

        private void CheckForUpdatesNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.CheckForUpdatesNow();
            }
        }
    }
}
