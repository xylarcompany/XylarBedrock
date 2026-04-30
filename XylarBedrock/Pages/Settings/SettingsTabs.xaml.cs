using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XylarBedrock.Pages.Settings.General;
using XylarBedrock.UI.Components;

namespace XylarBedrock.Pages.Settings
{
    public partial class SettingsTabs : Page
    {
        public GeneralSettingsPage generalSettingsPage = new GeneralSettingsPage();
        public AboutPage aboutPage = new AboutPage();

        private Navigator Navigator { get; set; } = new Navigator();

        public SettingsTabs()
        {
            InitializeComponent();
            ButtonManager_Base(GeneralButton.Name);
        }
        public void ResetButtonManager(string buttonName)
        {
            this.Dispatcher.Invoke(() =>
            {
                List<ToggleButton> toggleButtons = new List<ToggleButton>() {
                GeneralButton,
                AboutButton
            };

                foreach (ToggleButton button in toggleButtons) { button.IsChecked = false; }

                if (toggleButtons.Exists(x => x.Name == buttonName))
                {
                    toggleButtons.Where(x => x.Name == buttonName).FirstOrDefault().IsChecked = true;
                }
            });

        }

        public void ButtonManager(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                var toggleButton = sender as ToggleButton;
                string name = toggleButton.Name;
                Task.Run(() => ButtonManager_Base(name));
            });
        }
        public void ButtonManager_Base(string senderName)
        {
            this.Dispatcher.Invoke(() =>
            {
                ResetButtonManager(senderName);

                if (senderName == GeneralButton.Name) NavigateToGeneralPage();
                else if (senderName == AboutButton.Name) NavigateToAboutPage();
            });
        }

        public void NavigateToGeneralPage()
        {
            Navigator.UpdatePageIndex(0);
            Task.Run(() => Navigator.Navigate(SettingsScreenFrame,generalSettingsPage));
        }

        public void NavigateToAccountsPage()
        {
            Navigator.UpdatePageIndex(1);
        }

        public void NavigateToAboutPage()
        {
            Navigator.UpdatePageIndex(2);
            Task.Run(() => Navigator.Navigate(SettingsScreenFrame,aboutPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}

