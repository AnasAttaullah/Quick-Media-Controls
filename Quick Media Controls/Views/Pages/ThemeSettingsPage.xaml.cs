using Quick_Media_Controls.Models;
using System.Windows;
using System.Windows.Controls;

namespace Quick_Media_Controls.Views.Pages
{
    /// <summary>
    /// Interaction logic for ThemeSettingsPage.xaml
    /// </summary>
    public partial class ThemeSettingsPage : Page
    {
        private SettingsWindow? _settingWindows;
        private ThemeSettings _themeSettings = ThemeSettings.CreateDefault();
        private bool _isBinding;

        public ThemeSettingsPage()
        {
            InitializeComponent();
            Loaded += ThemeSettingsPage_Loaded;
        }

        private void ThemeSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settingWindows = Window.GetWindow(this) as SettingsWindow;
            if (_settingWindows is null) return;

            _themeSettings = _settingWindows.DraftSettings.Theme.Clone();
            BindControls();
        }

        private void BindControls()
        {
            _isBinding = true;

            switch (_themeSettings.AppTheme)
            {
                case ApplicationThemeSetting.Light:
                    AppThemeComboBox.SelectedIndex = 1;
                    break;
                case ApplicationThemeSetting.Dark:
                    AppThemeComboBox.SelectedIndex = 2;
                    break;
                case ApplicationThemeSetting.System:
                default:
                    AppThemeComboBox.SelectedIndex = 0;
                    break;
            }

            switch (_themeSettings.TrayIconTheme)
            {
                case TrayIconThemeSetting.Light:
                    TrayIconThemeComboBox.SelectedIndex = 1;
                    break;
                case TrayIconThemeSetting.Dark:
                    TrayIconThemeComboBox.SelectedIndex = 2;
                    break;
                case TrayIconThemeSetting.System:
                default:
                    TrayIconThemeComboBox.SelectedIndex = 0;
                    break;
            }

            _isBinding = false;
        }

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingWindows is null || _isBinding) return;

            _themeSettings.AppTheme = AppThemeComboBox.SelectedIndex switch
            {
                1 => ApplicationThemeSetting.Light,
                2 => ApplicationThemeSetting.Dark,
                _ => ApplicationThemeSetting.System
            };

            _settingWindows.SetDraftThemeSettings(_themeSettings);
        }

        private void TrayIconThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingWindows is null || _isBinding) return;

            _themeSettings.TrayIconTheme = TrayIconThemeComboBox.SelectedIndex switch
            {
                1 => TrayIconThemeSetting.Light,
                2 => TrayIconThemeSetting.Dark,
                _ => TrayIconThemeSetting.System
            };

            _settingWindows.SetDraftThemeSettings(_themeSettings);
        }
    }
}
