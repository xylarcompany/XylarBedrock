using System.Windows;
using System.Windows.Controls;

namespace XylarBedrock.Pages.Welcome
{
    public partial class WelcomePageFive : Page
    {
        public WelcomePagesSwitcher pageSwitcher = new WelcomePagesSwitcher();
        public WelcomePageFive()
        {
            InitializeComponent();
            BackButton.IsEnabled = false;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            pageSwitcher.MoveToPage(4);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            pageSwitcher.MoveToPage(6, BackupCheckbox.IsChecked.Value);
        }
    }
}

