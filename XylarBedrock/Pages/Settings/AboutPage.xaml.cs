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
using System.Diagnostics;
using XylarBedrock.ViewModels;

namespace XylarBedrock.Pages.Settings
{
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {

        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSupportDiagnostics();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            JemExtensions.WebExtensions.LaunchWebLink(button.Tag.ToString());
            e.Handled = true;
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () => {
                var result = await MainDataModel.Updater.CheckForUpdatesAsync();
                if (result) MainViewModel.Default.UpdateButton.ShowUpdateButton();
            });
        }

        private void ForceUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            MainDataModel.Updater.UpdateButton_Click(sender, e);
        }

        private void RefreshSupportDiagnostics()
        {
            var diagnostics = MainDataModel.Default.PackageManager.GetSupportDiagnostics();

            DiagnosticsStoreStatusText.Text = diagnostics.OfficialStoreReleaseDetected ? "Detected" : "Not detected";
            DiagnosticsDllStatusText.Text = diagnostics.BundledDllPack.StatusText;
            DiagnosticsLastLaunchText.Text = diagnostics.LastLaunchMethodAttempted;
            DiagnosticsStorePathsText.Text = JoinPaths(diagnostics.OfficialStoreReleaseDirectories);
            DiagnosticsPreviewPathsText.Text = JoinPaths(diagnostics.PreviewOrLocalDirectories);
        }

        private static string JoinPaths(System.Collections.Generic.IReadOnlyList<string> paths)
        {
            return paths != null && paths.Any()
                ? string.Join(Environment.NewLine, paths)
                : "None detected.";
        }

    }
}

