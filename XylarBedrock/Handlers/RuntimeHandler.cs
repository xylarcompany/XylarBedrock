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
using XylarBedrock.Localization.Language;
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
            Architecture architecture = RuntimeInformation.OSArchitecture;
            switch (architecture)
            {
                case Architecture.Arm:
                    Trace.WriteLine("ARM device detected. The launcher will try to continue in compatibility mode.");
                    break;
                case Architecture.Arm64:
                    Trace.WriteLine("ARM64 device detected. The launcher will try to continue in compatibility mode.");
                    break;
                case Architecture.X86:
                    Trace.WriteLine("x86 device detected.");
                    break;
                case Architecture.X64:
                    Trace.WriteLine("x64 device detected.");
                    break;
                default:
                    Trace.WriteLine("Unknown device architecture detected. The launcher will still try to start.");
                    break;
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
                MarkRecoveryCheckpoint();
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
                    MarkRecoveryCheckpoint();
                    return true;
                }
                catch (Exception retryEx)
                {
                    Trace.WriteLine($"Startup retry failed: {retryEx}");
                    return ContinueWithMinimalStartupFallback(retryEx);
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

        private static bool ContinueWithMinimalStartupFallback(Exception startupException)
        {
            try
            {
                Trace.WriteLine("Trying minimal startup fallback.");
                Trace.WriteLine(startupException.ToString());
                ClearErrorLayer();
                LanguageManager.Init();
                MainDataModel.Default.LoadConfig();
                MainDataModel.Default.ProgressBarState.PlayButtonLanguageChanged =
                    !MainDataModel.Default.ProgressBarState.PlayButtonLanguageChanged;
                MarkRecoveryCheckpoint();
                Trace.WriteLine("Minimal startup fallback completed.");
                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}

