using XylarBedrock.Developer;
using XylarBedrock.Downloaders;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using XylarBedrock.ViewModels;

namespace XylarBedrock.Handlers
{
    public static class RuntimeHandler
    {
        static TraceSwitch traceSwitch = new TraceSwitch("General", "Entire Application") { Level = TraceLevel.Verbose };

        private static bool IsBugrockEnabled = false; 
        public static bool IsBugRockOfTheWeek()
        {
            return IsBugrockEnabled;
        }
        public static async Task InitalizeBugRockOfTheWeek()
        {
            IsBugrockEnabled = await ChangelogDownloader.GetBedrockOfTheWeekStatus();
        }

        public static void LogStartupInformation()
        {
            Trace.WriteLine("Product: " + App.DisplayName);
            Trace.WriteLine("Version: " + App.Version);
            Trace.WriteLine("Publisher: Xylar Inc. and Mrmariix");
        }
        public static bool EnableDeveloperMode()
        {
            System.Diagnostics.Trace.WriteLine("Developer Mode is not required by XylarBedrock.");
            return true;
        }

        public static bool IsDeveloperModeEnabled()
        {
            return false;
        }

        public static void ShowDeveloperModeGuidance()
        {
            System.Diagnostics.Trace.WriteLine("Developer Mode guidance skipped because it is no longer required.");
        }

        private static RegistryView GetCurrentView()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64) return RegistryView.Registry64;
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86) return RegistryView.Registry32;
            else return RegistryView.Default;
        }
        public static void ValidateOSArchitecture()
        {
            var Architecture = RuntimeInformation.OSArchitecture;
            bool canRun;
            switch (Architecture)
            {
                case Architecture.Arm:
                    ShowError("Unsupported Architexture", "This application can not run on ARM computers");
                    canRun = false;
                    break;
                case Architecture.Arm64:
                    ShowError("Unsupported Architexture", "This application can not run on ARM computers");
                    canRun = false;
                    break;
                case Architecture.X86:
                    canRun = true;
                    break;
                case Architecture.X64:
                    canRun = true;
                    break;
                default:
                    ShowError("Unsupported Architexture", "Unable to determine architexture, not supported");
                    canRun = false;
                    break;
            }

            if (!canRun) Environment.Exit(0);


            void ShowError(string title, string message)
            {
                MessageBox.Show(message, title);
            }
        }
        public static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Trace.WriteLine(e.Exception.ToString());
            MarkSessionAsUnclean();
        }

        public static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                Trace.WriteLine(exception.ToString());
            }

            MarkSessionAsUnclean();
        }

        public static async Task RecoverAfterUnexpectedExitAsync()
        {
            if (Properties.LauncherSettings.Default.LastSessionClosedCleanly) return;

            await PerformRecoveryStepsAsync("previous session");
            MarkRecoveryCheckpoint();
        }

        public static async Task<bool> RecoverAndRunStartupAsync(Func<Task> startupAction)
        {
            if (!Properties.LauncherSettings.Default.LastSessionClosedCleanly)
            {
                await RecoverAfterUnexpectedExitAsync();
            }

            try
            {
                await startupAction();
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Startup flow failed. Trying recovery once: {ex}");
                MarkSessionAsUnclean();
                await PerformRecoveryStepsAsync("startup retry");

                try
                {
                    await startupAction();
                    return true;
                }
                catch (Exception retryEx)
                {
                    Trace.WriteLine($"Startup retry failed: {retryEx}");
                    ShowFriendlyStartupMessage(
                        "XylarBedrock hit a startup issue and could not fully repair itself this time.\n\nPlease close it and try again once.");
                    return false;
                }
            }
        }

        private static void MarkSessionAsUnclean()
        {
            try
            {
                Properties.LauncherSettings.Default.LastSessionClosedCleanly = false;
                Properties.LauncherSettings.Default.Save();
            }
            catch
            {
            }
        }

        public static NLogTraceListener InternalTraceListener { get; set; } = new NLogTraceListener();

        public static void StartLogging()
        {
            Trace.Listeners.Add(InternalTraceListener);
            Trace.AutoFlush = true;
        }

        private static async Task PerformRecoveryStepsAsync(string reason)
        {
            Trace.WriteLine($"Starting launcher recovery: {reason}");

            try
            {
                ClearErrorLayer();
                CleanupLauncherTempFiles();
                AddonsCatalogHandler.CleanupManagedDownloads();
                AddonsCatalogHandler.RepairCachedCatalog();
                await MainDataModel.Default.PackageManager.AutoRefreshBundledModAsync();
                Trace.WriteLine("Launcher recovery finished.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Recovery skipped: {ex}");
            }
        }

        private static void CleanupLauncherTempFiles()
        {
            string launcherTempDirectory = Path.Combine(Path.GetTempPath(), "XylarBedrock");
            if (!Directory.Exists(launcherTempDirectory)) return;

            foreach (string filePath in Directory.GetFiles(launcherTempDirectory))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    bool shouldDelete =
                        fileInfo.Length == 0 ||
                        fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-6) ||
                        fileInfo.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
                        fileInfo.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                        fileInfo.Name.StartsWith("vc_redist", StringComparison.OrdinalIgnoreCase);

                    if (shouldDelete)
                    {
                        fileInfo.Delete();
                    }
                }
                catch
                {
                }
            }
        }

        private static void ClearErrorLayer()
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() => MainViewModel.Default.SetDialogFrame(null));
            }
            catch
            {
            }
        }

        private static void MarkRecoveryCheckpoint()
        {
            try
            {
                Properties.LauncherSettings.Default.LastSessionClosedCleanly = true;
                Properties.LauncherSettings.Default.Save();
            }
            catch
            {
            }
        }

        private static void ShowFriendlyStartupMessage(string message)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show(message, App.DisplayName, MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            catch
            {
            }
        }
    }
}

