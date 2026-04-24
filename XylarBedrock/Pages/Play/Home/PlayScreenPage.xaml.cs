using XylarBedrock.Classes;
using XylarBedrock.Enums;
using XylarBedrock.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XylarBedrock.Pages.Play.Home
{
    public partial class PlayScreenPage : Page
    {
        private bool isLauncherFullyLoaded = false;
        private Window ownerWindow;

        public PlayScreenPage()
        {
            InitializeComponent();
            Loaded += PlayScreenPage_Loaded;
            Unloaded += PlayScreenPage_Unloaded;
            InstallationsList.SelectionChanged += CheckVersionAvailability;
            ((INotifyPropertyChanged)MainDataModel.Default.ProgressBarState).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainDataModel.Default.ProgressBarState.AllowPlaying) ||
                    e.PropertyName == nameof(MainDataModel.Default.ProgressBarState.IsGameRunning) ||
                    e.PropertyName == nameof(MainDataModel.Default.ProgressBarState.PlayButtonLanguageChanged))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CheckVersionAvailability(s, e);
                    });
                }
            };
        }

        private void PlayScreenPage_Loaded(object sender, RoutedEventArgs e)
        {
            ownerWindow = Window.GetWindow(this);
            if (ownerWindow != null)
            {
                ownerWindow.Activated -= OwnerWindow_Activated;
                ownerWindow.Activated += OwnerWindow_Activated;
            }

            CheckVersionAvailability(sender, e);
        }

        private void PlayScreenPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ownerWindow != null)
            {
                ownerWindow.Activated -= OwnerWindow_Activated;
                ownerWindow = null;
            }
        }

        private void OwnerWindow_Activated(object sender, EventArgs e)
        {
            CheckVersionAvailability(sender, e);
        }

        private void CheckVersionAvailability(object _, EventArgs __)
        {
            BLInstallation selectedInstallation = InstallationsList.SelectedItem as BLInstallation;
            bool isOfficialStoreInstalled = MainDataModel.Default.PackageManager.IsOfficialStoreReleaseInstalled();
            bool hasBundledDllPack = MainDataModel.Default.PackageManager.HasBundledModSource();

            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                MainPlayButton.IsEnabled = true;
                ApplyPlayButtonStyle(MainDataModel.Default.ProgressBarState.PlayButtonString, Brushes.White, 26);
            }
            else if (!isOfficialStoreInstalled)
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyStoreButtonStyle("Download Minecraft First.", Brushes.LightGray, 18);
            }
            else if (!hasBundledDllPack)
            {
                MainPlayButton.IsEnabled = false;
                ApplyStoreButtonStyle("Missing dll Folder.", Brushes.LightGray, 18);
            }
            else if (!MainDataModel.Default.PackageManager.IsBundledModInstalled())
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyModsButtonStyle("GET MODS", Brushes.White, 22);
            }
            else if (!isLauncherFullyLoaded)
            {
                // Can not check if versions exists on first load.
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyPlayButtonStyle("Play", Brushes.White, 26);
                isLauncherFullyLoaded = true;
            }
            else if (selectedInstallation is not null &&
                     selectedInstallation.Version is null &&
                     !(selectedInstallation.ReadOnly && selectedInstallation.VersioningMode == VersioningMode.LatestRelease))
            {
                MainPlayButton.IsEnabled = false;
                ApplyStoreButtonStyle("Download Minecraft First.", Brushes.LightGray, 18);
            }
            else
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyPlayButtonStyle("Play", Brushes.White, 26);
            }
        }

        private void ApplyPlayButtonStyle(string text, Brush foreground, double fontSize)
        {
            MainPlayButton.Style = (Style)FindResource("BigGreenButton");
            MainPlayButton.Width = 250;
            PlayButtonText.Text = text;
            PlayButtonText.Foreground = foreground;
            PlayButtonText.FontSize = fontSize;
        }

        private void ApplyStoreButtonStyle(string text, Brush foreground, double fontSize)
        {
            MainPlayButton.Style = (Style)FindResource("BigStoreButton");
            MainPlayButton.Width = 340;
            PlayButtonText.Text = text;
            PlayButtonText.Foreground = foreground;
            PlayButtonText.FontSize = fontSize;
        }

        private void ApplyModsButtonStyle(string text, Brush foreground, double fontSize)
        {
            MainPlayButton.Style = (Style)FindResource("BigModsButton");
            MainPlayButton.Width = 250;
            PlayButtonText.Text = text;
            PlayButtonText.Foreground = foreground;
            PlayButtonText.FontSize = fontSize;
        }

        private string GetLatestImage()
        {
            return Constants.Themes.First().Value;
        }

        private string GetCustomImage(string result)
        {
            DirectoryInfo directoryInfo = Directory.CreateDirectory(MainDataModel.Default.FilePaths.ThemesFolder);
            foreach (var file in directoryInfo.GetFiles())
            {
                if (file.Name == result) return file.FullName;
            }

            return Constants.Themes.Where(x => x.Key == "Original").FirstOrDefault().Value;
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string packUri = string.Empty;
                string currentTheme = Properties.LauncherSettings.Default.CurrentTheme;

                bool isBugRock = Handlers.RuntimeHandler.IsBugRockOfTheWeek();
                if (isBugRock)
                {
                    BedrockLogo.Visibility = Visibility.Collapsed;
                    BugrockLogo.Visibility = Visibility.Visible;
                    BugrockOfTheWeekLogo.Visibility = Visibility.Visible;
                }
                else
                {
                    BedrockLogo.Visibility = Visibility.Visible;
                    BugrockLogo.Visibility = Visibility.Collapsed;
                    BugrockOfTheWeekLogo.Visibility = Visibility.Collapsed;
                }

                if (currentTheme.StartsWith(Constants.ThemesCustomPrefix))
                {
                    packUri = GetCustomImage(currentTheme.Remove(0, Constants.ThemesCustomPrefix.Length));
                }
                else
                {
                    switch (currentTheme)
                    {
                        case "LatestUpdate":
                            packUri = GetLatestImage();
                            break;
                        default:
                            if (Constants.Themes.ContainsKey(currentTheme)) packUri = Constants.Themes.Where(x => x.Key == currentTheme).FirstOrDefault().Value;
                            else packUri = Constants.Themes.Where(x => x.Key == "Original").FirstOrDefault().Value;
                            break;
                    }
                }

                try
                {
                    BitmapImage bmp = new BitmapImage(new Uri(packUri, UriKind.Absolute));
                    ImageBrush.ImageSource = bmp;
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
            });
        }

        private async void MainPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                MainDataModel.Default.KillGame();
            }
            else if (!MainDataModel.Default.PackageManager.IsOfficialStoreReleaseInstalled())
            {
                await MainDataModel.Default.PackageManager.OpenOfficialStorePage();
            }
            else if (!MainDataModel.Default.PackageManager.IsBundledModInstalled())
            {
                bool installed = await MainDataModel.Default.PackageManager.InstallBundledModAsync();
                if (installed)
                {
                    CheckVersionAvailability(sender, e);
                }
            }
            else
            {
                BLInstallation i = InstallationsList.SelectedItem as BLInstallation;
                bool keepLauncherOpen = Properties.LauncherSettings.Default.KeepLauncherOpen;
                MainDataModel.Default.Play(MainDataModel.Default.Config.CurrentProfile, i, keepLauncherOpen, false);
            }
        }
    }
}
