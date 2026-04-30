using XylarBedrock.Classes;
using XylarBedrock.Downloaders;
using JemExtensions;
using SymbolicLinkSupport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.System;
using ZipProgress = JemExtensions.ZipFileExtensions.ZipProgress;
using XylarBedrock.Enums;
using System.Windows.Input;
using XylarBedrock.ViewModels;
using XylarBedrock.Exceptions;
using XylarBedrock.UpdateProcessor;
using XylarBedrock.UpdateProcessor.Authentication;
using XylarBedrock.UpdateProcessor.Handlers;
using XylarBedrock.Classes.Launcher;
using Windows.System.Diagnostics;
using XylarBedrock.UpdateProcessor.Enums;
using JemExtensions.WPF.Commands;
using XylarBedrock.UI.Pages.Common;
using System.Collections;
using XylarBedrock.UpdateProcessor.Classes;

namespace XylarBedrock.Handlers
{
    public class PackageHandler : IDisposable
    {
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private StoreNetwork StoreNetwork = new StoreNetwork();
        private PackageManager PM = new PackageManager();

        public VersionDownloader VersionDownloader { get; private set; } = new VersionDownloader();
        public Process GameHandle { get; private set; } = null;
        public bool isGameRunning { get => GameHandle != null; }

        #region Public Methods

