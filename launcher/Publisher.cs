// ============================================================================================================
//  Publisher - the engine behind the Zoid Gundam Launcher's "Creator" window and direct release packaging.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ZoidGundamLauncher
{
    public sealed class GitHubException : Exception
    {
        public int Status { get; }
        public GitHubException(int status, string message) : base(message) { Status = status; }
    }

    public sealed class PublishFile
    {
        public string RemotePath { get; init; } = "";
        public string LocalPath { get; init; } = "";
        public long Size { get; init; }
        public bool Changed { get; init; }
        public bool ExistsOnline { get; init; }
        public string? BlobSha { get; set; }
    }

    public sealed class ModPlan : INotifyPropertyChanged
    {
        public string Name { get; init; } = "";
        public List<PublishFile> Files { get; init; } = new List<PublishFile>();
        public bool IsNewMod { get; init; }
        public string Conflict { get; init; } = "";

        public int ChangedCount => Files.Count(f => f.Changed);
        public bool HasConflict => Conflict.Length > 0;
        public System.Windows.Visibility ConflictVisibility => HasConflict ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _selected;
        public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }

        private bool _overwrite;
        public bool OverwriteOk { get => _overwrite; set { _overwrite = value; Raise(nameof(OverwriteOk)); } }

        public bool WillPublish => IsSelected && ChangedCount > 0 && (!HasConflict || OverwriteOk);

        public string StatusText =>
            ChangedCount == 0 ? "Up to date" :
            (IsNewMod ? "New mod: " : "") + ChangedCount + (ChangedCount == 1 ? " file changed" : " files changed");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class BuildReport
    {
        public bool Finished { get; init; }
        public bool Success { get; set; }
        public List<string> Lines { get; init; } = new List<string>();
        public List<string> Errors { get; init; } = new List<string>();
    }

    public sealed class Publisher
    {
        public const string Repo = "a6407805-alt/Zoid-Gundam-Mod";
        private const string Branch = "main";
        private const long MaxFileBytes = 90L * 1024 * 1024;

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoidGundam");
        private static readonly string FallbackConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters");
        public static readonly string KeyFile = Path.Combine(ConfigDir, "creator-key.txt");
        public static readonly string FolderFile = Path.Combine(ConfigDir, "creator-folder.txt");

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoidGundamLauncher", "1.0"));
            return client;
        }

        private readonly string _token;
        public string Login { get; private set; } = "";
        public bool IsAdmin => Login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase);
        public bool CanEditAll => true;

        public Publisher(string token) { _token = token.Trim(); }

        public static string? ReadSavedKey()
        {
            try
            {
                if (File.Exists(KeyFile))
                {
                    string k = File.ReadAllText(KeyFile).Trim();
                    if (k.Length > 0) return k;
                }
                string fallback = Path.Combine(FallbackConfigDir, "creator-key.txt");
                if (File.Exists(fallback))
                {
                    string k = File.ReadAllText(fallback).Trim();
                    if (k.Length > 0)
                    {
                        try { Directory.CreateDirectory(ConfigDir); File.WriteAllText(KeyFile, k); } catch { }
                        return k;
                    }
                }
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        public static void SaveKey(string key)
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(KeyFile, key.Trim());
        }

        public static void ForgetKey()
        {
            try { if (File.Exists(KeyFile)) File.Delete(KeyFile); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        public static string? ReadSavedFolder()
        {
            try
            {
                if (File.Exists(FolderFile))
                {
                    string f = File.ReadAllText(FolderFile).Trim();
                    if (f.Length > 0 && Directory.Exists(f)) return f;
                }
                string desktopMod = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Zoid Gundam");
                if (Directory.Exists(desktopMod)) return desktopMod;
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        public static void SaveFolder(string folder)
        {
            try { Directory.CreateDirectory(ConfigDir); File.WriteAllText(FolderFile, folder); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        // ------------------------------------------------------------------ GitHub plumbing

        private async Task<JsonNode?> CallAsync(HttpMethod method, string path, JsonNode? body = null, CancellationToken ct = default)
        {
            string url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : "https://api.github.com/repos/" + Repo + path;
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            string text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
                throw new GitHubException((int)response.StatusCode, Friendly((int)response.StatusCode, text));
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        public async Task<string> UploadReleaseFileAsync(string tag, string assetName, string localPath, string contentType, CancellationToken ct = default)
        {
            JsonNode? release;
            try { release = await CallAsync(HttpMethod.Get, "/releases/tags/" + tag, null, ct); }
            catch (GitHubException ex) when (ex.Status == 404)
            {
                release = await CallAsync(HttpMethod.Post, "/releases", new JsonObject
                {
                    ["tag_name"] = tag,
                    ["target_commitish"] = Branch,
                    ["name"] = tag == "latest" ? "Zoid Gundam Mod (latest build)" : tag,
                    ["body"] = "Built automatically by the Zoid Gundam Launcher.",
                    ["prerelease"] = tag != "latest",
                    ["make_latest"] = tag == "latest" ? "true" : "false"
                }, ct);
            }
            long id = release!["id"]!.GetValue<long>();
            foreach (JsonNode? a in release["assets"]?.AsArray() ?? new JsonArray())
                if (a?["name"]?.GetValue<string>() == assetName)
                    await CallAsync(HttpMethod.Delete, "/releases/assets/" + a["id"]!.GetValue<long>(), null, ct);

            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://uploads.github.com/repos/" + Repo + "/releases/" + id + "/assets?name=" + Uri.EscapeDataString(assetName));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            await using FileStream fs = File.OpenRead(localPath);
            request.Content = new StreamContent(fs);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(10));
            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            string text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode) throw new GitHubException((int)response.StatusCode, Friendly((int)response.StatusCode, text));
            return JsonNode.Parse(text)!["browser_download_url"]!.GetValue<string>();
        }

        /// <summary>
        /// Immediately packages Zoid-Gundam-Mod.zip and release-info.json from the local mod files and uploads them
        /// to the "latest" release on GitHub so players get updates in seconds without waiting for a build runner.
        /// </summary>
        public async Task RepackageAndUploadReleaseAsync(string commitSha, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            string tempStage = Path.Combine(Path.GetTempPath(), "ZoidGundam-ReleaseStage-" + Guid.NewGuid().ToString("N"));
            string tempZip = tempStage + ".zip";
            string tempInfo = tempStage + "-info.json";
            try
            {
                progress?.Report("Packaging Zoid-Gundam-Mod.zip for immediate release...");
                Directory.CreateDirectory(Path.Combine(tempStage, "Mods"));

                // 1. Copy existing pack scripts (install.ps1, etc.) from %LOCALAPPDATA%\ZoidGundam\Launcher\pack if present
                string launcherPackDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoidGundam", "Launcher", "pack");
                if (Directory.Exists(launcherPackDir))
                {
                    foreach (string file in Directory.GetFiles(launcherPackDir))
                        File.Copy(file, Path.Combine(tempStage, Path.GetFileName(file)), true);
                    string packMods = Path.Combine(launcherPackDir, "Mods");
                    if (Directory.Exists(packMods))
                        CopyDirectory(packMods, Path.Combine(tempStage, "Mods"));
                }

                // 2. Overlay latest local mods from Rusted Warfare\mods\units\Zoid Gundam and Desktop\Zoid Gundam
                string? game = StagingSync.FindGameFolders().FirstOrDefault();
                if (game != null)
                {
                    string gameZoid = Path.Combine(game, "mods", "units", "Zoid Gundam");
                    if (Directory.Exists(gameZoid))
                        CopyDirectory(gameZoid, Path.Combine(tempStage, "Mods", "Zoid Gundam"));
                }
                string desktopZoid = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Zoid Gundam");
                if (Directory.Exists(desktopZoid))
                {
                    foreach (string f in Directory.EnumerateFiles(desktopZoid, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetFileName(f).StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                        string rel = Path.GetRelativePath(desktopZoid, f);
                        string dst = Path.Combine(tempStage, "Mods", "Zoid Gundam", rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        if (!File.Exists(dst) || File.GetLastWriteTimeUtc(f) >= File.GetLastWriteTimeUtc(dst))
                            File.Copy(f, dst, true);
                    }
                }

                ZipFile.CreateFromDirectory(tempStage, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                long zipSize = new FileInfo(tempZip).Length;

                // Keep the existing launcher exe size from the current release
                long exeSize = 0;
                try
                {
                    JsonNode? rel = await CallAsync(HttpMethod.Get, "/releases/tags/latest", null, ct);
                    foreach (JsonNode? a in rel?["assets"]?.AsArray() ?? new JsonArray())
                        if (a?["name"]?.GetValue<string>() == "Zoid-Gundam-Mod.exe")
                            exeSize = a["size"]!.GetValue<long>();
                }
                catch { }
                if (exeSize == 0 && Environment.ProcessPath is string me && File.Exists(me) && Path.GetFileName(me).StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase))
                    exeSize = new FileInfo(me).Length;

                var infoJson = new JsonObject
                {
                    ["version"] = commitSha,
                    ["zipSize"] = zipSize,
                    ["exeSize"] = exeSize,
                    ["published"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                File.WriteAllText(tempInfo, infoJson.ToJsonString() + "\n");

                progress?.Report("Uploading Zoid-Gundam-Mod.zip to GitHub release...");
                await UploadReleaseFileAsync("latest", "Zoid-Gundam-Mod.zip", tempZip, "application/zip", ct);
                await UploadReleaseFileAsync("latest", "release-info.json", tempInfo, "application/json", ct);

                // Point the "latest" tag and release target_commitish at the new commit SHA
                try
                {
                    await CallAsync(new HttpMethod("PATCH"), "/git/refs/tags/latest", new JsonObject { ["sha"] = commitSha, ["force"] = true }, ct);
                }
                catch
                {
                    try { await CallAsync(HttpMethod.Post, "/git/refs", new JsonObject { ["ref"] = "refs/tags/latest", ["sha"] = commitSha }, ct); } catch { }
                }
                try
                {
                    JsonNode? rel = await CallAsync(HttpMethod.Get, "/releases/tags/latest", null, ct);
                    if (rel?["id"] != null)
                    {
                        await CallAsync(new HttpMethod("PATCH"), "/releases/" + rel["id"]!.GetValue<long>(), new JsonObject
                        {
                            ["target_commitish"] = commitSha,
                            ["body"] = $"Built automatically from commit {(commitSha.Length >= 7 ? commitSha.Substring(0, 7) : commitSha)}. Run Zoid-Gundam-Mod.exe (the launcher) to auto-sync and play!"
                        }, ct);
                    }
                }
                catch { }

                // Update local version.txt so this PC knows it already has this build
                try
                {
                    string versionFile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoidGundam", "Launcher", "version.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(versionFile)!);
                    File.WriteAllText(versionFile, commitSha);
                }
                catch { }
            }
            finally
            {
                try { if (Directory.Exists(tempStage)) Directory.Delete(tempStage, true); } catch { }
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (File.Exists(tempInfo)) File.Delete(tempInfo); } catch { }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(sourceDir, file);
                string target = Path.Combine(destinationDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }

        private static string Friendly(int status, string text)
        {
            if (status == 401) return "That creator key doesn't work (it may have expired). Ask for a new one.";
            if (status == 403 || status == 404) return "This key isn't allowed to publish to the pack. Ask for a new one.";
            string detail = text;
            try { detail = JsonNode.Parse(text)?["message"]?.GetValue<string>() ?? text; } catch (JsonException) { }
            return "GitHub said no (" + status + "): " + detail;
        }

        public async Task<string> SignInAsync(CancellationToken ct = default)
        {
            JsonNode? me = await CallAsync(HttpMethod.Get, "https://api.github.com/user", null, ct);
            Login = me?["login"]?.GetValue<string>() ?? "";
            if (Login.Length == 0) throw new GitHubException(401, Friendly(401, ""));
            JsonNode? repo = await CallAsync(HttpMethod.Get, "", null, ct);
            JsonNode? pushNode = repo?["permissions"]?["push"];
            bool canPush = pushNode == null || pushNode.GetValue<bool>();
            if (!canPush) throw new GitHubException(403, Friendly(403, ""));
            return Login;
        }

        public async Task UpdateTextFileAsync(string repoPath, Func<string?, string> change, string message, CancellationToken ct = default)
        {
            string? sha = null, current = null;
            try
            {
                JsonNode? node = await CallAsync(HttpMethod.Get, "/contents/" + repoPath + "?ref=" + Branch, null, ct);
                sha = node?["sha"]?.GetValue<string>();
                string? b64 = node?["content"]?.GetValue<string>();
                if (b64 != null) current = Encoding.UTF8.GetString(Convert.FromBase64String(b64.Replace("\n", "").Replace("\r", "")));
            }
            catch (GitHubException ex) when (ex.Status == 404) { }

            var body = new JsonObject
            {
                ["message"] = message + " [skip ci]",
                ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(change(current))),
                ["branch"] = Branch,
            };
            if (sha != null) body["sha"] = sha;
            await CallAsync(HttpMethod.Put, "/contents/" + repoPath, body, ct);
        }

        private async Task<(string HeadSha, string TreeSha)> GetHeadAsync(CancellationToken ct)
        {
            JsonNode? reference = await CallAsync(HttpMethod.Get, "/git/ref/heads/" + Branch, null, ct);
            string head = reference!["object"]!["sha"]!.GetValue<string>();
            JsonNode? commit = await CallAsync(HttpMethod.Get, "/git/commits/" + head, null, ct);
            return (head, commit!["tree"]!["sha"]!.GetValue<string>());
        }

        private async Task<JsonNode> ReadCreatorsAsync(string refName, CancellationToken ct)
        {
            try
            {
                JsonNode? file = await CallAsync(HttpMethod.Get, "/contents/launcher/creators.json?ref=" + refName, null, ct);
                string b64 = (file?["content"]?.GetValue<string>() ?? "").Replace("\n", "");
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                return JsonNode.Parse(json) ?? new JsonObject();
            }
            catch (GitHubException ex) when (ex.Status == 404) { return new JsonObject(); }
        }

        public async Task<List<string>> PublishableModsAsync(CancellationToken ct = default)
        {
            (_, string treeSha) = await GetHeadAsync(ct);
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "Zoid Gundam" };
            JsonNode? tree = await CallAsync(HttpMethod.Get, "/git/trees/" + treeSha + "?recursive=1", null, ct);
            foreach (JsonNode? item in tree?["tree"]?.AsArray() ?? new JsonArray())
            {
                string[] parts = (item?["path"]?.GetValue<string>() ?? "").Split('/');
                if (item?["type"]?.GetValue<string>() == "tree" && parts.Length == 2 && parts[0] == "mods") names.Add(parts[1]);
            }
            return names.ToList();
        }

        // ------------------------------------------------------------------ scanning

        private static readonly string[] SkipFolders = { "bin", "obj", ".vs", ".git", ".idea", "node_modules" };
        private static readonly string[] SkipExtensions = { ".user", ".suo", ".tmp", ".bak", ".old", ".new" };

        private static IEnumerable<(string Remote, string Local)> ModFiles(string folder, string mod, bool isDirectModFolder)
        {
            if (isDirectModFolder)
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                    string[] parts = rel.Split('/');
                    if (parts.Take(parts.Length - 1).Any(p => SkipFolders.Contains(p, StringComparer.OrdinalIgnoreCase))) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (SkipExtensions.Contains(ext)) continue;
                    yield return ("mods/" + mod + "/" + rel, file);
                }
                yield break;
            }

            foreach (string area in new[] { "src", "mods" })
            {
                string dir = Path.Combine(folder, area, mod);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    string[] parts = rel.Split('/');
                    if (parts.Take(parts.Length - 1).Any(p => SkipFolders.Contains(p, StringComparer.OrdinalIgnoreCase))) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (SkipExtensions.Contains(ext)) continue;
                    yield return (area + "/" + mod + "/" + rel, file);
                }
            }
        }

        internal static string GitBlobSha(string path)
        {
            using FileStream fs = File.OpenRead(path);
            using var sha = SHA1.Create();
            byte[] header = Encoding.ASCII.GetBytes("blob " + fs.Length + "\0");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            var buffer = new byte[81920];
            int n;
            while ((n = fs.Read(buffer, 0, buffer.Length)) > 0) sha.TransformBlock(buffer, 0, n, null, 0);
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        /// <summary>
        /// Supports BOTH a repo-shaped folder (containing "mods/<Mod>") AND a direct Rusted Warfare mod folder
        /// like "Desktop\Zoid Gundam" or "Rusted Warfare\mods\units\Zoid Gundam" (containing "units" or "BUILDINGS" or *.ini).
        /// </summary>
        public async Task<List<ModPlan>> ScanAsync(string folder, CancellationToken ct = default)
        {
            bool hasRepoLayout = Directory.Exists(Path.Combine(folder, "mods")) || Directory.Exists(Path.Combine(folder, "src"));
            bool isDirectModFolder = !hasRepoLayout &&
                (Directory.Exists(Path.Combine(folder, "units")) ||
                 Directory.Exists(Path.Combine(folder, "BUILDINGS")) ||
                 Directory.EnumerateFiles(folder, "*.ini", SearchOption.AllDirectories).Any());

            if (!hasRepoLayout && !isDirectModFolder)
                throw new InvalidOperationException("Pick your Zoid Gundam mod folder (with 'units' and 'BUILDINGS' inside) or the repo folder.");

            (string head, string treeSha) = await GetHeadAsync(ct);
            JsonNode? tree = await CallAsync(HttpMethod.Get, "/git/trees/" + treeSha + "?recursive=1", null, ct);
            var remote = new Dictionary<string, string>(StringComparer.Ordinal);
            var remoteMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonNode? item in tree!["tree"]!.AsArray())
            {
                if (item == null || item["type"]?.GetValue<string>() != "blob") continue;
                string path = item["path"]!.GetValue<string>();
                remote[path] = item["sha"]!.GetValue<string>();
                string[] parts = path.Split('/');
                if (parts.Length >= 3 && (parts[0] == "src" || parts[0] == "mods")) remoteMods.Add(parts[1]);
            }

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (isDirectModFolder)
            {
                string dirName = Path.GetFileName(folder.TrimEnd('\\', '/'));
                names.Add(remoteMods.FirstOrDefault(m => m.Equals(dirName, StringComparison.OrdinalIgnoreCase)) ?? "Zoid Gundam");
            }
            else
            {
                foreach (string area in new[] { "src", "mods" })
                {
                    string dir = Path.Combine(folder, area);
                    if (Directory.Exists(dir))
                        foreach (string sub in Directory.GetDirectories(dir)) names.Add(Path.GetFileName(sub));
                }
            }

            var plans = new List<ModPlan>();
            foreach (string mod in names)
            {
                bool isNew = !remoteMods.Contains(mod);
                var files = await Task.Run(() =>
                {
                    var list = new List<PublishFile>();
                    foreach ((string rp, string lp) in ModFiles(folder, mod, isDirectModFolder))
                    {
                        long size = new FileInfo(lp).Length;
                        if (size > MaxFileBytes) continue;
                        bool online = remote.TryGetValue(rp, out string? sha);
                        bool changed = !online || sha != GitBlobSha(lp);
                        list.Add(new PublishFile { RemotePath = rp, LocalPath = lp, Size = size, Changed = changed, ExistsOnline = online });
                    }
                    return list;
                }, ct);

                int changedCount = files.Count(f => f.Changed);
                if (changedCount == 0 && !isDirectModFolder) continue;

                string conflict = "";
                if (!isNew && changedCount > 0) conflict = await FindConflictAsync(files, ct);

                plans.Add(new ModPlan { Name = mod, Files = files, IsNewMod = isNew, Conflict = conflict, IsSelected = changedCount > 0 });
            }
            return plans;
        }

        private async Task<string> FindConflictAsync(List<PublishFile> files, CancellationToken ct)
        {
            try
            {
                IEnumerable<PublishFile> suspects = files.Where(f => f.Changed && f.ExistsOnline)
                    .OrderBy(f => f.RemotePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .Take(6);
                foreach (PublishFile file in suspects)
                {
                    JsonNode? list = await CallAsync(HttpMethod.Get,
                        "/commits?sha=" + Branch + "&per_page=1&path=" + Uri.EscapeDataString(file.RemotePath), null, ct);
                    if (list is not JsonArray arr || arr.Count == 0) continue;
                    JsonNode? c = arr[0];
                    string who = c?["author"]?["login"]?.GetValue<string>() ?? "";
                    if (who.Length == 0 || who.Equals(Login, StringComparison.OrdinalIgnoreCase)) continue;
                    string when = c?["commit"]?["author"]?["date"]?.GetValue<string>() ?? "";
                    string day = DateTimeOffset.TryParse(when, out DateTimeOffset d) ? d.LocalDateTime.ToString("MMM d") : "recently";
                    return "Someone else (" + who + ") changed this mod online on " + day + ". Press Check for Updates first if needed.";
                }
            }
            catch (GitHubException) { }
            return "";
        }

        // ------------------------------------------------------------------ publishing

        public async Task<string> PublishAsync(IReadOnlyList<ModPlan> plans, string message, IProgress<(int Percent, string Text)> progress, CancellationToken ct = default)
        {
            List<PublishFile> files = plans.SelectMany(p => p.Files.Where(f => f.Changed)).ToList();
            if (files.Count == 0) throw new InvalidOperationException("Nothing to publish.");

            int done = 0;
            foreach (PublishFile f in files)
            {
                progress.Report((done * 75 / files.Count, "Uploading " + (done + 1) + " of " + files.Count + ": " + Path.GetFileName(f.LocalPath)));
                var blob = new JsonObject
                {
                    ["content"] = Convert.ToBase64String(await File.ReadAllBytesAsync(f.LocalPath, ct)),
                    ["encoding"] = "base64"
                };
                JsonNode? made = await CallAsync(HttpMethod.Post, "/git/blobs", blob, ct);
                f.BlobSha = made!["sha"]!.GetValue<string>();
                done++;
            }

            progress.Report((80, "Committing your changes to GitHub..."));
            string committedSha = "";
            for (int attempt = 1; ; attempt++)
            {
                (string head, string baseTree) = await GetHeadAsync(ct);
                var entries = new JsonArray();
                foreach (PublishFile f in files)
                    entries.Add(new JsonObject { ["path"] = f.RemotePath, ["mode"] = "100644", ["type"] = "blob", ["sha"] = f.BlobSha });

                JsonNode? tree = await CallAsync(HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                var parents = new JsonArray { head };
                JsonNode? commit = await CallAsync(HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = message,
                    ["tree"] = tree!["sha"]!.GetValue<string>(),
                    ["parents"] = parents
                }, ct);
                string sha = commit!["sha"]!.GetValue<string>();

                try
                {
                    await CallAsync(new HttpMethod("PATCH"), "/git/refs/heads/" + Branch, new JsonObject { ["sha"] = sha, ["force"] = false }, ct);
                    committedSha = sha;
                    break;
                }
                catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 4)
                {
                    progress.Report((85, "Someone else just published. Trying again on top of it..."));
                    await Task.Delay(1500, ct);
                }
            }

            // Also immediately repackage & upload Zoid-Gundam-Mod.zip to the "latest" release so everyone gets it right away!
            progress.Report((90, "Updating the live release zip on GitHub..."));
            await RepackageAndUploadReleaseAsync(committedSha, new Progress<string>(s => progress.Report((95, s))), ct);
            progress.Report((100, "Published and live!"));
            return committedSha;
        }
    }
}
