using XylarBedrock.Downloaders;
using XylarBedrock.Handlers;
using XylarBedrock.Localization.Language;
using XylarBedrock.ViewModels;
using JemExtensions;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace XylarBedrock
{
    public static class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private const string MinimumVCRuntimeVersion = "14.14.26405.0";
        private const string VCRedistDownloadUrl = "https://aka.ms/vc14/vc_redist.x64.exe";

        [STAThread]
        public static void Main()
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            ShowStartupNotice();

            RuntimeHandler.StartLogging();
            RuntimeHandler.LogStartupInformation();
            RuntimeHandler.ValidateOSArchitecture();
            Trace.WriteLine("Application Starting...");
            if (/*CheckForWindowsVersion() &&*/ CheckForVCRuntime())
            {
                RuntimeHandler.EnableDeveloperMode();
                var application = new App();
                application.Startup += OnApplicationInitalizing;
                application.InitializeComponent();
                application.Run();
            }
        }

        private static void ShowStartupNotice()
        {
            System.Windows.Forms.MessageBox.Show(
                App.StartupNotice,
                App.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        public static void OnApplicationInitalizing(object sender, StartupEventArgs e)
        {
            Trace.WriteLine("Application Initalization Started!");
            StartupArgsHandler.SetStartupArgs(e.Args);
            StartupArgsHandler.RunPreStartupArgs();
            Trace.WriteLine("Application Initalization Finished!");
        }
        public static async Task OnApplicationLoaded()
        {
            await MainViewModel.Default.ShowWaitingDialog(async () =>
            {
                Trace.WriteLine("Preparing Application...");
                await RuntimeHandler.InitalizeBugRockOfTheWeek();
                LanguageManager.Init();
                MainDataModel.Default.LoadConfig();
                await MainDataModel.Default.LoadVersions(true);
                MainDataModel.Default.ProgressBarState.PlayButtonLanguageChanged = !MainDataModel.Default.ProgressBarState.PlayButtonLanguageChanged;
                if (await MainDataModel.Updater.CheckForUpdatesAsync(true)) MainViewModel.Default.UpdateButton.ShowUpdateButton();
                Trace.WriteLine("Preparing Application: DONE");
            });
        }

        public static async Task OnApplicationRefresh()
        {

            await MainViewModel.Default.ShowWaitingDialog(async () =>
            {
                Trace.WriteLine("Refreshing Application...");
                MainDataModel.Default.LoadConfig();
                await MainDataModel.Default.LoadVersions();
                Trace.WriteLine("Refreshing Application: DONE");
            });
        }

        public static bool CheckForVCRuntime()
        {
            Trace.WriteLine("Checking VC Runtime version");
            if (TryGetInstalledVCRuntimeVersion(out Version installedVersion) &&
                installedVersion.CompareTo(new Version(MinimumVCRuntimeVersion)) >= 0)
            {
                Trace.WriteLine("VC++ Runtime OK");
                return true;
            }

            Trace.WriteLine("VC++ Runtime missing or outdated. Starting official Microsoft installer.");
            bool installResult = TryInstallVCRuntime();
            if (installResult)
            {
                Trace.WriteLine("VC++ Runtime OK");
                return true;
            }

            string errorMessage =
                "XylarBedrock needs the Microsoft Visual C++ x64 Runtime to start.\n\n" +
                "The launcher tried to install the official Microsoft package automatically but it did not complete.\n\n" +
                $"Please install it manually from:\n{VCRedistDownloadUrl}";
            Trace.WriteLine(errorMessage);
            System.Windows.Forms.MessageBox.Show(errorMessage, "Runtime required", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        public static bool CheckForWindowsVersion()
        {
            Trace.WriteLine("Checking Windows Version");
            bool result = false;
            string minimumVersionS = "10.0.19041.0";

            try
            {
                Version currentVersion = Environment.OSVersion.Version;
                Version minimumVersion = new Version(minimumVersionS);
                if (currentVersion.CompareTo(minimumVersion) >= 0) result = true;
            }
            catch (Exception) { }

            if (!result)
            {
                Trace.WriteLine("This application only works on Windows version " + minimumVersionS + " or above!");
                System.Windows.Forms.MessageBox.Show("This application only works on Windows version " + minimumVersionS + " or above!", "Error");
            }
            else
            {
                Trace.WriteLine("Windows Version OK");
            }
                return result;
        }

        private static bool TryInstallVCRuntime()
        {
            try
            {
                string workingDirectory = Path.Combine(Path.GetTempPath(), "XylarBedrock");
                Directory.CreateDirectory(workingDirectory);

                string installerPath = Path.Combine(workingDirectory, "vc_redist.x64.exe");
                string logPath = Path.Combine(workingDirectory, "vc_redist_install.log");

                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = client.GetAsync(VCRedistDownloadUrl).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    using Stream downloadStream = response.Content.ReadAsStream();
                    using FileStream fileStream = File.Create(installerPath);
                    downloadStream.CopyTo(fileStream);
                }

                ProcessStartInfo installerInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = $"/install /passive /norestart /log \"{logPath}\"",
                    UseShellExecute = true
                };

                using Process installer = Process.Start(installerInfo);
                installer?.WaitForExit();

                if (installer == null) return false;
                if (installer.ExitCode != 0 && installer.ExitCode != 3010)
                {
                    Trace.WriteLine($"VC++ Runtime installer exited with code {installer.ExitCode}");
                    return false;
                }

                return TryGetInstalledVCRuntimeVersion(out Version installedVersion)
                    && installedVersion.CompareTo(new Version(MinimumVCRuntimeVersion)) >= 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VC++ Runtime install failed: {ex}");
                return false;
            }
        }

        private static bool TryGetInstalledVCRuntimeVersion(out Version version)
        {
            version = null;

            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using RegistryKey runtimeKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                    string value = runtimeKey?.GetValue("Version") as string;
                    if (!string.IsNullOrWhiteSpace(value) && Version.TryParse(value.Replace("v", string.Empty), out Version parsedVersion))
                    {
                        version = parsedVersion;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Unable to read VC++ Runtime registry key in {view}: {ex.Message}");
                }
            }

            return false;
        }
    }
}

