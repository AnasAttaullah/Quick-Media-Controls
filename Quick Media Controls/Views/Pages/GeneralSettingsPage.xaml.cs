using AutoUpdaterDotNET;
using Quick_Media_Controls.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

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

            if (Application.Current is App app && app.IsPackagedDistribution)
            {
                CheckForUpdatesNowButton.Visibility = Visibility.Collapsed;
            }
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

        private async void CheckForUpdatesNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is not App app) return;

            CheckForUpdatesNowButton.IsEnabled = false;
            CheckForUpdatesNowButton.Content = "Checking for update...";

            var tcs = new TaskCompletionSource<UpdateInfoEventArgs?>();
            void OnCheckForUpdate(UpdateInfoEventArgs args)
            {
                AutoUpdater.CheckForUpdateEvent -= OnCheckForUpdate;
                tcs.TrySetResult(args);
            }

            AutoUpdater.CheckForUpdateEvent += OnCheckForUpdate;

            try
            {
                app.CheckForUpdatesNow();

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(15000));
                if (completedTask == tcs.Task)
                {
                    var args = await tcs.Task;
                    if (args is null)
                    {
                        _settingWindows?.ShowSnackbar(
                            "Update Check Failed",
                            "No response received from update server.",
                            ControlAppearance.Danger,
                            SymbolRegular.Warning20);
                    }
                    else if (args.Error != null)
                    {
                        _settingWindows?.ShowSnackbar(
                            "Update Check Failed",
                            args.Error.Message,
                            ControlAppearance.Danger,
                            SymbolRegular.DismissCircle20);
                    }
                    else if (args.IsUpdateAvailable)
                    {
                        _settingWindows?.ShowSnackbar(
                            "Update Available",
                            $"Version {args.CurrentVersion} is available for download.",
                            ControlAppearance.Secondary,
                            SymbolRegular.ArrowDownload20);

                        AutoUpdater.ShowUpdateForm(args);
                    }
                    else
                    {
                        _settingWindows?.ShowSnackbar(
                            "You're Up to Date",
                            "You are running the latest version of Quick Media Controls.",
                            ControlAppearance.Secondary,
                            SymbolRegular.CheckmarkCircle20);
                    }
                }
                else
                {
                    AutoUpdater.CheckForUpdateEvent -= OnCheckForUpdate;
                    _settingWindows?.ShowSnackbar(
                        "Update Check Timeout",
                        "The update check timed out. Please check your internet connection.",
                        ControlAppearance.Danger,
                        SymbolRegular.Warning20);
                }
            }
            catch (Exception ex)
            {
                AutoUpdater.CheckForUpdateEvent -= OnCheckForUpdate;
                _settingWindows?.ShowSnackbar(
                    "Update Check Failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    SymbolRegular.DismissCircle20);
            }
            finally
            {
                CheckForUpdatesNowButton.IsEnabled = true;
                CheckForUpdatesNowButton.Content = "Check for Update Now";
            }
        }
    }
}
