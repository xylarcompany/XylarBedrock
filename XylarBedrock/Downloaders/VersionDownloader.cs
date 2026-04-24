using XylarBedrock.Classes;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XylarBedrock.UpdateProcessor;
using XylarBedrock.UpdateProcessor.Databases;
using XylarBedrock.UpdateProcessor.Classes;
using System.Linq;
using JemExtensions;
using XylarBedrock.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using static XylarBedrock.UpdateProcessor.Handlers.VersionManager;
using XylarBedrock.UpdateProcessor.Handlers;
using System.Text.RegularExpressions;
using XylarBedrock.UpdateProcessor.Extensions;
using XylarBedrock.Enums;
using XylarBedrock.UpdateProcessor.Enums;
using System.Xml.Linq;

namespace XylarBedrock.Downloaders
{
    public class VersionDownloader
    {
        private VersionManager VersionDB = new VersionManager();

        private string winstoreDBFile => MainDataModel.Default.FilePaths.GetWinStoreVersionsDBFile();
        private string communityDBFile => MainDataModel.Default.FilePaths.GetCommunityVersionsDBFile();

        private MCVersion latestReleaseRef { get; set; }
        private MCVersion? latestBetaRef { get; set; }
        private MCVersion latestPreviewRef { get; set; }


        public async Task DownloadVersion(string versionName, string packageID, int revisionNumber, string destination, DownloadProgress progress, CancellationToken cancellationToken, VersionType versionType)
        {
            await VersionDB.DownloadVersion(versionName, GetUpdateIdentity(packageID), revisionNumber, destination, progress, cancellationToken, versionType);

            string GetUpdateIdentity(string packageID)
            {
                if (packageID == Constants.LATEST_BETA_UUID) return latestBetaRef.PackageID;
                else if (packageID == Constants.LATEST_RELEASE_UUID) return latestReleaseRef.PackageID;
                else if (packageID == Constants.LATEST_PREVIEW_UUID) return latestPreviewRef.PackageID;
                else return packageID;
            }
        }
        public async Task UpdateVersionList(ObservableCollection<MCVersion> versions, bool OnLoad = false)
        {
            bool AllowUpdating = OnLoad && Debugger.IsAttached ? Constants.Debugging.RetriveNewVersionsOnLoad : true;

            //Clear Existing Versions
            versions.Clear();

            //Retrive Versions
            int userIndex = Properties.LauncherSettings.Default.CurrentInsiderAccountIndex;
            VersionDB.Init(userIndex, winstoreDBFile, communityDBFile);
            await VersionDB.LoadVersions(true, Properties.LauncherSettings.Default.FetchVersionsFromMicrosoftStore);

            //Add Versions to ObservableCollection, then Sort them
            List<VersionInfoJson> versionList = VersionDB.GetVersions()
                .Where(entry => entry.GetVersionType() == VersionType.Release)
                .Where(entry => VersionDbExtensions.DoesVerionArchMatch(Constants.CurrentArchitecture, entry.GetArchitecture()))
                .ToList();

            foreach (VersionInfoJson entry in versionList)
            {
                versions.Add(new MCVersion(entry.GetUUID().ToString(), entry.GetUUID().ToString(), GetRealVersion(entry.GetVersion()), entry.GetVersionType(), entry.GetArchitecture()));
            }
                
            versions.Sort((x, y) => x.Compare(y));


            MCVersion latestRelease = versions.FirstOrDefault(x => x.IsRelease);
            this.latestReleaseRef = latestRelease ?? new MCVersion(Constants.LATEST_RELEASE_UUID, Constants.LATEST_RELEASE_UUID, "Minecraft for Windows", VersionType.Release, Constants.CurrentArchitecture);
            this.latestBetaRef = null;
            this.latestPreviewRef = null;

            MCVersion latest_release = new MCVersion(Constants.LATEST_RELEASE_UUID, Constants.LATEST_RELEASE_UUID, "Minecraft for Windows", VersionType.Release, Constants.CurrentArchitecture);
            versions.Insert(0, latest_release);

            await SyncUpLocalVersions(versions, OnLoad);

            string GetRealVersion(string versionS)
            {
                if (MinecraftVersion.TryParse(versionS, out MinecraftVersion version)) return version.ToRealString();
                else return new Version(0, 0, 0, 0).ToString();
            }
        }
        private async Task SyncUpLocalVersions(ObservableCollection<MCVersion> versions, bool OnLoad = false)
        {
            DirectoryInfo directoryInfo = Directory.CreateDirectory(MainDataModel.Default.FilePaths.VersionsFolder);
            var webVersions = VersionDB.GetVersions();

            foreach (var directory in directoryInfo.EnumerateDirectories())
            {
                string mainifest_file = Path.Combine(directory.FullName, MCVersionExtensions.MainifestFileName);
                string packageId_file = Path.Combine(directory.FullName, MCVersionExtensions.IdentificationFilename);
                string customName_file = Path.Combine(directory.FullName, "custom_name.txt");
                string uuid = directory.Name;

                try
                {
                    if (File.Exists(mainifest_file))
                    {
                        //Legacy Version Support
                        if (directory.Name.StartsWith("Minecraft-"))
                        {
                            string legacyPkgID = directory.Name.Replace("Minecraft-", "");
                            if (!File.Exists(packageId_file))
                            {
                                if (versions.Exists(x => x.PackageID == legacyPkgID))
                                    await File.WriteAllTextAsync(packageId_file, legacyPkgID);
                            }
                            directory.Rename(legacyPkgID);
                            uuid = legacyPkgID;
                        }

                        string packageID = await FileExtensions.TryReadAllTextAsync(packageId_file, null);

                        if (!versions.Exists(x => x.UUID == uuid && x.PackageID == packageID))
                        {
                            var customVersion = await GetAppxMaifestIdentity(packageID, uuid, mainifest_file);
                            string customNameFallback = string.Format("{0}.{1}.{2}", customVersion.Name, customVersion.Type.ToString().FirstOrDefault(), customVersion.Architecture);
                            customVersion.CustomName = await FileExtensions.TryReadAllTextAsync(customName_file, customNameFallback);
                            versions.Add(customVersion);
                        }

                    }
                }
                catch
                {
                    //TODO: Add Exception Handling
                }

            }
        }
        private async Task<MCVersion> GetAppxMaifestIdentity(string PackageID, string UUID, string file)
        {
            var (Name, Version, ProcessorArchitecture) = await MCVersionExtensions.GetCommonPackageValuesAsync(file);

            if (Name != "Microsoft.MinecraftUWP") throw new Exception("Only the official Minecraft for Windows package is supported.");

            return new MCVersion(UUID, PackageID, Version, VersionType.Release, ProcessorArchitecture);
        }
        public MCVersion GetVersion(VersioningMode versioningMode, string versionUUID)
        {
            if (versioningMode != VersioningMode.None)
            {
                if (versioningMode == VersioningMode.LatestRelease && latestReleaseRef != null)
                {
                    MCVersion? latest_release = MainDataModel.Default.Versions
                        .ToList().FirstOrDefault(x => x.UUID == latestReleaseRef.UUID && x.Type == latestReleaseRef.Type, null);
                    return latest_release;
                }
                else return null;
            }
            else if (MainDataModel.Default.Versions.ToList().Exists(x => x.UUID == versionUUID))
            {
                return MainDataModel.Default.Versions.ToList().Where(x => x.UUID == versionUUID).FirstOrDefault();
            }
            else return null;
        }
    }
}
