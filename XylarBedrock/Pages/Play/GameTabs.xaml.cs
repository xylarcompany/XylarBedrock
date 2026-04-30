using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Animation;
using XylarBedrock.Classes;
using XylarBedrock.Downloaders;
using XylarBedrock.Pages.Play.FAQ;
using XylarBedrock.Pages.Play.Home;
using XylarBedrock.Pages.Play.Installations;
using XylarBedrock.Pages.Play.PatchNotes;
using XylarBedrock.UI.Components;

namespace XylarBedrock.Pages.Play
{
    public partial class GameTabs : Page
    {
        private static ChangelogDownloader PatchNotesDownloader = new ChangelogDownloader();

        public PlayScreenPage playScreenPage = new PlayScreenPage();
        public InstallationsScreen installationsScreen = new InstallationsScreen();
        public FaqPage faqPage = new FaqPage();
        public PatchNotesPage patchNotesPage = new PatchNotesPage(PatchNotesDownloader);

        private Navigator Navigator { get; set; } = new Navigator();

        public GameTabs()
        {
            InitializeComponent();
        }



        public void ResetButtonManager(string buttonName)
        {
            this.Dispatcher.Invoke(() =>
            {
                ToggleButton[] toggleButtons = new ToggleButton[] {
                PlayButton,
                InstallationsButton,
                FaqButton,
                PatchNotesButton
            };

                foreach (ToggleButton button in toggleButtons)
                {
                    button.IsChecked = button.Name == buttonName;
                }
            });

        }

        public void ButtonManager2(object sender, RoutedEventArgs e)
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

                if (senderName == PlayButton.Name) NavigateToPlayScreen();
                else if (senderName == InstallationsButton.Name) NavigateToInstallationsPage();
                else if (senderName == FaqButton.Name) NavigateToFaqPage();
                else if (senderName == PatchNotesButton.Name) NavigateToPatchNotes();
            });
        }

        public void NavigateToPlayScreen()
        {
            Navigator.UpdatePageIndex(0);
            Task.Run(() => Navigator.Navigate(MainPageFrame, playScreenPage));

        }
        public void NavigateToInstallationsPage()
        {
            Navigator.UpdatePageIndex(1);
            Task.Run(() => Navigator.Navigate(MainPageFrame, installationsScreen));
        }

        public void NavigateToFaqPage()
        {
            Navigator.UpdatePageIndex(2);
            Task.Run(() => Navigator.Navigate(MainPageFrame, faqPage));
        }

        public void NavigateToPatchNotes()
        {
            Navigator.UpdatePageIndex(3);
            Task.Run(() => Navigator.Navigate(MainPageFrame, patchNotesPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ResetButtonManager(null);
            ButtonManager_Base(PlayButton.Name);
        }
    }
}

