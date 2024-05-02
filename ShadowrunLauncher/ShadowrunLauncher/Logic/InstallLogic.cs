using ShadowrunLauncher.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ShadowrunLauncher.Logic
{
    enum LauncherStatus
    {
        ready, download, failed, downloadingGame, downloadingUpdate
    }
    internal class InstallLogic
    {
        internal string rootPath;
        internal string onlineBuildZip = @"C:\Users\sfish\OneDrive\Desktop\build\build.zip";
        internal string onlineGfwlZip = @"C:\Users\sfish\OneDrive\Desktop\build\gfwlivesetup.zip";
        internal string onlineVersionFile = @"C:\Users\sfish\OneDrive\Desktop\build\version.txt";
        internal string directXInstall = @"https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
        internal string gfwlProgramFileExe = @"C:\Program Files (x86)\Microsoft Games for Windows - LIVE\Client\GFWLive.exe";
        internal string releasefolderName = "shadowrun";
        internal string gameZipFileName = "build.zip";
        internal string gfwlZipFileName = "gfwlivesetup.zip";
        internal string directXInstallFileName = "dxwebsetup.exe";
        internal string versionFileName = "version.txt";
        internal string gameExeFileName = "Shadowrun.exe";
        internal string gfwlExeFileName = "gfwlivesetup.exe";
        internal string releaseFilesPath;
        internal string gameZip;
        internal string gfwlZip;
        internal string gameExe;
        internal string gfwlExe;
        internal string directXExe;
        internal string localVersionFile;
        private LauncherStatus _status;
        private MainWindow _mainWindow;

        public InstallLogic(MainWindow mainWindow)
        {
            rootPath = Directory.GetCurrentDirectory();
            releaseFilesPath = Path.Combine(rootPath, releasefolderName);
            gameZip = Path.Combine(rootPath, gameZipFileName);
            gfwlZip = Path.Combine(rootPath, gfwlZipFileName);
            gameExe = Path.Combine(releaseFilesPath, gameExeFileName);
            gfwlExe = Path.Combine(releaseFilesPath, gfwlExeFileName);
            directXExe = Path.Combine(releaseFilesPath, directXInstallFileName);
            localVersionFile = Path.Combine(releaseFilesPath, versionFileName);
            _mainWindow = mainWindow;
        }

        internal void PlayButtonClickLogic(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                try
                {
                    // For PCID Change
                    string srPcidBackupValue = RegistryLogic.GetSrPcidBackupFromRegistry();
                    if (!string.IsNullOrEmpty(srPcidBackupValue))
                    {
                        string uawPcidHex = HelperMethods.DecimalToHexFormat(int.Parse(srPcidBackupValue));
                        Console.WriteLine($"UAWPCIDBACKUP from registry (decimal): {srPcidBackupValue}");
                        Console.WriteLine($"UAWPCIDBACKUP from registry (hex format): {uawPcidHex}");
                        RegistryLogic.SetPcidInRegistry(srPcidBackupValue);
                        RegistryLogic.DeleteSrPcidBackupFromRegistry();
                    }
                    ProcessStartInfo startInfo = new ProcessStartInfo(gameExe)
                    {
                        WorkingDirectory = releaseFilesPath
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception occurred: {ex}");
                }

                //Close();
            }
            else if (Status == LauncherStatus.download)
            {
                CheckForUpdates();
            }
            /*else
            {
                CheckForUpdates();
            }*/
        }

        internal void CheckForUpdates()
        {
            if (File.Exists(localVersionFile))
            {
                GameVersion localVersion = new GameVersion(File.ReadAllText(localVersionFile));
                _mainWindow.VersionText.Content = localVersion.ToString();
                try
                {
                    WebClient webClient = new WebClient();
                    GameVersion onlineVersion = new GameVersion(webClient.DownloadString(onlineVersionFile));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else if (Status == LauncherStatus.download)
            {
                Status = LauncherStatus.download;
                InstallGameFiles(false, GameVersion.zero);
            }
            else
            {
                Status = LauncherStatus.download;
                //InstallGameFiles(false, Version.zero);
            }
        }

        private void InstallGameFiles(bool _isUpdate, GameVersion _onlineVersion)
        {
            try
            {
                WebClient webClientGame = new WebClient();
                WebClient webClientGfwl = new WebClient();
                WebClient webClientDirectX = new WebClient();
                if (!_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new GameVersion(webClientGame.DownloadString(onlineVersionFile));
                }
                webClientGame.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClientGame.DownloadFileAsync(new Uri(onlineBuildZip), gameZip, _onlineVersion);

                // if the user doesn't already have gfwl install it
                if (File.Exists(gfwlProgramFileExe) == false)
                {
                    webClientGfwl.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGfwlCompletedCallback);
                    webClientGfwl.DownloadFileAsync(new Uri(onlineGfwlZip), gfwlZip);
                }

                if (!IsDirectX9Installed())
                {
                    webClientDirectX.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadDirectXCompletedCallback);
                    webClientDirectX.DownloadFileAsync(new Uri(directXInstall), directXInstallFileName);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }
        private void DownloadDirectXCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                Console.WriteLine($"Attempting to run: {directXInstallFileName}");

                if (Directory.Exists(releaseFilesPath))
                {
                    if (File.Exists(directXExe))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(directXExe);
                        startInfo.Verb = "runas"; // Run as administrator
                        Process directxProcess = Process.Start(startInfo);

                        // Wait for the process to finish
                        directxProcess.WaitForExit();

                        // Close the process
                        directxProcess.Close();
                    }
                    else
                    {
                        Status = LauncherStatus.failed;
                        MessageBox.Show("DirectX exe not found in releases directory", "Warning", MessageBoxButton.OK);
                    }
                }
                else
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show("Your game is not installed", "Warning", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing DirectX download: {ex}");
            }
        }
        private void DownloadGfwlCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                ZipArchiveExtensions.ExtractToDirectory(sourceDirectoryName: gfwlZip, destinationDirectoryName: releaseFilesPath, overwrite: true);
                File.Delete(gfwlZip);

                Console.WriteLine($"Attempting to run: {gfwlExe}");

                if (Directory.Exists(releaseFilesPath))
                {
                    if (File.Exists(gfwlExe))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(gfwlExe);
                        startInfo.Verb = "runas"; // Run as administrator
                        Process gfwlProcess = Process.Start(startInfo);

                        // Wait for the process to finish
                        gfwlProcess.WaitForExit();

                        // Close the process
                        gfwlProcess.Close();
                    }
                    else
                    {
                        Status = LauncherStatus.failed;
                        MessageBox.Show("GFWL exe not found in releases directory", "Warning", MessageBoxButton.OK);
                    }
                }
                else
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show("Your game is not installed", "Warning", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing GFWL download: {ex}");
            }
        }
        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {

            try
            {
                string onlineVersion = ((GameVersion)e.UserState).ToString();
                ZipArchiveExtensions.ExtractToDirectory(sourceDirectoryName: gameZip, destinationDirectoryName: releaseFilesPath, overwrite: true);
                File.Delete(gameZip);

                File.WriteAllText(Path.Combine(releaseFilesPath, versionFileName), onlineVersion);

                _mainWindow.VersionText.Content = onlineVersion;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing game download: {ex}");
            }
        }

        public static bool IsDirectX9Installed()
        {
            string system32Directory = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "System32");
            bool foundD3dx9 = false;
            bool foundD3d9 = false;

            foreach (string filename in Directory.GetFiles(system32Directory, "*.dll"))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                if (fileNameWithoutExtension.StartsWith("d3dx9_", StringComparison.OrdinalIgnoreCase))
                {
                    foundD3dx9 = true;
                }
                else if (fileNameWithoutExtension.Equals("d3d9", StringComparison.OrdinalIgnoreCase))
                {
                    foundD3d9 = true;
                }

                if (foundD3dx9 && foundD3d9)
                {
                    return true; // Both DirectX 9 DLLs found
                }
            }
            return false; // Either or both DLLs are missing
        }

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        _mainWindow.PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.download:
                        _mainWindow.PlayButton.Content = "Download";
                        break;
                    case LauncherStatus.failed:
                        _mainWindow.PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.downloadingGame:
                        _mainWindow.PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        _mainWindow.PlayButton.Content = "Downloading Update";
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
