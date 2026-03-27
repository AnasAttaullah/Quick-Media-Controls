using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Quick_Media_Controls.Views.Pages
{
    /// <summary>
    /// Interaction logic for Home.xaml
    /// </summary>
    public partial class Home : Page
    {
        public Home()
        {
            InitializeComponent();
        }

        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void CardAction_Click1(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/AnasAttaullah/Quick-Media-Controls/issues");
        }

        private void CardAction_Click_2(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/AnasAttaullah/Quick-Media-Controls/issues");
        }

        private void CardAction_Click_3(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/AnasAttaullah/Quick-Media-Controls");
        }

        private void CardAction_Click_4(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/AnasAttaullah/Quick-Media-Controls");
        }
    }
}
