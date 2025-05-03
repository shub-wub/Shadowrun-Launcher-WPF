using ShadowrunLauncher.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShadowrunLauncher.Logic
{
    enum LauncherStatus
    {
        ready, download, failed, downloadingGame, downloadingUpdate
    }

    internal class InstallLogic
    {
        internal string rootPath;
        internal string onlineBuildZip = @"http://157.245.214.234/releases/build.zip";
        internal string onlineGfwlZip = @"http://157.245.214.234/releases/gfwlsetup.zip";
        internal string onlineVersionFile = @"http://157.245.214.234/releases/version.txt";
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
            _status = LauncherStatus.download;
            _mainWindow = mainWindow;
        }

        internal async void PlayButtonClickLogic()
        {
            if (IsGameInstalled() && Status == LauncherStatus.ready)
            {
                try
                {
                    string srPcidBackupValue = RegistryLogic.GetSrPcidBackupFromRegistry();
                    if (!string.IsNullOrEmpty(srPcidBackupValue))
                    {
                        string srPcidHex = HelperMethods.DecimalToHexFormat(long.Parse(srPcidBackupValue));
                        Console.WriteLine($"SRPCIDBACKUP from registry (decimal): {srPcidBackupValue}");
                        Console.WriteLine($"SRPCIDBACKUP from registry (hex format): {srPcidHex}");
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
            }
            else if (Status == LauncherStatus.download)
            {
                await CheckForUpdates(false);
            }
        }

        internal async Task CheckForUpdates(bool updateCheck)
        {
            if (File.Exists(localVersionFile) && updateCheck)
            {
                GameVersion localVersion = new GameVersion(File.ReadAllText(localVersionFile));
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.VersionText.Content = localVersion.ToString();
                });

                try
                {
                    using HttpClient client = new HttpClient();
                    GameVersion onlineVersion = new GameVersion(await client.GetStringAsync(onlineVersionFile));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        await InstallGameFiles(true, onlineVersion);
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
            else if (Status == LauncherStatus.download && !updateCheck)
            {
                Status = LauncherStatus.download;
                await InstallGameFiles(false, GameVersion.zero);
            }
        }

        private async Task InstallGameFiles(bool _isUpdate, GameVersion _onlineVersion)
        {
            try
            {
                _mainWindow.playButton.IsEnabled = false;
                Status = _isUpdate ? LauncherStatus.downloadingUpdate : LauncherStatus.downloadingGame;

                using HttpClient httpClient = new HttpClient();

                if (!_isUpdate)
                {
                    string versionStr = await httpClient.GetStringAsync(onlineVersionFile);
                    _onlineVersion = new GameVersion(versionStr);
                }

                var gameTask = DownloadFileAsync(httpClient, onlineBuildZip, gameZip);
                var gfwlTask = File.Exists(gfwlProgramFileExe)
                    ? Task.CompletedTask
                    : DownloadFileAsync(httpClient, onlineGfwlZip, gfwlZip);

                var dxTask = IsDirectX9Installed()
                    ? Task.CompletedTask
                    : DownloadFileAsync(httpClient, directXInstall, directXInstallFileName);

                await Task.WhenAll(gameTask, gfwlTask, dxTask);

                if (File.Exists(gfwlZip)) DownloadGfwlCompletedCallback();
                if (File.Exists(directXExe)) DownloadDirectXCompletedCallback();

                string onlineVersionStr = _onlineVersion.ToString();
                ZipArchiveExtensions.ExtractToDirectory(gameZip, releaseFilesPath, overwrite: true);
                File.Delete(gameZip);
                File.WriteAllText(Path.Combine(releaseFilesPath, versionFileName), onlineVersionStr);

                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.VersionText.Content = onlineVersionStr;
                });

                Status = LauncherStatus.ready;
                _mainWindow.playButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string outputPath)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        private void DownloadDirectXCompletedCallback()
        {
            try
            {
                Console.WriteLine($"Attempting to run: {directXInstallFileName}");

                if (Directory.Exists(releaseFilesPath) && File.Exists(directXExe))
                {
                    var startInfo = new ProcessStartInfo(directXExe) { Verb = "runas" };
                    var proc = Process.Start(startInfo);
                    proc.WaitForExit();
                    proc.Close();
                }
                else
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show("DirectX executable not found or game not installed.");
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing DirectX install: {ex}");
            }
        }

        private void DownloadGfwlCompletedCallback()
        {
            try
            {
                ZipArchiveExtensions.ExtractToDirectory(gfwlZip, releaseFilesPath, overwrite: true);
                File.Delete(gfwlZip);

                Console.WriteLine($"Attempting to run: {gfwlExe}");

                if (Directory.Exists(releaseFilesPath) && File.Exists(gfwlExe))
                {
                    var startInfo = new ProcessStartInfo(gfwlExe) { Verb = "runas" };
                    var proc = Process.Start(startInfo);
                    proc.WaitForExit();
                    proc.Close();
                }
                else
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show("GFWL executable not found or game not installed.");
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing GFWL install: {ex}");
            }
        }

        public bool IsGameInstalled()
        {
            if (!File.Exists(localVersionFile))
                return false;

            GameVersion localVersion = new GameVersion(File.ReadAllText(localVersionFile));
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.VersionText.Content = localVersion.ToString();
            });

            try
            {
                using HttpClient client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, onlineVersionFile);
                var response = client.Send(request);
                response.EnsureSuccessStatusCode();

                string versionString = response.Content.ReadAsStringAsync().Result;
                GameVersion onlineVersion = new GameVersion(versionString);

                return !onlineVersion.IsDifferentThan(localVersion);
            }
            catch
            {
                return false;
            }
        }


        public bool IsGfwlInstalled()
        {
            return File.Exists(gfwlProgramFileExe);
        }

        public bool IsDirectX9Installed()
        {
            string system32Directory = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "System32");
            bool foundD3dx9 = false, foundD3d9 = false;

            foreach (var file in Directory.GetFiles(system32Directory, "*.dll"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("d3dx9_", StringComparison.OrdinalIgnoreCase)) foundD3dx9 = true;
                else if (name.Equals("d3d9", StringComparison.OrdinalIgnoreCase)) foundD3d9 = true;

                if (foundD3dx9 && foundD3d9) return true;
            }
            return false;
        }

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                string text = _status switch
                {
                    LauncherStatus.ready => "Play",
                    LauncherStatus.download => "Download",
                    LauncherStatus.failed => "Update Failed - Retry",
                    LauncherStatus.downloadingGame => "Downloading Game",
                    LauncherStatus.downloadingUpdate => "Downloading Update",
                    _ => ""
                };

                _mainWindow.playButton.Dispatcher.Invoke(() =>
                {
                    ((TextBlock)((Grid)_mainWindow.playButton.Content).Children[1]).Text = text;
                });
            }
        }
    }
}
