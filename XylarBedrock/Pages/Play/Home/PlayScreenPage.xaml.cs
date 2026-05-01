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
using XylarBedrock.Classes.Launcher;

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
            BLInstallation selectedInstallation = ResolveSelectedInstallation();
            bool isOfficialStoreInstalled = MainDataModel.Default.PackageManager.IsOfficialStoreReleaseInstalled();
            BundledDllPackDiagnostics bundledDllPack = MainDataModel.Default.PackageManager.GetBundledDllPackDiagnostics();

            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                MainPlayButton.IsEnabled = true;
                ApplyButtonDetails(null);
                ApplyPlayButtonStyle(MainDataModel.Default.ProgressBarState.PlayButtonString, Brushes.White, 26);
            }
            else if (!isOfficialStoreInstalled)
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyButtonDetails("Minecraft for Windows was not found. Install the original Microsoft Store release first.");
                ApplyStoreButtonStyle("Download Minecraft First.", Brushes.LightGray, 18);
            }
            else if (!bundledDllPack.IsReady)
            {
                MainPlayButton.IsEnabled = false;
                ApplyButtonDetails(bundledDllPack.DetailsText);
                ApplyStoreButtonStyle(GetBundledDllButtonText(bundledDllPack), Brushes.LightGray, 18);
            }
            else if (!MainDataModel.Default.PackageManager.IsBundledModInstalled())
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyButtonDetails("The bundled mod pack is ready. Click GET MODS to copy it into your Minecraft profile.");
                ApplyModsButtonStyle("GET MODS", Brushes.White, 22);
            }
            else if (!isLauncherFullyLoaded)
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyButtonDetails(null);
                ApplyPlayButtonStyle("Play", Brushes.White, 26);
                isLauncherFullyLoaded = true;
            }
            else if (selectedInstallation is not null &&
                     selectedInstallation.Version is null &&
                     !(selectedInstallation.ReadOnly && selectedInstallation.VersioningMode == VersioningMode.LatestRelease))
            {
                MainPlayButton.IsEnabled = false;
                ApplyButtonDetails("The selected installation is incomplete. Recreate it or switch back to the official Microsoft Store release.");
                ApplyStoreButtonStyle("Download Minecraft First.", Brushes.LightGray, 18);
            }
            else if (selectedInstallation is not null && !IsSupportedStoreInstallation(selectedInstallation))
            {
                MainPlayButton.IsEnabled = false;
                ApplyButtonDetails("Play now supports only the official Minecraft for Windows release from Microsoft Store.");
                ApplyStoreButtonStyle("Store Release Only", Brushes.LightGray, 18);
            }
            else if (selectedInstallation is null)
            {
                MainPlayButton.IsEnabled = false;
                ApplyButtonDetails("No valid Minecraft installation is selected right now. Reopen the launcher once or pick the official Store release.");
                ApplyStoreButtonStyle("Select Installation", Brushes.LightGray, 18);
            }
            else
            {
                MainPlayButton.IsEnabled = MainDataModel.Default.ProgressBarState.AllowPlaying;
                ApplyButtonDetails(null);
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

        private void ApplyButtonDetails(string details)
        {
            ToolTipService.SetShowOnDisabled(MainPlayButton, true);
            MainPlayButton.ToolTip = string.IsNullOrWhiteSpace(details) ? null : details;
        }

        private BLInstallation ResolveSelectedInstallation()
        {
            BLInstallation selectedInstallation = InstallationsList.SelectedItem as BLInstallation;
            if (IsSupportedStoreInstallation(selectedInstallation))
            {
                return selectedInstallation;
            }

            selectedInstallation = MainDataModel.Default.Config.CurrentInstallations?
                .FirstOrDefault(IsSupportedStoreInstallation);

            if (selectedInstallation == null)
            {
                selectedInstallation = MainDataModel.Default.Config.CurrentInstallation;
            }

            if (selectedInstallation != null && !ReferenceEquals(InstallationsList.SelectedItem, selectedInstallation))
            {
                InstallationsList.SelectedItem = selectedInstallation;
            }

            return selectedInstallation;
        }

        private static string GetBundledDllButtonText(BundledDllPackDiagnostics diagnostics)
        {
            if (!diagnostics.DllDirectoryExists) return "Missing dll Folder.";
            if (!diagnostics.ModDllExists) return $"Missing {Constants.BUNDLED_MOD_DLL_NAME}";
            if (!diagnostics.RuntimeDllExists) return $"Missing {Constants.EXTRA_DLL_NAME}";
            if (!diagnostics.ModDllReadable || !diagnostics.RuntimeDllReadable) return "DLL Pack Unreadable";
            return "Missing DLL Pack";
        }

        private static bool IsSupportedStoreInstallation(BLInstallation installation)
        {
            return installation != null &&
                   installation.ReadOnly &&
                   installation.VersioningMode == VersioningMode.LatestRelease;
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
                BLInstallation i = ResolveSelectedInstallation();
                if (i == null)
                {
                    MessageBox.Show(
                        "No valid Minecraft installation is selected right now. Reopen the launcher once and try Play again.",
                        App.DisplayName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool keepLauncherOpen = Properties.LauncherSettings.Default.KeepLauncherOpen;
                MainDataModel.Default.Play(MainDataModel.Default.Config.CurrentProfile, i, keepLauncherOpen, false);
            }
        }
    }
}
