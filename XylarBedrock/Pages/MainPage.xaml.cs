using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms.Design;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Data;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Core;
using Windows.Management.Deployment;
using Windows.System;
using XylarBedrock.Classes;
using System.Windows.Media.Animation;
using XylarBedrock.Pages;
using XylarBedrock.Pages.Welcome;
using XylarBedrock.ViewModels;
using XylarBedrock.Pages.Settings;
using XylarBedrock.Pages.Play;
using XylarBedrock.Pages.News;
using XylarBedrock.Pages.Addons;
using XylarBedrock.Pages.Preview;
using XylarBedrock.Handlers;
using XylarBedrock.UI.Pages.Common;
using XylarBedrock.UI.Components;
using XylarBedrock.Pages.Toolbar;

namespace XylarBedrock.Pages
{
    public partial class MainPage : Page, IDisposable
    {
        private GameTabs gamePage;
        private SettingsTabs settingsScreenPage;
        private NewsScreenTabs newsScreenPage;
        private AddonsPage addonsPage;

        private Navigator Navigator { get; set; } = new Navigator(true);

        public MainPage()
        {
            this.DataContext = MainDataModel.Default;
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MainDataModel.Default.PackageManager.Cancel();
        }

        public void ResetButtonManager(string buttonName)
        {
            this.Dispatcher.Invoke(() =>
            {
                List<ToggleButton> toggleButtons = new List<ToggleButton>() { 
                NewsButton.Button,
                AddonsButton.Button,
                BedrockEditionButton.Button,
                SettingsButton.Button,
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

                if (senderName == BedrockEditionButton.Name) NavigateToGamePage();
                else if (senderName == NewsButton.Name) NavigateToNewsPage();
                else if (senderName == AddonsButton.Name) NavigateToAddonsPage();
                else if (senderName == SettingsButton.Name) NavigateToSettings();
            });

        }

        public void NavigateToNewsPage()
        {
            this.Dispatcher.Invoke(() =>
            {
                Navigator.UpdatePageIndex(0);
                NewsButton.Button.IsChecked = true;
                NewsScreenTabs page = GetNewsPage();
                Task.Run(() => Navigator.Navigate(MainWindowFrame, page));
            });

        }
        public void NavigateToGamePage()
        {
            this.Dispatcher.Invoke(() =>
            {
                Navigator.UpdatePageIndex(1);
                BedrockEditionButton.Button.IsChecked = true;
                GameTabs page = GetGamePage();
                Task.Run(() => Navigator.Navigate(MainWindowFrame, page));
            });

        }

        public void NavigateToAddonsPage()
        {
            this.Dispatcher.Invoke(() =>
            {
                Navigator.UpdatePageIndex(2);
                AddonsButton.Button.IsChecked = true;
                AddonsPage page = GetAddonsPage();
                Task.Run(() => Navigator.Navigate(MainWindowFrame, page));
            });

        }

        public void NavigateToSettings()
        {
            this.Dispatcher.Invoke(() =>
            {
                Navigator.UpdatePageIndex(4);
                SettingsButton.Button.IsChecked = true;
                SettingsTabs page = GetSettingsPage();
                Task.Run(() => Navigator.Navigate(MainWindowFrame, page));
            });

        }

        private void BedrockEditionButton_Click(object sender, EventArgs e)
        {
            if (sender != null && sender is Toolbar_ButtonBase) ButtonManager_Base((sender as Toolbar_ButtonBase).Name);
        }

        private void NewsButton_Click(object sender, EventArgs e)
        {
            if (sender != null && sender is Toolbar_ButtonBase) ButtonManager_Base((sender as Toolbar_ButtonBase).Name);
        }

        private void AddonsButton_Click(object sender, EventArgs e)
        {
            if (sender != null && sender is Toolbar_ButtonBase) ButtonManager_Base((sender as Toolbar_ButtonBase).Name);
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            if (sender != null && sender is Toolbar_ButtonBase) ButtonManager_Base((sender as Toolbar_ButtonBase).Name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {

        }

        private GameTabs GetGamePage()
        {
            gamePage ??= new GameTabs();
            return gamePage;
        }

        private SettingsTabs GetSettingsPage()
        {
            settingsScreenPage ??= new SettingsTabs();
            return settingsScreenPage;
        }

        private NewsScreenTabs GetNewsPage()
        {
            newsScreenPage ??= new NewsScreenTabs();
            return newsScreenPage;
        }

        private AddonsPage GetAddonsPage()
        {
            addonsPage ??= new AddonsPage();
            return addonsPage;
        }

    }
}

