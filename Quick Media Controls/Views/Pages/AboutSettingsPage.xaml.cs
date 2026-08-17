using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Quick_Media_Controls.Views.Pages
{
    /// <summary>
    /// Interaction logic for AboutSettingsPage.xaml
    /// </summary>
    public partial class AboutSettingsPage : Page
    {
        private SettingsWindow? _settingWindows;

        public string AppVersion { get; }
        public string AppVersionDisplay => $"v{AppVersion}";
        public string OsVersion { get; }
        public string RuntimeVersion { get; }
        public string Architecture { get; }
        public string DistributionChannel { get; }
        public string LicenseName => "GNU GPL v3.0";
        public string AuthorName => "Anas Attaullah";

        public AboutSettingsPage()
        {
            AppVersion = GetAppVersion();
            OsVersion = GetOsDisplayName();
            RuntimeVersion = RuntimeInformation.FrameworkDescription;
            Architecture = RuntimeInformation.ProcessArchitecture.ToString();
            DistributionChannel = GetDistributionChannel();

            InitializeComponent();
            DataContext = this;
            Loaded += AboutSettingsPage_Loaded;
        }

        private void AboutSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settingWindows = Window.GetWindow(this) as SettingsWindow;
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

        private static string GetOsDisplayName()
        {
            var build = Environment.OSVersion.Version.Build;
            var osName = build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{osName} (Build {build})";
        }

        private static string GetDistributionChannel()
        {
            if (Application.Current is App app && app.IsPackagedDistribution)
            {
                return "Microsoft Store (Packaged)";
            }

            return "Standalone (Unpackaged)";
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void AuthorTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenUrl("https://github.com/AnasAttaullah");
        }

        private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("### Environment & Diagnostics");
            diagnostics.AppendLine($"- **App Version**: {AppVersion}");
            diagnostics.AppendLine($"- **Operating System**: {OsVersion}");
            diagnostics.AppendLine($"- **Architecture**: {Architecture}");
            diagnostics.AppendLine($"- **Runtime**: {RuntimeVersion}");
            diagnostics.AppendLine($"- **Distribution Channel**: {DistributionChannel}");

            try
            {
                Clipboard.SetDataObject(diagnostics.ToString().TrimEnd(), true);
                _settingWindows?.ShowSnackbar(
                    "Copied to Clipboard",
                    "Diagnostics copied. Ready to paste into GitHub issues.",
                    ControlAppearance.Secondary,
                    SymbolRegular.CheckmarkCircle20);
            }
            catch (Exception ex)
            {
                _settingWindows?.ShowSnackbar(
                    "Copy Failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    SymbolRegular.DismissCircle20);
            }
        }
    }
}
