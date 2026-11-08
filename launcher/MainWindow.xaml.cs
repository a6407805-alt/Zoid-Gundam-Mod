// ============================================================================================================
//  Zoid Gundam Launcher - MainWindow (Rusted Warfare Auto-Sync, Auto-Update & Steam Integration)
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ZoidGundamLauncher
{
    public partial class MainWindow : Window
    {
        private const string Repo = "a6407805-alt/Zoid-Gundam-Mod";
        private const string AssetName = "Zoid-Gundam-Mod.zip";
        private const string LegacyAssetName = "Zoid-Gundam-Collection.zip";
        private const string LauncherAssetName = "Zoid-Gundam-Mod.exe";

        /// <summary>Steam's URL for Rusted Warfare - RTS (app id 647960).</summary>
        private const string SteamLaunchUri = "steam://rungameid/647960";

        private static readonly string LauncherDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoidGundam", "Launcher");
        private static readonly string VersionFile = Path.Combine(LauncherDir, "version.txt");
        private static readonly string PackDir = Path.Combine(LauncherDir, "pack");
        private static readonly string DownloadFile = Path.Combine(LauncherDir, "download.zip.part");

        private static readonly string ExtrasDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoidGundam");
        private static readonly string ExtrasKeyFile = Path.Combine(ExtrasDir, "extras-key.txt");
        private static readonly string ExtrasSkipFile = Path.Combine(ExtrasDir, "extras-skip.txt");

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            };
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoidGundamLauncher", "1.0"));
            return client;
        }

        private sealed record ReleaseInfo(string Version, string DownloadUrl, long Size, DateTimeOffset Published, long LauncherSize = 0);

        private ReleaseInfo? _remote;
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            LocationChanged += DockSidePanels;
            SizeChanged += DockSidePanels;
        }

        // ================================================================== startup

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowExtrasState(!string.IsNullOrEmpty(Publisher.ReadSavedKey()) || File.Exists(ExtrasKeyFile));

            LoadBuiltInRoster();
            Task roster = RefreshRosterFromGitHubAsync();

            if (PreviewMode)
            {
                _ = LoadChangelogAsync();
                ShowInstalledVersion("preview");
                Ready("Installation Up to Date!");
                if (ScreenshotPath != null)
                {
                    await roster;
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    SaveScreenshot(ScreenshotPath);
                    Application.Current.Shutdown();
                }
                return;
            }

            _ = LoadChangelogAsync();

            if (await TrySelfUpdateAsync(fromRefresh: false)) return;

            // Auto-sync local edits from Desktop\Zoid Gundam and Rusted Warfare\mods\units\Zoid Gundam to GitHub first!
            await RunStagingAsync();
            if (_modsNotSent > 0)
            {
                Ready("Couldn't send " + _modsNotSent + " changed mod file(s) to GitHub, so the update was skipped to keep them safe.");
                return;
            }

            await CheckAndUpdateAsync();
            try { await SetUpSteamPlayAsync(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception)
            { Debug.WriteLine("Steam Play setup skipped: " + ex.Message); }

            if (PlayAfterUpdate && PlayButton.IsEnabled) LaunchGame();
        }

        private async Task CheckAndUpdateAsync(bool forceInstall = false)
        {
            if (_busy) return;
            _busy = true;
            RetryButton.Visibility = Visibility.Collapsed;
            PlayButton.IsEnabled = false;
            SetProgress(0, indeterminate: true);

            try
            {
                string? local = ReadLocalVersion();
                ShowInstalledVersion(local);
                SetStatus("Checking GitHub for updates...");

                try
                {
                    _remote = await GetLatestReleaseAsync();
                }
                catch (Exception ex) when (IsNetworkError(ex))
                {
                    if (local != null)
                    {
                        Ready("Can't reach GitHub right now. Playing the version you have.");
                        return;
                    }
                    Fail("Can't reach GitHub to download the pack. Check your internet and press Retry.", ex);
                    return;
                }

                bool installed = local != null && File.Exists(Path.Combine(PackDir, "install.ps1"));
                if (!forceInstall && installed && string.Equals(local, _remote.Version, StringComparison.OrdinalIgnoreCase))
                {
                    Ready("Zoid Gundam Up to Date & Synced!");
                    return;
                }

                if (!installed || !string.Equals(local, _remote.Version, StringComparison.OrdinalIgnoreCase))
                    await DownloadAndExtractAsync(_remote);
                await RunInstallerAsync();

                WriteLocalVersion(_remote.Version);
                ShowInstalledVersion(_remote.Version);
                Ready("Zoid Gundam Up to Date & Synced!");
            }
            catch (Exception ex)
            {
                Fail("Update failed: " + ex.Message, ex);
            }
            finally
            {
                _busy = false;
            }
        }

        // ================================================================== version checking

        private static string? ReadLocalVersion()
        {
            try
            {
                if (!File.Exists(VersionFile)) return null;
                string v = File.ReadAllText(VersionFile).Trim();
                return v.Length > 0 ? v : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static void WriteLocalVersion(string version)
        {
            Directory.CreateDirectory(LauncherDir);
            File.WriteAllText(VersionFile, version);
        }

        private static async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return await GetLatestReleaseOnceAsync(); }
                catch (ReleaseMissingException) when (attempt < 4) { await Task.Delay(TimeSpan.FromSeconds(5)); }
                catch (HttpRequestException)
                {
                    ReleaseInfo? viaFile = await GetLatestReleaseFromInfoFileAsync();
                    if (viaFile != null) return viaFile;
                    throw;
                }
            }
        }

        private sealed class ReleaseMissingException : InvalidOperationException
        {
            public ReleaseMissingException(string message) : base(message) { }
        }

        private static async Task<ReleaseInfo?> GetLatestReleaseFromInfoFileAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                string text = await Http.GetStringAsync($"https://github.com/{Repo}/releases/latest/download/release-info.json?t={DateTime.UtcNow.Ticks}", cts.Token);
                using JsonDocument json = JsonDocument.Parse(text);
                JsonElement r = json.RootElement;
                string version = r.GetProperty("version").GetString() ?? "";
                if (version.Length == 0) return null;
                DateTimeOffset published = r.TryGetProperty("published", out JsonElement p) && p.TryGetDateTimeOffset(out DateTimeOffset d) ? d : DateTimeOffset.MinValue;
                return new ReleaseInfo(version, $"https://github.com/{Repo}/releases/latest/download/{AssetName}",
                    r.GetProperty("zipSize").GetInt64(), published, r.TryGetProperty("exeSize", out JsonElement x) ? x.GetInt64() : 0);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        internal static void AddKeyIfAny(HttpRequestMessage request)
        {
            string? key = Publisher.ReadSavedKey();
            if (!string.IsNullOrEmpty(key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        private static async Task<ReleaseInfo> GetLatestReleaseOnceAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            AddKeyIfAny(request);

            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                throw new HttpRequestException("GitHub is rate-limiting this PC (60 checks an hour). Try again in a few minutes.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ReleaseMissingException("No release found for " + Repo + ".");
            response.EnsureSuccessStatusCode();

            await using Stream body = await response.Content.ReadAsStreamAsync(cts.Token);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);
            JsonElement root = json.RootElement;

            string version = root.TryGetProperty("target_commitish", out JsonElement sha) && sha.GetString() is { Length: > 0 } s
                ? s
                : root.GetProperty("published_at").GetString() ?? "unknown";
            DateTimeOffset published = root.TryGetProperty("published_at", out JsonElement p) && p.TryGetDateTimeOffset(out DateTimeOffset d)
                ? d : DateTimeOffset.MinValue;

            long launcherSize = 0;
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == LauncherAssetName) launcherSize = asset.GetProperty("size").GetInt64();
            }

            foreach (string wanted in new[] { AssetName, LegacyAssetName })
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != wanted) continue;
                return new ReleaseInfo(
                    version,
                    asset.GetProperty("browser_download_url").GetString() ?? throw new InvalidOperationException("The release has no download link."),
                    asset.GetProperty("size").GetInt64(),
                    published,
                    launcherSize);
            }
            throw new InvalidOperationException($"The latest release doesn't contain {AssetName}.");
        }

        // ================================================================== changelog

        private async Task LoadChangelogAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/commits?per_page=12");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                AddKeyIfAny(request);
                using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();

                await using Stream body = await response.Content.ReadAsStreamAsync(cts.Token);
                using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);

                var sb = new StringBuilder();
                foreach (JsonElement c in json.RootElement.EnumerateArray())
                {
                    JsonElement commit = c.GetProperty("commit");
                    string message = commit.GetProperty("message").GetString() ?? "";
                    string firstLine = message.Split('\n')[0].Trim();
                    if (firstLine.Length == 0 || firstLine.StartsWith("Merge ", StringComparison.Ordinal)) continue;

                    string date = commit.GetProperty("author").TryGetProperty("date", out JsonElement de) && de.TryGetDateTimeOffset(out DateTimeOffset dt)
                        ? dt.ToLocalTime().ToString("MMM d") : "";
                    sb.Append("•  ").Append(firstLine);
                    if (date.Length > 0) sb.Append("   (").Append(date).Append(')');
                    sb.AppendLine().AppendLine();
                }
                ChangelogText.Text = sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes listed.";
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                ChangelogText.Text = "Couldn't load the changelog (offline?). The launcher still works.";
            }
        }

        // ================================================================== downloading & extracting

        private async Task DownloadAndExtractAsync(ReleaseInfo release)
        {
            Directory.CreateDirectory(LauncherDir);
            SetStatus("Downloading the latest Zoid Gundam build...");
            SetProgress(0, indeterminate: false);

            var progress = new Progress<(long Done, long Total)>(p =>
            {
                double percent = p.Total > 0 ? p.Done * 100.0 / p.Total : 0;
                DownloadProgress.Value = percent;
                ProgressText.Text = $"{p.Done / 1048576.0:0.0} / {p.Total / 1048576.0:0.0} MB";
            });

            await DownloadFileAsync(release.DownloadUrl, DownloadFile, release.Size, progress);

            SetStatus("Unpacking Zoid Gundam Mod...");
            SetProgress(0, indeterminate: true);
            ProgressText.Text = "";
            await Task.Run(() =>
            {
                if (Directory.Exists(PackDir)) Directory.Delete(PackDir, recursive: true);
                Directory.CreateDirectory(PackDir);
                ZipFile.ExtractToDirectory(DownloadFile, PackDir, overwriteFiles: true);
                File.Delete(DownloadFile);
            });
        }

        private static async Task DownloadFileAsync(string url, string destination, long expectedSize, IProgress<(long, long)> progress)
        {
            using var stall = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? expectedSize;

            await using Stream source = await response.Content.ReadAsStreamAsync(stall.Token);
            await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long done = 0;
                long lastReport = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), stall.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), stall.Token);
                    done += read;
                    stall.CancelAfter(TimeSpan.FromSeconds(30));

                    if (done - lastReport >= 131072 || done == total)
                    {
                        lastReport = done;
                        progress.Report((done, total));
                    }
                }
                progress.Report((done, total));

                if (total > 0 && done != total)
                    throw new IOException($"The download was cut short ({done:N0} of {total:N0} bytes). Press Retry.");
            }
        }

        // ================================================================== installing into Rusted Warfare

        private async Task RunInstallerAsync()
        {
            string script = Path.Combine(PackDir, "install.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("The pack has no install.ps1.", script);

            SetStatus("Installing into Rusted Warfare...");
            SetProgress(0, indeterminate: true);

            string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -SkipExtras";

            var psi = new ProcessStartInfo("powershell.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = PackDir
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var errors = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                string line = e.Data.Trim();
                Dispatcher.InvokeAsync(() => SetStatus(line));
            };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (errors) errors.AppendLine(e.Data); };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException("Windows PowerShell couldn't be started: " + ex.Message, ex);
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string detail = errors.ToString().Trim();
                throw new InvalidOperationException("the installer stopped (code " + process.ExitCode + ")" +
                    (detail.Length > 0 ? ": " + detail.Split('\n')[0] : ". See install_log.txt in " + PackDir));
            }
        }

        // ================================================================== PLAY

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            PlayButton.IsEnabled = false;
            if (await TrySelfUpdateAsync(fromRefresh: true, thenPlay: true)) return;
            await RunStagingAsync();
            await CheckAndUpdateAsync();
            if (PlayButton.IsEnabled) LaunchGame();
        }

        private void LaunchGame()
        {
            try
            {
                if (SteamGameCommand(out string exe, out List<string> gameArgs))
                {
                    var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
                    foreach (string a in gameArgs) psi.ArgumentList.Add(a);
                    Process.Start(psi);
                }
                else Process.Start(new ProcessStartInfo(SteamLaunchUri) { UseShellExecute = true });
                Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(this, "Couldn't start Rusted Warfare: " + ex.Message + "\n\nIs Steam installed?",
                    "Zoid Gundam Mod", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static bool SteamGameCommand(out string exe, out List<string> rest)
        {
            exe = ""; rest = new List<string>();
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
            {
                if (!a[i].EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(a[i])) continue;
                exe = a[i];
                for (int j = i + 1; j < a.Length; j++) rest.Add(a[j]);
                return true;
            }
            return false;
        }

        // ================================================================== Steam Play button (opt-in) + desktop shortcut

        private static readonly string StableExe = Path.Combine(LauncherDir, "Zoid-Gundam-Mod.exe");
        private static readonly string SteamPlayFile = Path.Combine(ExtrasDir, "steam-play.txt");

        private static string EnsureStableCopy()
        {
            try
            {
                string? me = Environment.ProcessPath;
                if (me == null || string.Equals(Path.GetFullPath(me), Path.GetFullPath(StableExe), StringComparison.OrdinalIgnoreCase)) return StableExe;
                Directory.CreateDirectory(LauncherDir);
                var src = new FileInfo(me);
                var dst = new FileInfo(StableExe);
                if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc < src.LastWriteTimeUtc) File.Copy(me, StableExe, overwrite: true);
                return StableExe;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Environment.ProcessPath ?? StableExe;
            }
        }

        private async Task SetUpSteamPlayAsync()
        {
            string saved = File.Exists(SteamPlayFile) ? File.ReadAllText(SteamPlayFile).Trim() : "";
            SteamPlayToggle.IsChecked = saved == "on" || saved == "done";
            if (!File.Exists(Path.Combine(PackDir, "install.ps1"))) return;
            await RunSteamPlayScriptAsync("");
        }

        private async Task<string> RunSteamPlayScriptAsync(string extra)
        {
            string script = Path.Combine(PackDir, "install.ps1");
            string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -SteamPlayOnly -LauncherPath \"{EnsureStableCopy()}\" {extra}";
            var psi = new ProcessStartInfo("powershell.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using Process? p = Process.Start(psi);
            if (p == null) return "";
            string output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output;
        }

        private static string? SteamExe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                string? exe = key?.GetValue("SteamExe") as string;
                return exe != null && File.Exists(exe) ? exe : null;
            }
            catch (System.Security.SecurityException) { return null; }
        }

        private static bool SteamRunning() => Process.GetProcessesByName("steam").Length > 0;

        private async void SteamPlayToggle_Click(object sender, RoutedEventArgs e)
        {
            bool turnOn = SteamPlayToggle.IsChecked == true;
            void Revert() => SteamPlayToggle.IsChecked = !turnOn;

            if (SteamGameCommand(out _, out _))
            {
                MessageBox.Show(this, "Steam opened this launcher, so Steam can't be closed right now.\n\nOpen the launcher from its desktop shortcut to change this.",
                    "Zoid Gundam Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                Revert(); return;
            }
            if (!File.Exists(Path.Combine(PackDir, "install.ps1")))
            {
                MessageBox.Show(this, "Let the launcher finish installing first, then try again.", "Zoid Gundam Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                Revert(); return;
            }

            string? steam = SteamExe();
            bool restart = false;
            if (SteamRunning())
            {
                if (MessageBox.Show(this, "Steam has to close for a moment to save this (it would undo the change otherwise).\n\nClose Steam, save, and open it again?",
                        "Zoid Gundam Mod", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { Revert(); return; }
                SteamPlayToggle.IsEnabled = false;
                SetStatus("Closing Steam...");
                try { if (steam != null) Process.Start(steam, "-shutdown"); else Process.Start(new ProcessStartInfo("steam://exit") { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception) { }
                for (int i = 0; i < 60 && SteamRunning(); i++) await Task.Delay(500);
                if (SteamRunning())
                {
                    SetStatus("Steam didn't close. Close it yourself (Steam menu > Exit) and flip the switch again.");
                    SteamPlayToggle.IsEnabled = true; Revert(); return;
                }
                restart = true;
            }

            SteamPlayToggle.IsEnabled = false;
            SetStatus(turnOn ? "Pointing Steam's Rusted Warfare Play button at the launcher..." : "Putting Steam's Play button back to normal...");
            string output = await RunSteamPlayScriptAsync(turnOn ? "-SetSteamPlay" : "-UnsetSteamPlay");
            bool ok = output.Contains("STEAMPLAY_ON") || output.Contains("STEAMPLAY_OFF") || output.Contains("already set") || (!turnOn && !output.Contains("STEAM_OPEN"));

            if (output.Contains("STEAMPLAY_KEPT"))
            {
                MessageBox.Show(this, "You already have your own Launch Options for Rusted Warfare in Steam, so they were left alone.\n\n" +
                    "To use this anyway: Steam > Rusted Warfare > Properties > clear Launch Options, then flip the switch again.",
                    "Zoid Gundam Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                ok = false;
            }
            if (ok)
            {
                Directory.CreateDirectory(ExtrasDir);
                File.WriteAllText(SteamPlayFile, turnOn ? "on" : "off");
                SetStatus(turnOn ? "Done: Steam's Play button for Rusted Warfare now opens this launcher." : "Done: Steam's Play button starts Rusted Warfare normally again.");
            }
            else
            {
                Revert();
                if (!output.Contains("STEAMPLAY_KEPT"))
                    SetStatus("Couldn't change Steam's settings. " + (output.Contains("STEAM_OPEN") ? "Steam is still open." : "Has Rusted Warfare been played on this Steam account?"));
            }
            if (restart && steam != null) { try { Process.Start(steam); } catch (System.ComponentModel.Win32Exception) { } }
            SteamPlayToggle.IsEnabled = true;
        }

        // ================================================================== creator & guide tools

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || PreviewMode) return;
            RefreshButton.IsEnabled = false;
            try
            {
                Task roster = RefreshRosterFromGitHubAsync();
                Task changelog = LoadChangelogAsync();
                if (await TrySelfUpdateAsync(fromRefresh: true)) return;
                await RunStagingAsync();
                await CheckAndUpdateAsync();
                await roster;
                await changelog;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private GuideWindow? _guide;
        private PublishWindow? _creator;
        private const double SidePanelGap = 16;

        private void GuideTab_Click(object sender, RoutedEventArgs e)
        {
            if (_guide != null) { _guide.Close(); return; }
            _guide = new GuideWindow { Owner = this };
            _guide.Closed += (_, __) => _guide = null;
            DockSidePanels(this, EventArgs.Empty);
            _guide.Show();
        }

        private void DockSidePanels(object? sender, EventArgs e)
        {
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double w = ActualWidth > 0 ? ActualWidth : Width;
            if (_guide != null)
            {
                _guide.Height = h;
                _guide.Top = Top;
                _guide.Left = Math.Max(SystemParameters.VirtualScreenLeft, Left - _guide.Width - SidePanelGap);
            }
            if (_creator != null)
            {
                _creator.Height = h;
                _creator.Top = Top;
                _creator.Left = Math.Min(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - _creator.Width,
                                         Left + w + SidePanelGap);
            }
        }

        private void Creator_Click(object sender, RoutedEventArgs e)
        {
            if (_creator != null) { _creator.Close(); return; }
            _creator = new PublishWindow { Owner = this };
            _creator.Closed += (_, __) => _creator = null;
            DockSidePanels(this, EventArgs.Empty);
            _creator.Show();
        }

        // ================================================================== key box

        private async void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            string key = ExtrasKeyBox.Password.Trim();
            if (key.Length == 0) return;
            if (!KeyVault.LooksLikeKey(key))
            {
                SaveKeyButton.IsEnabled = false;
                (string? unlocked, string? error) = await KeyVault.UnlockFromRepoAsync(KeyVault.ExtrasPath, key);
                SaveKeyButton.IsEnabled = true;
                if (unlocked == null)
                {
                    MessageBox.Show(this, error, "Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                key = unlocked;
            }
            try
            {
                Directory.CreateDirectory(ExtrasDir);
                File.WriteAllText(ExtrasKeyFile, key);
                Publisher.SaveKey(key);
                if (File.Exists(ExtrasSkipFile)) File.Delete(ExtrasSkipFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Fail("Couldn't save the key: " + ex.Message, ex);
                return;
            }
            ExtrasKeyBox.Clear();
            ShowExtrasState(true);
            await RunStagingAsync();
            await CheckAndUpdateAsync(forceInstall: true);
        }

        private void ShowExtrasState(bool keySaved)
        {
            ExtrasPanel.Visibility = keySaved ? Visibility.Collapsed : Visibility.Visible;
            ExtrasActivePanel.Visibility = keySaved ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================================================================== roster, factions & modes

        private void LoadBuiltInRoster()
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri("pack://application:,,,/roster.json"));
                if (resource == null) return;
                using var reader = new StreamReader(resource.Stream);
                ApplyRoster(RosterData.Parse(reader.ReadToEnd(), path => new Uri("pack://application:,,,/" + path.TrimStart('/'))));
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                Debug.WriteLine("Built-in roster failed: " + ex);
            }
        }

        private async Task RefreshRosterFromGitHubAsync()
        {
            const string rawBase = "https://raw.githubusercontent.com/" + Repo + "/main/launcher/";
            const string mergedRoster = "https://github.com/" + Repo + "/releases/latest/download/roster.json";
            try
            {
                string? json = null;
                try
                {
                    using var first = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    json = await Http.GetStringAsync(mergedRoster, first.Token);
                }
                catch (Exception ex) when (IsNetworkError(ex)) { Debug.WriteLine("Merged roster unavailable: " + ex.Message); }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                json ??= await Http.GetStringAsync(rawBase + "roster.json", cts.Token);
                RosterData remote = RosterData.Parse(json, path => new Uri(rawBase + path.TrimStart('/')));
                if (remote.Characters.Count > 0) ApplyRoster(remote);
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is JsonException)
            {
                Debug.WriteLine("Online roster unavailable, keeping the built-in one: " + ex.Message);
            }
        }

        private void ApplyRoster(RosterData data)
        {
            RosterList.ItemsSource = data.Characters;
            MapsList.ItemsSource = data.Maps;
            ModesList.ItemsSource = data.Modes;
            RosterCountText.Text = data.Characters.Count + " units in pack";
            RosterScroll.ScrollToHorizontalOffset(0);
            UpdateRosterArrows();
        }

        private const double CardStep = 168;
        private double _scrollFrom, _scrollTo;
        private DateTime _scrollStart;
        private bool _scrollAnimating;

        private void SmoothScrollTo(double target)
        {
            _scrollFrom = RosterScroll.HorizontalOffset;
            _scrollTo = Math.Max(0, Math.Min(target, RosterScroll.ScrollableWidth));
            _scrollStart = DateTime.UtcNow;
            if (_scrollAnimating) return;
            _scrollAnimating = true;
            CompositionTarget.Rendering += OnScrollFrame;
        }

        private void OnScrollFrame(object? sender, EventArgs e)
        {
            double t = Math.Min(1.0, (DateTime.UtcNow - _scrollStart).TotalSeconds / 0.35);
            double eased = 1 - Math.Pow(1 - t, 3);
            RosterScroll.ScrollToHorizontalOffset(_scrollFrom + (_scrollTo - _scrollFrom) * eased);
            if (t < 1.0) return;
            CompositionTarget.Rendering -= OnScrollFrame;
            _scrollAnimating = false;
        }

        private double RosterTarget => _scrollAnimating ? _scrollTo : RosterScroll.HorizontalOffset;
        private double RosterPage => Math.Max(1, Math.Floor((RosterScroll.ViewportWidth - 80) / CardStep)) * CardStep;

        private void RosterLeft_Click(object sender, RoutedEventArgs e) => SmoothScrollTo(RosterTarget - RosterPage);
        private void RosterRight_Click(object sender, RoutedEventArgs e) => SmoothScrollTo(RosterTarget + RosterPage);

        private void Roster_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            SmoothScrollTo(RosterTarget - e.Delta / 120.0 * CardStep);
            e.Handled = true;
        }

        private void Roster_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateRosterArrows();

        private void UpdateRosterArrows()
        {
            RosterLeft.IsEnabled = RosterScroll.HorizontalOffset > 1;
            RosterRight.IsEnabled = RosterScroll.HorizontalOffset < RosterScroll.ScrollableWidth - 1;
        }

        private Point _dragStart;
        private double _dragOffset;
        private bool _dragPending, _dragging;

        private void Roster_DragStart(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(RosterScroll);
            _dragOffset = RosterScroll.HorizontalOffset;
            _dragPending = true;
        }

        private void Roster_DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragPending || e.LeftButton != MouseButtonState.Pressed) { _dragPending = false; return; }
            double dx = e.GetPosition(RosterScroll).X - _dragStart.X;
            if (!_dragging && Math.Abs(dx) > 6)
            {
                _dragging = true;
                RosterScroll.CaptureMouse();
                RosterScroll.Cursor = Cursors.SizeWE;
            }
            if (_dragging) RosterScroll.ScrollToHorizontalOffset(_dragOffset - dx);
        }

        private void Roster_DragEnd(object sender, MouseButtonEventArgs e)
        {
            _dragPending = false;
            if (!_dragging) return;
            _dragging = false;
            RosterScroll.ReleaseMouseCapture();
            RosterScroll.Cursor = null;
            e.Handled = true;
        }

        private void TileRow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        // ================================================================== preview / screenshot

        private static readonly string[] Args = Environment.GetCommandLineArgs();
        private static bool PreviewMode => Array.Exists(Args, a => a == "--preview" || a == "--screenshot");

        private static string? ScreenshotPath
        {
            get
            {
                int i = Array.IndexOf(Args, "--screenshot");
                return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null;
            }
        }

        private void SaveScreenshot(string path)
        {
            if (Content is not FrameworkElement root) return;
            root.UpdateLayout();
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using FileStream fs = File.Create(path);
            png.Save(fs);
        }

        // ================================================================== UI helpers

        private void Ready(string message)
        {
            SetStatus(message);
            SetProgress(100, indeterminate: false);
            ProgressText.Text = "";
            PlayButton.IsEnabled = true;
            PlayButton.Focus();
        }

        private void Fail(string message, Exception ex)
        {
            Debug.WriteLine(ex);
            SetStatus(message);
            SetProgress(0, indeterminate: false);
            ProgressText.Text = "";
            RetryButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = ReadLocalVersion() != null;
        }

        private void SetStatus(string text) => StatusText.Text = text;

        private void SetProgress(double percent, bool indeterminate)
        {
            DownloadProgress.IsIndeterminate = indeterminate;
            DownloadProgress.Value = percent;
        }

        private void ShowInstalledVersion(string? version)
        {
            VersionText.Text = version == null
                ? "Installed: nothing yet"
                : "Installed build: " + (version.Length > 7 ? version.Substring(0, 7) : version);
        }

        private static bool IsNetworkError(Exception ex) =>
            ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException || ex is IOException;

        private async void Retry_Click(object sender, RoutedEventArgs e) => await CheckAndUpdateAsync();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_busy && MessageBox.Show(this, "An update is running. Close anyway?", "Zoid Gundam Mod",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Application.Current.Shutdown();
        }
    }
}
