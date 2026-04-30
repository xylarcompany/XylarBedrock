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
using System.Windows.Navigation;
using System.Windows.Shapes;
using CodeHollow.FeedReader;
using XylarBedrock.Classes;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using XylarBedrock.Handlers;
using XylarBedrock.UI.Components;
using XylarBedrock.Pages.News.Offical;

namespace XylarBedrock.Pages.News
{
    public partial class NewsScreenTabs : Page
    {
        private OfficalNewsPage javaNewsPage = new OfficalNewsPage();

        private Navigator Navigator { get; set; } = new Navigator();

        private string LastTabName;

        public NewsScreenTabs()
        {
            InitializeComponent();
            LastTabName = JavaTab.Name;
        }



        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
             Task.Run(() => ButtonManager_Base(LastTabName));
        }

        public void ResetButtonManager(string buttonName)
        {
            this.Dispatcher.Invoke(() =>
            {
                List<ToggleButton> toggleButtons = new List<ToggleButton>() {
                JavaTab,
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
                ButtonManager_Base(name);
            });
        }

        public void ButtonManager_Base(string senderName)
        {
            this.Dispatcher.Invoke(() =>
            {
                ResetButtonManager(senderName);

                if (senderName == JavaTab.Name) NavigateToJavaNews();
            });
        }

        public void NavigateToJavaNews()
        {
            Navigator.UpdatePageIndex(1);
            Task.Run(() => Navigator.Navigate(ContentFrame, javaNewsPage));
            LastTabName = JavaTab.Name;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (LastTabName.Equals(JavaTab.Name)) javaNewsPage.RefreshNews();
        }
    }
}

