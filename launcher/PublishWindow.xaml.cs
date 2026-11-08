using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ZoidGundamLauncher
{
    public partial class PublishWindow : Window
    {
        private readonly string? _screenshotPath;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Publisher? _pub;
        private List<ModPlan> _plans = new List<ModPlan>();
        private string _folder = "";
        private bool _busy;

        public PublishWindow(string? screenshotPath = null)
        {
            InitializeComponent();
            _screenshotPath = screenshotPath;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_screenshotPath != null) { await ShowDemoAsync(); return; }

            string? key = Publisher.ReadSavedKey();
            if (string.IsNullOrEmpty(key)) ShowKeyPanel(null);
            else await SignInAsync(key);
        }

        private void ShowKeyPanel(string? error)
        {
            MainPanel.Visibility = Visibility.Visible;
            MainPanel.IsEnabled = false;
            MainPanel.Opacity = 0.45;
            HelloText.Text = "Not signed in yet: sign in above to use Pick files and Publish.";
            KeyPanel.Visibility = Visibility.Visible;
            KeyError.Text = error ?? "";
            KeyError.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
            KeyBox.Focus();
        }

        private async void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyBox.Password.Trim();
            if (key.Length == 0) return;
            SaveKeyButton.IsEnabled = false;
            try
            {
                if (!KeyVault.LooksLikeKey(key))
                {
                    ShowKeyPanel("That doesn't look like a creator key (they start with ghp_ or github_pat_).");
                    return;
                }
                Publisher.SaveKey(key);
                KeyBox.Clear();
                await SignInAsync(key);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowKeyPanel("Couldn't save the key: " + ex.Message);
            }
            finally { SaveKeyButton.IsEnabled = true; }
        }

        private async Task SignInAsync(string key)
        {
            try
            {
                var pub = new Publisher(key);
                await pub.SignInAsync(_cts.Token);
                _pub = pub;
            }
            catch (GitHubException ex)
            {
                if (ex.Status == 401 || ex.Status == 403 || ex.Status == 404) Publisher.ForgetKey();
                ShowKeyPanel(ex.Message);
                return;
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                ShowKeyPanel("Couldn't reach GitHub. Check your internet and open this window again.");
                return;
            }

            KeyPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            MainPanel.IsEnabled = true;
            MainPanel.Opacity = 1;
            HelloText.Text = "Signed in as " + _pub.Login;

            string? saved = Publisher.ReadSavedFolder();
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            {
                _folder = saved;
                FolderText.Text = saved;
                await RefreshAsync();
            }
            else StatusLine.Text = "Press \"Choose folder...\" and pick your Zoid Gundam folder.";
            UpdatePublishButton();
        }

        private void CreateToken_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { }
            e.Handled = true;
        }

        private void ChangeKey_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            Publisher.ForgetKey();
            _pub = null;
            ShowKeyPanel(null);
        }

        private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pick your Zoid Gundam mod folder (Desktop\\Zoid Gundam or Rusted Warfare\\mods\\units\\Zoid Gundam)"
            };
            if (_folder.Length > 0 && Directory.Exists(_folder)) dialog.InitialDirectory = _folder;
            if (dialog.ShowDialog(this) != true) return;
            _folder = dialog.FolderName;
            FolderText.Text = _folder;
            Publisher.SaveFolder(_folder);
            await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async Task RefreshAsync()
        {
            if (_pub == null || _folder.Length == 0 || _busy) return;
            SetBusy(true);
            Bar.IsIndeterminate = true;
            ResultText.Text = "";
            CopyErrorsButton.Visibility = Visibility.Collapsed;
            StatusLine.Text = "Checking your unit & building files against GitHub...";
            try
            {
                _plans = await _pub.ScanAsync(_folder, _cts.Token);
                foreach (ModPlan plan in _plans) plan.PropertyChanged += (_, __) => UpdatePublishButton();
                ModList.ItemsSource = _plans;
                if (_plans.Count == 0)
                {
                    EmptyText.Text = "Everything in this folder is already up to date on GitHub.";
                    EmptyText.Visibility = Visibility.Visible;
                }
                else EmptyText.Visibility = Visibility.Collapsed;
                StatusLine.Text = _plans.Any(p => p.ChangedCount > 0) ? "Ready. Tick your mod and press the gold button." : "Everything is up to date on GitHub. Nothing to publish.";
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch (Exception ex) when (ex is GitHubException || ex is InvalidOperationException || ex is IOException || IsNetworkError(ex))
            {
                StatusLine.Text = ex is GitHubException || ex is InvalidOperationException ? ex.Message : "Couldn't check your files: " + ex.Message;
            }
            finally
            {
                Bar.IsIndeterminate = false;
                Bar.Value = 0;
                SetBusy(false);
            }
        }

        private void UpdatePublishButton()
        {
            int files = _plans.Where(p => p.WillPublish).Sum(p => p.ChangedCount);
            PublishButton.Content = files == 0 ? "NOTHING TO PUBLISH" : "PUBLISH " + files + (files == 1 ? " FILE" : " FILES");
            PublishButton.IsEnabled = !_busy && files > 0;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            ChooseFolderButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            ChangeKeyButton.IsEnabled = !busy;
            PickFilesButton.IsEnabled = !busy;
            QuickCheckButton.IsEnabled = !busy;
            ModList.IsEnabled = !busy;
            NoteBox.IsEnabled = !busy;
            UpdatePublishButton();
        }

        private async void Publish_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null) return;
            List<ModPlan> chosen = _plans.Where(p => p.WillPublish).ToList();
            if (chosen.Count == 0) return;
            int files = chosen.Sum(p => p.ChangedCount);
            string names = string.Join(", ", chosen.Select(p => p.Name));

            if (MessageBox.Show(this,
                    "Publish " + files + (files == 1 ? " file" : " files") + " for " + names + "?\n\nEveryone gets the new version immediately when they press PLAY or Check Updates.",
                    "Publish", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            SetBusy(true);
            ResultText.Text = "";
            CopyErrorsButton.Visibility = Visibility.Collapsed;
            string note = NoteBox.Text.Trim();
            string message = names + ": " + (note.Length > 0 ? note : "update") +
                             "\n\nPublished from the Zoid Gundam launcher by " + _pub.Login + ".";
            var uploadProgress = new Progress<(int Percent, string Text)>(p => { Bar.IsIndeterminate = false; Bar.Value = p.Percent; StatusLine.Text = p.Text; });

            try
            {
                string sha = await _pub.PublishAsync(chosen, message, uploadProgress, _cts.Token);
                NoteBox.Clear();
                Bar.IsIndeterminate = false;
                Bar.Value = 100;
                StatusLine.Text = "All done! Commit " + (sha.Length >= 7 ? sha.Substring(0, 7) : sha) + " is live.";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xE3, 0x9A));
                ResultText.Text = "✔ Published to GitHub main and updated the live release zip! Friends get it right now.";
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is GitHubException || ex is IOException || ex is InvalidOperationException || IsNetworkError(ex))
            {
                Bar.IsIndeterminate = false;
                Bar.Value = 0;
                StatusLine.Text = "";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x8C));
                string why = ex.InnerException != null ? ex.Message + " (" + ex.InnerException.Message + ")" : ex.Message;
                ResultText.Text = "Couldn't publish: " + why;
            }
            finally { SetBusy(false); }

            await RefreshAsync();
        }

        private void CopyErrors_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(ResultText.Text); CopyErrorsButton.Content = "Copied!"; }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }

        private async Task ShowDemoAsync()
        {
            KeyPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            HelloText.Text = "Signed in as a6407805-alt";
            FolderText.Text = @"C:\Users\ahren\Desktop\Zoid Gundam";
            _plans = new List<ModPlan>
            {
                new ModPlan { Name = "Zoid Gundam", Files = DemoFiles(5), IsSelected = true }
            };
            ModList.ItemsSource = _plans;
            NoteBox.Text = "Added Shield Liger & updated Gundam 4 beam sabers";
            StatusLine.Text = "Ready. Tick your mod and press the gold button.";
            UpdatePublishButton();
            await Task.Delay(1200);
            SaveScreenshot(_screenshotPath!);
            Application.Current.Shutdown();
        }

        private static List<PublishFile> DemoFiles(int count) =>
            Enumerable.Range(0, count).Select(i => new PublishFile { Changed = true, RemotePath = "mods/Zoid Gundam/f" + i, LocalPath = "f" + i }).ToList();

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

        private static bool IsNetworkError(Exception ex) =>
            ex is HttpRequestException || ex is TaskCanceledException || ex is IOException;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
        }
    }
}
