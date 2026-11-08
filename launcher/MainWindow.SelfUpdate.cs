// ============================================================================================================
//  Zoid Gundam Launcher - Self-Update (MainWindow.SelfUpdate.cs)
// ============================================================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ZoidGundamLauncher
{
    public partial class MainWindow
    {
        private const string LauncherDownloadUrl = "https://github.com/" + Repo + "/releases/latest/download/" + LauncherAssetName;

        private static bool PlayAfterUpdate => Array.Exists(Args, a => a == "--play");
        private static bool JustUpdated => Array.Exists(Args, a => a == "--updated");

        private async Task<bool> TrySelfUpdateAsync(bool fromRefresh, bool thenPlay = false)
        {
#if DEBUG
            await Task.CompletedTask;
            return false;
#else
            if (PreviewMode || (JustUpdated && !fromRefresh)) return false;

            string? me = Environment.ProcessPath;
            string myName = me == null ? "" : Path.GetFileName(me);
            if (me == null || !myName.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)
                || !myName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (myName.IndexOf("-dev", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            string newPath = me + ".new";
            string oldPath = me + ".old";
            TryDelete(oldPath);
            TryDelete(newPath);

            ReleaseInfo release;
            try
            {
                release = await GetLatestReleaseAsync();
                _remote = release;
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is InvalidOperationException || ex is System.Text.Json.JsonException
                                       || ex is System.Collections.Generic.KeyNotFoundException)
            {
                Debug.WriteLine("Launcher update check skipped: " + ex.Message);
                return false;
            }

            long mine;
            try { mine = new FileInfo(me).Length; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }

            if (release.LauncherSize <= 0 || release.LauncherSize == mine) return false;

            _busy = true;
            PlayButton.IsEnabled = false;
            SetStatus("Updating the launcher...");
            SetProgress(0, indeterminate: false);
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
                using (HttpResponseMessage response = await Http.GetAsync(LauncherDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                {
                    response.EnsureSuccessStatusCode();
                    await using Stream source = await response.Content.ReadAsStreamAsync(cts.Token);
                    await using var target = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int lastPercent = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                        total += read;
                        int percent = (int)Math.Min(100, total * 100 / release.LauncherSize);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            SetProgress(percent, indeterminate: false);
                        }
                    }
                }

                if (new FileInfo(newPath).Length != release.LauncherSize)
                {
                    TryDelete(newPath);
                    return Skip("The launcher download didn't finish. It will try again next time.");
                }

                File.Move(me, oldPath);
                try
                {
                    File.Move(newPath, me);
                }
                catch (Exception) when (TryRestore(oldPath, me))
                {
                    throw;
                }
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is UnauthorizedAccessException)
            {
                Debug.WriteLine("Launcher update skipped: " + ex.Message);
                TryDelete(newPath);
                return Skip("Couldn't update the launcher right now. It will try again next time.");
            }

            try
            {
                var psi = new ProcessStartInfo(me) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(me) ?? "" };
                psi.ArgumentList.Add("--updated");
                if (thenPlay) psi.ArgumentList.Add("--play");
                for (int i = 1; i < Args.Length; i++)
                {
                    if (Args[i] != "--updated" && Args[i] != "--play") psi.ArgumentList.Add(Args[i]);
                }
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine("Restart after launcher update failed: " + ex.Message);
                return Skip("Launcher updated. Close and reopen it to use the new version.");
            }

            Application.Current.Shutdown();
            return true;
#endif
        }

        private bool Skip(string message)
        {
            _busy = false;
            SetStatus(message);
            SetProgress(0, indeterminate: false);
            return false;
        }

        private static bool TryRestore(string oldPath, string me)
        {
            try { if (!File.Exists(me) && File.Exists(oldPath)) File.Move(oldPath, me); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Debug.WriteLine("Restore failed: " + ex.Message); }
            return true;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Debug.WriteLine("Couldn't delete " + path + ": " + ex.Message); }
        }
    }
}
