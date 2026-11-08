using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ZoidGundamLauncher
{
    public partial class PublishWindow
    {
        private List<string> _quickPicked = new List<string>();
        private List<string> _quickKnown = new List<string>();

        private async void PickFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null || _busy) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick unit files or .zip (.zip, .ini, .png, .gif, .ogg, .dll)",
                Multiselect = true,
                Filter = "Rusted Warfare Mod files (*.zip;*.ini;*.png;*.gif;*.ogg;*.dll)|*.zip;*.ini;*.png;*.gif;*.ogg;*.dll|All files (*.*)|*.*"
            };
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads)) dialog.InitialDirectory = downloads;
            if (dialog.ShowDialog(this) != true) return;
            await SetQuickPicksAsync(dialog.FileNames);
        }

        private void Quick_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = _pub != null && !_busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Quick_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await SetQuickPicksAsync(paths);
        }

        private async Task SetQuickPicksAsync(string[] paths)
        {
            if (_pub == null || _busy) return;
            _quickPicked = paths.ToList();
            QuickText.Text = "Picked: " + string.Join(", ", paths.Select(p => Path.GetFileName(p.TrimEnd('\\', '/'))));
            QuickNameRow.Visibility = Visibility.Visible;

            string guess = QuickUpload.GuessName(_quickPicked);
            try
            {
                List<string> mods = await _pub.PublishableModsAsync(_cts.Token);
                _quickKnown = mods;
                QuickName.ItemsSource = mods;
                guess = mods.FirstOrDefault(m => m.Equals(guess, StringComparison.OrdinalIgnoreCase)) ?? guess;
            }
            catch (Exception ex) when (ex is GitHubException || IsNetworkError(ex)) { }
            QuickName.Text = guess;
            StatusLine.Text = "Check the mod name (defaults to Zoid Gundam), then press \"Check files\".";
        }

        private async void QuickCheck_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null || _busy || _quickPicked.Count == 0) return;
            string name = (QuickName.Text ?? "").Trim();
            if (_quickKnown.Count == 0)
            {
                try { _quickKnown = await _pub.PublishableModsAsync(_cts.Token); }
                catch (Exception ex) when (ex is GitHubException || IsNetworkError(ex)) { }
            }

            QuickUpload.Result staged;
            try { staged = await Task.Run(() => QuickUpload.Stage(_quickPicked, name, _quickKnown)); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                StatusLine.Text = ex is InvalidDataException ? "That zip looks broken. Download it again." : ex.Message;
                return;
            }
            _folder = staged.Folder;
            FolderText.Text = "Quick upload: " + (staged.Multi ? staged.Included.Count + " mods" : name);
            await RefreshAsync();
        }
    }
}
