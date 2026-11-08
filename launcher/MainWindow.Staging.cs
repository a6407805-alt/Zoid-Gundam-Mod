// ============================================================================================================
//  Main window: Auto-Sync (both Owner & Creators) and the Review Bell.
// ============================================================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ZoidGundamLauncher
{
    public partial class MainWindow
    {
        private string? _stagingKey;
        private int _modsNotSent;
        private bool _isOwner;
        private System.Windows.Threading.DispatcherTimer? _bellTimer;
        private System.Windows.Threading.DispatcherTimer? _everyoneBellTimer;

        /// <summary>
        /// Auto-syncs local edits from Desktop\Zoid Gundam and Rusted Warfare\mods\units\Zoid Gundam straight to GitHub
        /// (for BOTH the owner and creators!), updates the live GitHub Release zip immediately, and shows the review bell.
        /// </summary>
        private async Task RunStagingAsync()
        {
            string? key = StagingSync.ReadCreatorKey();
            if (string.IsNullOrEmpty(key)) { await StageWithSharedKeyAsync(); ShowEveryoneBell(); return; }
            _stagingKey = key;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                (string login, bool owner) = await StagingSync.WhoAmIAsync(key, cts.Token);
                if (login.Length == 0) return;

                DateTime installed = File.Exists(VersionFile) ? File.GetLastWriteTimeUtc(VersionFile) : DateTime.MinValue;
                var progress = new Progress<string>(SetStatus);
                int staged = await StagingSync.SilentStageLocalModAsync(key, login, ReadLocalVersion(), installed, null, cts.Token, autoPublish: true, progress: progress);
                if (staged > 0) SetStatus("Auto-synced & published " + staged + " changed file(s) to everyone!");
                if (staged < 0) _modsNotSent = -staged;

                if (owner) _ = ShowOwnerReviewAsync();
                else ShowEveryoneBell();
            }
            catch (Exception ex) { StagingSync.Log("Skipped: " + ex.Message); }
        }

        private async Task StageWithSharedKeyAsync()
        {
            try
            {
                string? extras = KeyVault.ReadExtrasKey();
                if (string.IsNullOrEmpty(extras)) return;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                (string? shared, _) = await KeyVault.UnlockFromRepoAsync(KeyVault.StagingPath, extras, cts.Token);
                if (string.IsNullOrEmpty(shared)) return;
                string name = System.Text.RegularExpressions.Regex.Replace(Environment.UserName.ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-');
                if (name.Length == 0) name = Environment.MachineName.ToLowerInvariant();
                DateTime installed = File.Exists(VersionFile) ? File.GetLastWriteTimeUtc(VersionFile) : DateTime.MinValue;
                int staged = await StagingSync.SilentStageLocalModAsync(shared, name, ReadLocalVersion(), installed, null, cts.Token, autoPublish: false);
                if (staged > 0) SetStatus("Sent " + staged + " changed file(s) to the pack owner for review.");
            }
            catch (Exception ex) { StagingSync.Log("Shared key skipped: " + ex.Message); }
        }

        private async Task ShowOwnerReviewAsync()
        {
            if (_stagingKey == null) return;
            _isOwner = true;
            ReviewBellButton.Visibility = Visibility.Visible;
            if (_bellTimer == null)
            {
                _bellTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
                _bellTimer.Tick += async (_, __) => await RefreshBellAsync();
                _bellTimer.Start();
            }
            await RefreshBellAsync();
        }

        private async Task RefreshBellAsync()
        {
            if (_stagingKey == null) return;
            try
            {
                var waiting = await StagingSync.CheckAllStagingAsync(_stagingKey);
                DateTimeOffset seen = StagingSync.BellSeen();
                var uploads = await StagingSync.RecentUploadsAsync(_stagingKey, 30);
                int fresh = uploads.Count(u => u.FromCreator && u.Date > seen && u.Build != "part");
                SetBell(waiting.Count, fresh, false);
            }
            catch (Exception ex) { Debug.WriteLine("Bell check skipped: " + ex.Message); }
        }

        private void SetBell(int waiting, int fresh = 0, bool building = false)
        {
            int count = waiting + fresh;
            bool lit = count > 0 || building;
            ReviewBellButton.Tag = count > 0 ? count.ToString() : null;
            ReviewBellButton.Background = new SolidColorBrush(lit ? Color.FromRgb(0xFF, 0xC9, 0x28) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            ReviewBellButton.Foreground = new SolidColorBrush(lit ? Color.FromRgb(0x10, 0x14, 0x20) : Colors.White);
            ReviewBellButton.ToolTip = waiting > 0 ? waiting + " update(s) waiting for your approval"
                                     : fresh > 0 ? fresh + " new upload(s) from creators"
                                     : "Mod uploads and updates";
        }

        private void ShowEveryoneBell()
        {
            ReviewBellButton.Visibility = Visibility.Visible;
            if (_everyoneBellTimer != null) return;
            _everyoneBellTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
            _everyoneBellTimer.Tick += async (_, __) => await RefreshEveryoneBellAsync();
            _everyoneBellTimer.Start();
            _ = RefreshEveryoneBellAsync();
        }

        private async Task RefreshEveryoneBellAsync()
        {
            try
            {
                ReleaseInfo? r = await GetLatestReleaseFromInfoFileAsync();
                if (r == null) return;
                bool newSinceSeen = r.Published > StagingSync.BellSeen();
                bool notInstalled = !string.Equals(r.Version, ReadLocalVersion(), StringComparison.OrdinalIgnoreCase);
                bool lit = newSinceSeen || notInstalled;
                ReviewBellButton.Tag = lit ? "!" : null;
                ReviewBellButton.Background = new SolidColorBrush(lit ? Color.FromRgb(0xFF, 0xC9, 0x28) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                ReviewBellButton.Foreground = new SolidColorBrush(lit ? Color.FromRgb(0x10, 0x14, 0x20) : Colors.White);
                ReviewBellButton.ToolTip = notInstalled ? "A new update is ready (" + r.Published.LocalDateTime.ToString("MMM d, h:mm tt") + "): press PLAY or SYNC & CHECK UPDATES"
                                         : newSinceSeen ? "Update finished (" + r.Published.LocalDateTime.ToString("MMM d, h:mm tt") + ")"
                                         : "Update status";
            }
            catch (Exception ex) { Debug.WriteLine("Bell check skipped: " + ex.Message); }
        }

        private void ReviewBell_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOwner)
            {
                var status = new ReviewWindow(_stagingKey, owner: false) { Owner = this };
                StagingSync.MarkBellSeen();
                status.ShowDialog();
                _ = RefreshEveryoneBellAsync();
                return;
            }
            if (_stagingKey == null) return;
            var w = new ReviewWindow(_stagingKey) { Owner = this };
            StagingSync.MarkBellSeen();
            w.ShowDialog();
            SetBell(w.Remaining);
            _ = RefreshBellAsync();
            if (w.Approved) SetStatus("Update approved and published live!");
        }
    }
}
