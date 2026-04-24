using System.Windows;
using System.Windows.Controls;

namespace XylarBedrock.Pages.Welcome
{
    /// <summary>
    /// Логика взаимодействия для WelcomePageOne.xaml
    /// </summary>
    public partial class WelcomePageOne : Page
    {
        public WelcomePagesSwitcher pageSwitcher = new WelcomePagesSwitcher();
        public WelcomePageOne()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Close();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            pageSwitcher.MoveToPage(2);
        }

        private async void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModels.MainDataModel.Default.PackageManager.OpenOfficialStorePage();
        }
    }
}