        public async Task LaunchPackage(MCVersion v, string dirPath, bool KeepLauncherOpen, bool LaunchEditor)
        {
            try
            {
                StartTask();
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isLaunching);
                bool launchRequested = await TryLaunchMinecraftAsync(v.Type, LaunchEditor);

                if (launchRequested)
                {
                    Trace.WriteLine("App launch finished!");

                    EndTask();

                    if (!KeepLauncherOpen)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => Application.Current.MainWindow.Close());
                    }
                    else
                    {
                        _ = GetGameHandle(GetKnownMinecraftProcessNames(v.Type));
                    }
                }
                else
                {
                    EndTask();
                    string message = LaunchEditor
                        ? $"Impossible to launch Editor: Failed to open {Constants.GetUri(v.Type)} URI"
                        : "Minecraft for Windows could not be started from any detected installation.";
                    SetException(new AppLaunchFailedException(message, new Exception()));
                }
            }
            catch (Exception e)
            {
                EndTask();
                SetException(new AppLaunchFailedException(e));
            }
        }

        public bool IsOfficialStoreReleaseInstalled()
        {
            return GetInstalledMinecraftPackages(VersionType.Release).Any();
        }

        public string GetOfficialStorePackageVersionString()
        {
            var package = GetInstalledMinecraftPackages(VersionType.Release)
                .OrderByDescending(pkg => pkg.Id.Version.Major)
                .ThenByDescending(pkg => pkg.Id.Version.Minor)
                .ThenByDescending(pkg => pkg.Id.Version.Build)
                .ThenByDescending(pkg => pkg.Id.Version.Revision)
                .FirstOrDefault();

            if (package == null) return string.Empty;

            PackageVersion version = package.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        public IReadOnlyList<string> GetInstalledMinecraftDirectories(VersionType type)
        {
            return GetInstalledMinecraftPackages(type)
                .Select(GetInstalledLocationPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void ShowOfficialStoreRequirementMessage()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    "XylarBedrock now works only with the original Minecraft for Windows app from Microsoft Store. Install it first, even with the free trial, then reopen the launcher.",
                    App.DisplayName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }

        public async Task OpenOfficialStorePage()
        {
            Uri storeUri = new Uri(Constants.MINECRAFT_STORE_URI);
            bool opened = false;

            try
            {
                opened = await Launcher.LaunchUriAsync(storeUri);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Unable to open Microsoft Store URI: {ex}");
            }

            if (!opened)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Constants.MINECRAFT_STORE_WEB_URL,
                    UseShellExecute = true
                });
            }
        }

        public string GetModsDirectoryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Minecraft Bedrock",
                Constants.MODS_FOLDER_NAME);
        }

        public string GetBundledDllDirectoryPath()
        {
            return Path.Combine(MainDataModel.Default.FilePaths.ExecutableDirectory, Constants.BUNDLED_MODS_DIRECTORY_NAME);
        }

        public string GetBundledModSourcePath()
        {
            return Path.Combine(GetBundledDllDirectoryPath(), Constants.BUNDLED_MOD_DLL_NAME);
        }

        public string GetBundledExtraDllSourcePath()
        {
            return Path.Combine(GetBundledDllDirectoryPath(), Constants.EXTRA_DLL_NAME);
        }

        public string GetInstalledModPath()
        {
            return Path.Combine(GetModsDirectoryPath(), Constants.BUNDLED_MOD_DLL_NAME);
        }

        public bool IsBundledModInstalled()
        {
            string sourcePath = GetBundledModSourcePath();
            string installedPath = GetInstalledModPath();

            if (!File.Exists(sourcePath) || !File.Exists(installedPath)) return false;

            FileInfo sourceInfo = new FileInfo(sourcePath);
            FileInfo installedInfo = new FileInfo(installedPath);

            if (sourceInfo.Length != installedInfo.Length) return false;

            return GetFileHash(sourcePath) == GetFileHash(installedPath);
        }

        public bool HasBundledModSource()
        {
            return File.Exists(GetBundledModSourcePath()) && File.Exists(GetBundledExtraDllSourcePath());
        }

        public async Task<bool> InstallBundledModAsync()
        {
            return await InstallBundledModInternalAsync(showMessage: true, forceInstall: true);
        }

        public async Task<bool> AutoRefreshBundledModAsync()
        {
            string currentMinecraftVersion = GetOfficialStorePackageVersionString();
            bool versionChanged = !string.IsNullOrWhiteSpace(currentMinecraftVersion) &&
                                  !string.Equals(Properties.LauncherSettings.Default.LastPatchedMinecraftVersion, currentMinecraftVersion, StringComparison.OrdinalIgnoreCase);

            return await InstallBundledModInternalAsync(showMessage: false, forceInstall: versionChanged);
        }

        private async Task<bool> InstallBundledModInternalAsync(bool showMessage, bool forceInstall)
        {
            string dllDirectoryPath = GetBundledDllDirectoryPath();
            string sourcePath = GetBundledModSourcePath();
            string extraDllSourcePath = GetBundledExtraDllSourcePath();
            string installedPath = GetInstalledModPath();

            if (!File.Exists(sourcePath) || !File.Exists(extraDllSourcePath))
            {
                if (showMessage)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            "Required DLL pack not found next to the launcher.\n\n" +
                            $"Expected folder:\n{dllDirectoryPath}\n\n" +
                            $"Required files:\n- {Constants.BUNDLED_MOD_DLL_NAME}\n- {Constants.EXTRA_DLL_NAME}",
                            App.DisplayName,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }

                return false;
            }

            if (!forceInstall && IsBundledModInstalled()) return true;

            await Task.Run(() =>
            {
                Directory.CreateDirectory(GetModsDirectoryPath());
                File.Copy(sourcePath, installedPath, true);

                string minecraftFolder = @"C:\Program Files\WindowsApps\MICROSOFT.MINECRAFTUWP_1.26.1301.0_x64__8wekyb3d8bbwe";
                string extraDllDestPath = Path.Combine(minecraftFolder, Constants.EXTRA_DLL_NAME);

                if (File.Exists(extraDllSourcePath))
                {
                    Directory.CreateDirectory(minecraftFolder);
                    File.Copy(extraDllSourcePath, extraDllDestPath, true);
                }
            });

            bool installed = IsBundledModInstalled();
            string currentMinecraftVersion = GetOfficialStorePackageVersionString();
            if (installed && !string.IsNullOrWhiteSpace(currentMinecraftVersion))
            {
                Properties.LauncherSettings.Default.LastPatchedMinecraftVersion = currentMinecraftVersion;
                Properties.LauncherSettings.Default.Save();
            }

            if (!showMessage) return installed;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    installed
                        ? "Mods are now in your Minecraft Bedrock mods folder. You can press PLAY now."
                        : "The launcher could not finish copying the bundled mod file.",
                    App.DisplayName,
                    MessageBoxButton.OK,
                    installed ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });

            return installed;
        }


        public async Task InstallPackage(MCVersion v, string dirPath)
        {
            try
            {
                StartTask();

                if (!v.IsInstalled)
                {
                    List<VersionInfoJson> versions = VersionManager.Singleton.GetVersions();
                    if (versions.Any(ver => v.UUID.CompareTo(ver.uuid.ToString()) == 0))
                    {
                        await DownloadAndExtractPackage(v);
                    }
                    else
                    {
                        throw new NoVersionAccessibleException();
                    }
                }

                await UnregisterPackage(v, true);
                await RegisterPackage(v);

                await RedirectSaveData(dirPath, v.Type);
            }
            catch (PackageManagerException e)
            {
                SetException(e);
            }
            catch (NoVersionAccessibleException e)
            {
                SetException(e);
            }
            catch (Exception e)
            {
                SetException(new AppInstallFailedException(e));
            }
            finally
            {
                EndTask();
            }
        }
        public async Task ClosePackage()
        {
            if (GameHandle != null)
            {
                string title = XylarBedrock.Localization.Language.LanguageManager.GetResource("Dialog_KillGame_Title") as string;
                string content = XylarBedrock.Localization.Language.LanguageManager.GetResource("Dialog_KillGame_Text") as string;
                var result = await DialogPrompt.ShowDialog_YesNo(title, content);

                if (result == System.Windows.Forms.DialogResult.Yes) GameHandle.Kill();
            }
        }
        public async Task RemovePackage(MCVersion v)
        {
            try
            {
                StartTask();

                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isUninstalling);
                await UnregisterPackage(v, false, true);
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isUninstalling);
                await DirectoryExtensions.DeleteAsync(v.GameDirectory, (x, y, phase) => ProgressWrapper(x, y, phase), "Files", "Folders");
                if (Directory.Exists(v.GameDirectory)) Directory.Delete(v.GameDirectory, true);
                v.UpdateFolderSize();
                await Task.Run(Program.OnApplicationRefresh);
                foreach (var ver in MainDataModel.Default.Versions) ver.UpdateFolderSize();
            }
            catch (PackageManagerException e)
            {
                SetException(e);
            }
            catch (Exception ex)
            {
                SetException(new PackageRemovalFailedException(ex));
            }
            finally
            {
                EndTask();
            }
        }
        public async Task AddPackage(string packagePath)
        {
            try
            {
                if (!File.Exists(packagePath)) return;
                StartTask();
                var outputDirectoryName = FileExtensions.GetAvaliableFileName(Path.GetFileNameWithoutExtension(packagePath), MainDataModel.Default.FilePaths.VersionsFolder);
                var outputDirectoryPath = Path.Combine(MainDataModel.Default.FilePaths.VersionsFolder, outputDirectoryName);
                Trace.WriteLine("Extraction started");
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isExtracting);
                if (Directory.Exists(outputDirectoryPath)) Directory.Delete(outputDirectoryPath, true);
                var fileStream = File.OpenRead(packagePath);
                var progress = new Progress<ZipProgress>();
                progress.ProgressChanged += (s, z) => MainDataModel.Default.ProgressBarState.SetProgressBarProgress(currentProgress: z.Processed, totalProgress: z.Total);
                await Task.Run(() => new ZipArchive(fileStream).ExtractToDirectory(outputDirectoryPath, progress, CancelSource));
                fileStream.Close();
                File.Delete(Path.Combine(outputDirectoryPath, "AppxSignature.p7x"));
                File.Move(packagePath, Path.Combine(MainDataModel.Default.FilePaths.VersionsFolder, "AppxBackups", packagePath));
                Trace.WriteLine("Extracted successfully");
                await Task.Run(Program.OnApplicationRefresh);
                foreach (var ver in MainDataModel.Default.Versions) ver.UpdateFolderSize();
            }
            catch (PackageManagerException e)
            {
                SetException(e);
            }
            catch (Exception e)
            {
                SetException(new PackageAddFailedException(e));
            }
            finally
            {
                EndTask();
            }
        }
        public async Task DownloadPackage(MCVersion v)
        {
            try
            {
                StartTask();
                await DownloadAndExtractPackage(v);
            }
            catch (PackageManagerException e)
            {
                SetException(e);
            }
            catch (Exception e)
            {
                SetException(new PackageDownloadAndExtractFailedException(e));
            }
            finally
            {
                EndTask();
            }
        }
        public void Cancel()
        {
            if (CancelSource != null && !CancelSource.IsCancellationRequested) CancelSource.Cancel();
        }

        #endregion

        #region Private Throwable Methods

        private async Task<bool> TryLaunchMinecraftAsync(VersionType type, bool launchEditor)
        {
            if (await TryLaunchMinecraftByUriAsync(type, launchEditor))
            {
                return true;
            }

            if (launchEditor)
            {
                return false;
            }

            Trace.WriteLine($"Failed to open {Constants.GetUri(type)} URI. Trying installed Minecraft packages instead.");
            return await TryLaunchInstalledMinecraftAsync(type);
        }

        private static async Task<bool> TryLaunchMinecraftByUriAsync(VersionType type, bool launchEditor)
        {
            try
            {
                return await Launcher.LaunchUriAsync(new Uri($"{Constants.GetUri(type)}:?Editor={launchEditor}"));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Minecraft URI launch failed: {ex}");
                return false;
            }
        }

        private async Task<bool> TryLaunchInstalledMinecraftAsync(VersionType type)
        {
            foreach (Package package in GetInstalledMinecraftPackages(type))
            {
                if (await TryLaunchPackageEntriesAsync(package))
                {
                    return true;
                }
            }

            try
            {
                var diagnosticPackages =
                    await AppDiagnosticInfo.RequestInfoForPackageAsync(Constants.GetPackageFamily(type));

                foreach (AppDiagnosticInfo diagnosticPackage in diagnosticPackages)
                {
                    try
                    {
                        AppActivationResult activationResult = await diagnosticPackage.LaunchAsync();
                        if (activationResult != null)
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Diagnostic launch failed for package '{diagnosticPackage.AppInfo.AppUserModelId}': {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppDiagnosticInfo launch fallback failed: {ex}");
            }

            foreach (Package package in GetInstalledMinecraftPackages(type))
            {
                if (TryLaunchInstalledExecutable(package))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> TryLaunchPackageEntriesAsync(Package package)
        {
            try
            {
                var appEntries = await package.GetAppListEntriesAsync();
                foreach (var appEntry in appEntries)
                {
                    if (await appEntry.LaunchAsync())
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppListEntry launch failed for package '{package.Id.FullName}': {ex}");
            }

            return false;
        }

        private static bool TryLaunchInstalledExecutable(Package package)
        {
            foreach (string executablePath in GetInstalledExecutablePaths(package))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = executablePath,
                        WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Direct executable launch failed for '{executablePath}': {ex}");
                }
            }

            return false;
        }

        private async Task GetGameHandle(IEnumerable<string> processNames)
        {
            await Task.Run(() =>
            {
                try
                {
                    string[] candidateNames = processNames?
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray() ?? Array.Empty<string>();

                    if (candidateNames.Length == 0)
                    {
                        candidateNames = new[] { Constants.MINECRAFT_PROCESS_NAME };
                    }

                    Process[] minecraftProcesses = Array.Empty<Process>();
                    Stopwatch waitTimer = Stopwatch.StartNew();

                    while (waitTimer.Elapsed < TimeSpan.FromSeconds(35))
                    {
                        minecraftProcesses = candidateNames
                            .SelectMany(Process.GetProcessesByName)
                            .GroupBy(process => process.Id)
                            .Select(group => group.First())
                            .ToArray();

                        if (minecraftProcesses.Length > 0)
                        {
                            break;
                        }

                        Thread.Sleep(500);
                    }

                    if (minecraftProcesses.Length >= 1)
                    {
                        MainDataModel.Default.ProgressBarState.SetGameRunningStatus(true);
                        GameHandle = minecraftProcesses[0];
                        GameHandle.EnableRaisingEvents = true;
                        GameHandle.Exited += OnPackageExit;


                        void OnPackageExit(object sender, EventArgs e)
                        {
                            Process p = sender as Process;
                            p.Exited -= OnPackageExit;
                            GameHandle = null;
                            MainDataModel.Default.ProgressBarState.SetGameRunningStatus(false);
                        }

                        Trace.WriteLine("Successfully attached Minecraft process");
                    }
                    else
                    {
                        Trace.WriteLine("Minecraft launch request was sent, but no game process was found before timeout.");
                        GameHandle = null;
                        MainDataModel.Default.ProgressBarState.SetGameRunningStatus(false);
                    }
                }
                catch (InvalidOperationException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    throw new PackageProcessHookFailedException(e);
                }
            });

        }

        private IEnumerable<Package> GetInstalledMinecraftPackages(VersionType type)
        {
            try
            {
                return PM.FindPackagesForUser(string.Empty, Constants.GetPackageFamily(type))
                    .OrderByDescending(pkg => pkg.Id.Version.Major)
                    .ThenByDescending(pkg => pkg.Id.Version.Minor)
                    .ThenByDescending(pkg => pkg.Id.Version.Build)
                    .ThenByDescending(pkg => pkg.Id.Version.Revision)
                    .ToList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to enumerate Minecraft packages: {ex}");
                return Enumerable.Empty<Package>();
            }
        }

        private static string GetInstalledLocationPath(Package package)
        {
            try
            {
                return package?.InstalledLocation?.Path ?? string.Empty;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Could not read package location for '{package?.Id.FullName}': {ex.Message}");
                return string.Empty;
            }
        }

        private IEnumerable<string> GetKnownMinecraftProcessNames(VersionType type)
        {
            HashSet<string> processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Constants.MINECRAFT_PROCESS_NAME
            };

            foreach (Package package in GetInstalledMinecraftPackages(type))
            {
                foreach (string executablePath in GetInstalledExecutablePaths(package))
                {
                    string processName = Path.GetFileNameWithoutExtension(executablePath);
                    if (!string.IsNullOrWhiteSpace(processName))
                    {
                        processNames.Add(processName);
                    }
                }
            }

            return processNames;
        }

        private static IEnumerable<string> GetInstalledExecutablePaths(Package package)
        {
            string installPath = GetInstalledLocationPath(package);
            if (string.IsNullOrWhiteSpace(installPath))
            {
                yield break;
            }

            string manifestPath = Path.Combine(installPath, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
            {
                yield break;
            }

            XDocument manifestDocument;
            try
            {
                manifestDocument = XDocument.Load(manifestPath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Could not read package manifest '{manifestPath}': {ex}");
                yield break;
            }

            foreach (XElement applicationElement in manifestDocument.Descendants().Where(x => x.Name.LocalName == "Application"))
            {
                string executableRelativePath = applicationElement.Attribute("Executable")?.Value;
                if (string.IsNullOrWhiteSpace(executableRelativePath))
                {
                    continue;
                }

                string executablePath = Path.Combine(installPath, executableRelativePath);
                if (File.Exists(executablePath))
                {
                    yield return executablePath;
                }
            }
        }

        private async Task DownloadAndExtractPackage(MCVersion v)
        {
            //MCVersion debugGDKVersion = new MCVersion("", "", "1.21.120", VersionType.Release, "x64");

            try
            {
                Trace.WriteLine($"Download start: {v.PackageID}");
                SetCancelation(true);

                string subDirectory = Path.Combine(MainDataModel.Default.FilePaths.VersionsFolder, "AppxBackups");
                if (!Directory.Exists(subDirectory))
                {
                    Directory.CreateDirectory(subDirectory);
                }

                string dlPath = "Minecraft-" + v.Name + ".Appx";
                string bkpsPath = Path.Combine(subDirectory, dlPath);
                string pkgPath = File.Exists(bkpsPath) ? bkpsPath : dlPath;

                if (!File.Exists(bkpsPath)) await DownloadPackage(v, dlPath, CancelSource);
                await ExtractPackage(v, dlPath, bkpsPath, pkgPath, CancelSource);

                v.UpdateFolderSize();
            }
            catch (PackageManagerException e)
            {
                ResetTask();
                throw e;
            }
            catch (Exception ex)
            {
                ResetTask();
                throw new Exception("DownloadAndExtractPackage Failed", ex);
            }
            finally
            {
                ResetTask();
                SetCancelation(false);
                CancelSource = null;
            }

        }
        private async Task DownloadPackage(MCVersion v, string dlPath, CancellationTokenSource cancelSource)
        {
            try
            {
                if (v.IsBeta) await AuthenticateBetaUser();
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isDownloading);
                Trace.WriteLine("Download starting");
                await VersionDownloader.DownloadVersion(v.DisplayName, v.PackageID, 1, dlPath, (x, y) => ProgressWrapper(x, y), cancelSource.Token, v.Type);
                Trace.WriteLine("Download complete");
            }
            catch (PackageManagerException e)
            {
                ResetTask();
                throw e;
            }
            catch (TaskCanceledException e)
            {
                ResetTask();
                throw new PackageDownloadCanceledException(e);
            }
            catch (Exception e)
            {
                ResetTask();
                throw new PackageDownloadFailedException(e);
            }
            finally
            {
                ResetTask();
            }
        }
        private async Task RegisterPackage(MCVersion v)
        {
            try
            {
                Trace.WriteLine("Registering package");
                MainDataModel.Default.ProgressBarState.SetProgressBarText(v.GetPackageNameFromMainifest());
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isRegisteringPackage);
                await DeploymentProgressWrapper(PM.RegisterPackageAsync(new Uri(v.ManifestPath), null, Constants.PackageDeploymentOptions));
                Trace.WriteLine("App re-register done!");
            }
            catch (PackageManagerException e)
            {
                ResetTask();
                throw e;
            }
            catch (Exception e)
            {
                ResetTask();
                throw new PackageRegistrationFailedException(e);
            }
            finally
            {
                ResetTask();
            }

        }
        private async Task ExtractPackage(MCVersion v, string dlPath, string bkpsPath, string pkgPath, CancellationTokenSource cancelSource)
        {
            try
            {
                Trace.WriteLine("Extraction started");
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isExtracting);

                if (Directory.Exists(v.GameDirectory))
                    await DirectoryExtensions.DeleteAsync(v.GameDirectory, (x, y, phase) => ProgressWrapper(x, y, phase));

                using var fileStream = File.OpenRead(pkgPath);
                var progress = new Progress<ZipProgress>();
                progress.ProgressChanged += (s, z) => MainDataModel.Default.ProgressBarState.SetProgressBarProgress(currentProgress: z.Processed, totalProgress: z.Total);
                await Task.Run(() =>
                {
                    using var zipArchive = new ZipArchive(fileStream);
                    zipArchive.ExtractToDirectory(v.GameDirectory, progress, cancelSource);
                });

                await File.WriteAllTextAsync(v.IdentificationPath, v.PackageID);
                File.Delete(Path.Combine(v.GameDirectory, "AppxSignature.p7x"));

                if (!File.Exists(bkpsPath))
                {
                    if (Properties.LauncherSettings.Default.KeepAppx)
                        File.Move(dlPath, bkpsPath);
                    else
                        File.Delete(dlPath);
                }

                Trace.WriteLine("Extracted successfully");
            }
            catch (PackageManagerException e)
            {
                ResetTask();
                throw e;
            }
            catch (TaskCanceledException e)
            {
                MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isCanceling);
                await DirectoryExtensions.DeleteAsync(v.GameDirectory, (x, y, phase) => ProgressWrapper(x, y, phase));
                ResetTask();
                throw new PackageExtractionCanceledException(e);
            }
            catch (Exception e)
            {
                ResetTask();
                throw new PackageExtractionFailedException(e);
            }
            finally
            {
                ResetTask();
            }
        }
        private async Task UnregisterPackage(MCVersion v, bool keepVersion = false, bool mustMatchVersion = false)
        {
            try
            {
                foreach (var pkg in PM.FindPackagesForUser(string.Empty, Constants.GetPackageFamily(v.Type)))
                {
                    string location;

                    try { location = pkg.InstalledLocation.Path; }
                    catch (FileNotFoundException) { location = string.Empty; }

                    if (location == v.GameDirectory && keepVersion)
                    {
                        Trace.WriteLine("Skipping package removal - same path: " + pkg.Id.FullName + " " + location);
                        continue;
                    }

                    if (location != v.GameDirectory && mustMatchVersion) continue;

                    Trace.WriteLine("Removing package: " + pkg.Id.FullName);

                    MainDataModel.Default.ProgressBarState.SetProgressBarText(pkg.Id.FullName);
                    MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isRemovingPackage);
                    await DeploymentProgressWrapper(PM.RemovePackageAsync(pkg.Id.FullName, Constants.PackageRemovalOptions));
                    Trace.WriteLine("Removal of package done: " + pkg.Id.FullName);
                }
            }
            catch (PackageManagerException e)
            {
                ResetTask();
                throw e;
            }
            catch (Exception ex)
            {
                ResetTask();
                throw new PackageDeregistrationFailedException(ex);
            }
            finally
            {
                ResetTask();
            }
        }
        private async Task RedirectSaveData(string InstallationsFolderPath, VersionType type)
        {
            await Task.Run(() =>
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                    string LocalStateFolder = Path.Combine(localAppData, "Packages", Constants.GetPackageFamily(type), "LocalState");
                    string PackageFolder = Path.Combine(localAppData, "Packages", Constants.GetPackageFamily(type), "LocalState", "games", "com.mojang");
                    string PackageBakFolder = Path.Combine(localAppData, "Packages", Constants.GetPackageFamily(type), "LocalState", "games", "com.mojang.default");
                    string ProfileFolder = Path.GetFullPath(InstallationsFolderPath);

                    string RequiredDir = Directory.GetParent(PackageFolder).FullName;
                    if (Directory.Exists(PackageFolder)) Directory.Delete(PackageFolder, true);
                    if (!Directory.Exists(RequiredDir)) Directory.CreateDirectory(RequiredDir);
                    DirectoryInfo profileDir = Directory.CreateDirectory(ProfileFolder);
                    
                    bool linkCreated = TryCreatePackageProfileLink(PackageFolder, ProfileFolder);
                    if (!linkCreated)
                    {
                        throw new SaveRedirectionFailedException(
                            new Exception("Failed to connect the Minecraft save folder to the selected profile directory."));
                    }
                    
                    DirectoryInfo pkgDir = Directory.CreateDirectory(PackageFolder);
                    DirectoryInfo lsDir = Directory.CreateDirectory(LocalStateFolder);

                    SecurityIdentifier owner = WindowsIdentity.GetCurrent().User;
                    SecurityIdentifier authenticated_users_identity = new SecurityIdentifier("S-1-5-11");

                    FileSystemAccessRule owner_access_rules = new FileSystemAccessRule(owner, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow);
                    FileSystemAccessRule au_access_rules = new FileSystemAccessRule(authenticated_users_identity, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow);

                    var lsSecurity = lsDir.GetAccessControl();
                    AuthorizationRuleCollection rules = lsSecurity.GetAccessRules(true, true, typeof(NTAccount));
                    List<FileSystemAccessRule> needed_rules = new List<FileSystemAccessRule>();
                    foreach (AccessRule rule in rules)
                    {
                        if (rule.IdentityReference is SecurityIdentifier)
                        {
                            var required_rule = new FileSystemAccessRule(rule.IdentityReference, FileSystemRights.FullControl, rule.InheritanceFlags, rule.PropagationFlags, rule.AccessControlType);
                            needed_rules.Add(required_rule);
                        }
                    }

                    var pkgSecurity = pkgDir.GetAccessControl();
                    pkgSecurity.SetOwner(owner);
                    pkgSecurity.AddAccessRule(au_access_rules);
                    pkgSecurity.AddAccessRule(owner_access_rules);
                    pkgDir.SetAccessControl(pkgSecurity);

                    var profileSecurity = profileDir.GetAccessControl();
                    //profileSecurity.SetOwner(owner);
                    profileSecurity.AddAccessRule(au_access_rules);
                    profileSecurity.AddAccessRule(owner_access_rules);
                    needed_rules.ForEach(x => profileSecurity.AddAccessRule(x));
                    profileDir.SetAccessControl(profileSecurity);
                }
                catch (PackageManagerException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    throw new SaveRedirectionFailedException(e);
                }
            });

        }
        private async Task AuthenticateBetaUser()
        {
            try
            {
                var userIndex = Properties.LauncherSettings.Default.CurrentInsiderAccountIndex;
                var token = await Task.Run(() => AuthenticationManager.Default.GetWUToken(userIndex));
                StoreNetwork.setMSAUserToken(token);
            }
            catch (PackageManagerException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Error while Authenticating UserToken for Version Fetching:\n" + e); //TODO: Localize Error Message
                throw new BetaAuthenticationFailedException(e);
            }
        }

        private static bool TryCreatePackageProfileLink(string linkPath, string targetPath)
        {
            if (SymLinkHelper.CreateSymbolicLinkSafe(linkPath, targetPath, SymLinkHelper.SymbolicLinkType.Directory))
            {
                Trace.WriteLine("Save data redirection created with symbolic link support.");
                return true;
            }

            return TryCreateDirectoryJunction(linkPath, targetPath);
        }

        private static bool TryCreateDirectoryJunction(string linkPath, string targetPath)
        {
            try
            {
                ProcessStartInfo junctionInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(linkPath) ?? AppContext.BaseDirectory
                };

                using Process junctionProcess = Process.Start(junctionInfo);
                junctionProcess?.WaitForExit();

                bool created = junctionProcess != null && junctionProcess.ExitCode == 0 && Directory.Exists(linkPath);
                Trace.WriteLine(created
                    ? "Save data redirection created with directory junction fallback."
                    : "Directory junction fallback failed.");
                return created;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Directory junction fallback failed: {ex}");
                return false;
            }
        }
        #endregion

        #region Helpers

        protected async Task DeploymentProgressWrapper(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> t)
        {
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            t.Progress += (v, p) => MainDataModel.Default.ProgressBarState.SetProgressBarProgress(currentProgress: Convert.ToInt64(p.percentage), totalProgress: 100);
            t.Completed += (v, p) =>
            {
                MainDataModel.Default.ProgressBarState.ResetProgressBarProgress();

                if (p == AsyncStatus.Error)
                {
                    Trace.WriteLine("Deployment failed: " + v.GetResults().ErrorText);
                    src.SetException(new Exception("Deployment failed: " + v.GetResults().ErrorText));
                }
                else
                {
                    Trace.WriteLine("Deployment done: " + p);
                    src.SetResult(1);
                }
            };
            await src.Task;
        }
        protected void ProgressWrapper(long current, long total, string text = null)
        {
            MainDataModel.Default.ProgressBarState.SetProgressBarProgress(current, total);
            MainDataModel.Default.ProgressBarState.SetProgressBarText(text);
        }
        protected void ResetTask()
        {
            MainDataModel.Default.ProgressBarState.ResetProgressBarProgress();
            MainDataModel.Default.ProgressBarState.SetProgressBarText();
            MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.None);
        }
        protected void EndTask()
        {
            MainDataModel.Default.ProgressBarState.ResetProgressBarProgress();
            MainDataModel.Default.ProgressBarState.SetProgressBarText();
            MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.None);
            MainDataModel.Default.ProgressBarState.SetProgressBarVisibility(false);
        }
        protected void StartTask()
        {
            MainDataModel.Default.ProgressBarState.SetProgressBarState(LauncherState.isInitializing);
            MainDataModel.Default.ProgressBarState.SetProgressBarVisibility(true);

        }
        protected void SetCancelation(bool cancelState)
        {
            if (cancelState) CancelSource = new CancellationTokenSource();
            MainDataModel.Default.ProgressBarState.AllowCancel = cancelState ? true : false;
            MainDataModel.Default.ProgressBarState.CancelCommand = cancelState ? new RelayCommand((o) => Cancel()) : null;
        }
        protected void SetException(Exception e)
        {
            if (e.GetType() == typeof(PackageExtractionFailedException)) SetError(e, "Extraction failed", "Error_AppExtractionFailed_Title", "Error_AppExtractionFailed");
            else if (e.GetType() == typeof(PackageDownloadFailedException)) SetError(e, "Download failed", "Error_AppDownloadFailed_Title", "Error_AppDownloadFailed");
            else if (e.GetType() == typeof(BetaAuthenticationFailedException)) SetError(e, "Authentication failed", "Error_AuthenticationFailed_Title", "Error_AuthenticationFailed");
            else if (e.GetType() == typeof(AppLaunchFailedException)) SetError(e, "App launch failed", "Error_AppLaunchFailed_Title", "Error_AppLaunchFailed");
            else if (e.GetType() == typeof(PackageRegistrationFailedException)) SetError(e, "App registeration failed", "Error_AppReregisterFailed_Title", "Error_AppReregisterFailed");
            else if (e.GetType() == typeof(PackageRemovalFailedException)) SetError(e, "App uninstall failed", "Error_AppUninstallFailed_Title", "Error_AppUninstallFailed");
            else if (e.GetType() == typeof(SaveRedirectionFailedException)) SetError(e, "Save redirection failed", "Error_SaveDirectoryRedirectionFailed_Title", "Error_SaveDirectoryRedirectionFailed");
            else if (e.GetType() == typeof(PackageDeregistrationFailedException)) SetError(e, "App deregisteration failed", "Error_AppDeregisteringFailed_Title", "Error_AppDeregisteringFailed");

            else if (e.GetType() == typeof(PackageDownloadAndExtractFailedException)) SetGenericError(e);
            else if (e.GetType() == typeof(PackageProcessHookFailedException)) SetGenericError(e);

            else if (e.GetType() == typeof(PackageExtractionCanceledException)) CancelAction();
            else if (e.GetType() == typeof(PackageDownloadCanceledException)) CancelAction();

            else SetGenericError(e);

            void CancelAction()
            {
                SetCancelation(false);
            }

            void SetGenericError(Exception ex)
            {
                _ = MainDataModel.BackwardsCommunicationHost.exceptionmsg(ex);
            }

            void SetError(Exception ex2, string debugMessage, string dialogTitle, string dialogText)
            {
                Trace.WriteLine(debugMessage + ":\n" + ex2.ToString());
                MainDataModel.BackwardsCommunicationHost.errormsg(dialogTitle, dialogText, ex2);
            }
        }

        private static string GetFileHash(string filePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            CancelSource?.Dispose();
        }

        #endregion







    }
}
