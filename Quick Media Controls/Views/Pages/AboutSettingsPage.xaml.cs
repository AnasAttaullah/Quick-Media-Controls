using System;
using System.Reflection;
using System.Windows.Controls;

namespace Quick_Media_Controls.Views.Pages
{
    /// <summary>
    /// Interaction logic for AboutSettingsPage.xaml
    /// </summary>
    public partial class AboutSettingsPage : Page
    {
        public string AppVersion { get; }

        public AboutSettingsPage()
        {
            AppVersion = GetAppVersion();
            InitializeComponent();
            DataContext = this;
        }

        private static string GetAppVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var plusIndex = informationalVersion.IndexOf('+');
                return plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
            }

            return assembly.GetName().Version?.ToString(3) ?? "Unknown";
        }
    }
}
