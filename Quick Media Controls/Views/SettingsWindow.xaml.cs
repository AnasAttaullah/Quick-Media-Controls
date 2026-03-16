using Quick_Media_Controls.Views.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace Quick_Media_Controls
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : FluentWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();

        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsNavigation.Navigate(typeof(Home));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
