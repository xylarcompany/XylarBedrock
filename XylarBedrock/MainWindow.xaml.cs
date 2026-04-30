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
using XylarBedrock.Pages.Preview;
using XylarBedrock.Handlers;
using XylarBedrock.UI.Pages.Common;
using XylarBedrock.UI.Components;

namespace XylarBedrock
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.DataContext = MainViewModel.Default;
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MainViewModel.Default.AttemptClose(sender, e);
            if (!e.Cancel)
            {
                Properties.LauncherSettings.Default.LastSessionClosedCleanly = true;
                Properties.LauncherSettings.Default.Save();
            }
        }
        private async void Window_Initialized(object sender, EventArgs e)
        {
            Panel.SetZIndex(MainFrame, 0);
            Panel.SetZIndex(OverlayFrame, 1);
            Panel.SetZIndex(ErrorFrame, 2);
            Panel.SetZIndex(UpdateButton, 3);

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                bool startupReady = await RuntimeHandler.RecoverAndRunStartupAsync(async () =>
                {
                    await Program.OnApplicationLoaded();
                    await MainDataModel.Default.PackageManager.AutoRefreshBundledModAsync();
                });

                if (!startupReady) return;

                Properties.LauncherSettings.Default.LastSessionClosedCleanly = false;
                Properties.LauncherSettings.Default.Save();
                MainPage.NavigateToGamePage();
                StartupArgsHandler.RunStartupArgs();

                bool isFirstLaunch = Properties.LauncherSettings.Default.GetIsFirstLaunch(MainDataModel.Default.Config.profiles.Count());
                if (isFirstLaunch) 
                {
                    Properties.LauncherSettings.Default.IsFirstLaunch = false;
                    Properties.LauncherSettings.Default.Save();
                    MainViewModel.Default.SetOverlayFrame(new WelcomePage(), true);
                }
            }
        }


    }
}

