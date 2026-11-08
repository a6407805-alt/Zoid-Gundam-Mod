// ============================================================================================================
//  AUTO-SYNC (SILENT STAGING & DIRECT PUBLISH) FOR ZOID GUNDAM MOD (RUSTED WARFARE).
//
//  1. Keeps Desktop\Zoid Gundam and Rusted Warfare\mods\units\Zoid Gundam in sync locally (newest file wins).
//  2. Compares local unit/building files in Rusted Warfare\mods\units\Zoid Gundam against GitHub main.
//  3. Uploads any new or edited files to main AND immediately updates the "latest" release zip (Zoid-Gundam-Mod.zip)
//     and release-info.json so friends get the update right away when they press PLAY or CHECK FOR UPDATES.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ZoidGundamLauncher
{
    public sealed class StagingStatus
    {
        public string Branch { get; init; } = "";
        public string Creator { get; init; } = "";
        public string Author { get; init; } = "";
        public string PackVersion { get; init; } = "";
        public DateTimeOffset Date { get; init; }
        public List<(string Path, string Status, string? Sha)> Files { get; init; } = new List<(string, string, string?)>();
        public IEnumerable<string> Mods => Files.Select(f => f.Path.Split('/')).Where(p => p.Length > 1).Select(p => p[1]).Distinct();
    }

    public sealed class UploadItem
    {
        public string Sha { get; init; } = "";
        public string Title { get; init; } = "";
        public string Author { get; init; } = "";
        public DateTimeOffset Date { get; init; }
        public string Build { get; init; } = "";
        public bool FromCreator { get; init; }
        public string Url { get; init; } = "";
    }

    public static class StagingSync
    {
        private const string Repo = Publisher.Repo;
        private const string Main = "main";
        private const string Prefix = "staging/";

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoidGundamLauncher", "1.0"));
            return c;
        }

        public static string? ReadCreatorKey() => Publisher.ReadSavedKey();

        public static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoidGundam", "staging-log.txt");

        public static void Log(string line)
        {
            Debug.WriteLine("Staging: " + line);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                var lines = File.Exists(LogFile) ? File.ReadAllLines(LogFile).TakeLast(300).ToList() : new List<string>();
                lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line);
                File.WriteAllLines(LogFile, lines);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        private static JsonArray Parents(string sha) { var a = new JsonArray(); a.Add(sha); return a; }

        private static async Task<JsonNode?> CallAsync(string key, HttpMethod method, string path, JsonNode? body = null, CancellationToken ct = default)
        {
            string url = path.StartsWith("http", StringComparison.Ordinal) ? path : "https://api.github.com/repos/" + Repo + path;
            using var req = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(key)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage r = await Http.SendAsync(req, ct);
            string text = await r.Content.ReadAsStringAsync(ct);
            if (!r.IsSuccessStatusCode) throw new GitHubException((int)r.StatusCode, "GitHub said " + (int)r.StatusCode + ": " + text);
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        private static async Task<string?> RefShaAsync(string key, string branch, CancellationToken ct)
        {
            try { return (await CallAsync(key, HttpMethod.Get, "/git/ref/heads/" + branch, null, ct))?["object"]?["sha"]?.GetValue<string>(); }
            catch (GitHubException ex) when (ex.Status == 404) { return null; }
        }

        private static async Task<string> TreeOfAsync(string key, string commitSha, CancellationToken ct) =>
            (await CallAsync(key, HttpMethod.Get, "/git/commits/" + commitSha, null, ct))!["tree"]!["sha"]!.GetValue<string>();

        private static async Task SetRefAsync(string key, string branch, string sha, bool force, CancellationToken ct)
        {
            if (await RefShaAsync(key, branch, ct) == null)
                await CallAsync(key, HttpMethod.Post, "/git/refs", new JsonObject { ["ref"] = "refs/heads/" + branch, ["sha"] = sha }, ct);
            else
                await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + branch, new JsonObject { ["sha"] = sha, ["force"] = force }, ct);
        }

        public static async Task<(string Login, bool IsOwner)> WhoAmIAsync(string key, CancellationToken ct = default)
        {
            string login = (await CallAsync(key, HttpMethod.Get, "https://api.github.com/user", null, ct))?["login"]?.GetValue<string>() ?? "";
            return (login, login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsOwner(string login) => login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase);

        public static async Task<List<StagingStatus>> CheckAllStagingAsync(string key, CancellationToken ct = default)
        {
            var found = new List<StagingStatus>();
            JsonNode? refs = await CallAsync(key, HttpMethod.Get, "/git/matching-refs/heads/" + Prefix, null, ct);
            foreach (JsonNode? r in refs?.AsArray() ?? new JsonArray())
            {
                string branch = (r?["ref"]?.GetValue<string>() ?? "").Replace("refs/heads/", "");
                if (branch.Length == 0) continue;
                JsonNode? cmp = await CallAsync(key, HttpMethod.Get, "/compare/" + Main + "..." + Uri.EscapeDataString(branch).Replace("%2F", "/"), null, ct);
                if ((cmp?["ahead_by"]?.GetValue<int>() ?? 0) == 0) continue;
                var files = new List<(string, string, string?)>();
                foreach (JsonNode? f in cmp?["files"]?.AsArray() ?? new JsonArray())
                    if (f != null)
                        files.Add((f["filename"]!.GetValue<string>(), f["status"]?.GetValue<string>() ?? "modified", f["sha"]?.GetValue<string>()));
                if (files.Count == 0) continue;
                JsonNode? last = cmp?["commits"]?.AsArray().LastOrDefault();
                string when = last?["commit"]?["author"]?["date"]?.GetValue<string>() ?? "";
                found.Add(new StagingStatus
                {
                    Branch = branch,
                    Creator = branch.Substring(Prefix.Length),
                    PackVersion = System.Text.RegularExpressions.Regex.Match(last?["commit"]?["message"]?.GetValue<string>() ?? "", @"pack=([0-9a-f]{7,40})").Groups[1].Value,
                    Author = last?["author"]?["login"]?.GetValue<string>() ?? last?["commit"]?["author"]?["name"]?.GetValue<string>() ?? branch.Substring(Prefix.Length),
                    Date = DateTimeOffset.TryParse(when, out DateTimeOffset d) ? d : DateTimeOffset.MinValue,
                    Files = files
                });
            }
            return found;
        }

        public static async Task<string> ApproveAndPublishAsync(string key, StagingStatus staged, CancellationToken ct = default)
        {
            for (int attempt = 1; ; attempt++)
            {
                string head = (await RefShaAsync(key, Main, ct))!;
                string baseTree = await TreeOfAsync(key, head, ct);
                var entries = new JsonArray();
                foreach ((string path, string status, string? sha) in staged.Files)
                {
                    if (status == "removed") entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = null });
                    else if (sha != null) entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = sha });
                }
                JsonNode? tree = await CallAsync(key, HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                string mods = string.Join(", ", staged.Mods);
                JsonNode? commit = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = "Merge " + staged.Creator + " mod update" + (mods.Length > 0 ? " (" + mods + ")" : "") + " (approved by owner)",
                    ["tree"] = tree!["sha"]!.GetValue<string>(),
                    ["parents"] = Parents(head)
                }, ct);
                string sha2 = commit!["sha"]!.GetValue<string>();
                try
                {
                    await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + Main, new JsonObject { ["sha"] = sha2, ["force"] = false }, ct);
                }
                catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 4) { await Task.Delay(1500, ct); continue; }
                await SetRefAsync(key, staged.Branch, sha2, true, ct);
                await new Publisher(key).RepackageAndUploadReleaseAsync(sha2, null, ct);
                return sha2;
            }
        }

        public static async Task<List<UploadItem>> RecentUploadsAsync(string? key, int count = 20, CancellationToken ct = default)
        {
            JsonNode? commits = await CallAsync(key ?? "", HttpMethod.Get, "/commits?sha=" + Main + "&per_page=" + count, null, ct);
            string latestRelSha = await LatestReleaseShaAsync(key ?? "", ct);
            var list = new List<UploadItem>();
            foreach (JsonNode? c in commits?.AsArray() ?? new JsonArray())
            {
                if (c == null) continue;
                string sha = c["sha"]!.GetValue<string>();
                string msg = c["commit"]?["message"]?.GetValue<string>() ?? "";
                string title = msg.Split('\n')[0];
                string build = msg.Contains("[skip ci]") ? "part" : "built";
                list.Add(new UploadItem
                {
                    Sha = sha,
                    Title = title,
                    Author = c["author"]?["login"]?.GetValue<string>() ?? c["commit"]?["author"]?["name"]?.GetValue<string>() ?? "",
                    Date = DateTimeOffset.TryParse(c["commit"]?["author"]?["date"]?.GetValue<string>(), out DateTimeOffset d) ? d : DateTimeOffset.MinValue,
                    Build = build,
                    FromCreator = title.StartsWith("Auto-update from", StringComparison.Ordinal) || title.Contains("(approved by owner)"),
                    Url = c["html_url"]?.GetValue<string>() ?? ""
                });
            }
            return list;
        }

        public static readonly string BellSeenFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoidGundam", "bell-seen.txt");

        public static DateTimeOffset BellSeen()
        {
            try { return File.Exists(BellSeenFile) && DateTimeOffset.TryParse(File.ReadAllText(BellSeenFile), out DateTimeOffset d) ? d : DateTimeOffset.UtcNow.AddDays(-2); }
            catch (IOException) { return DateTimeOffset.UtcNow.AddDays(-2); }
        }

        public static void MarkBellSeen()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(BellSeenFile)!); File.WriteAllText(BellSeenFile, DateTimeOffset.UtcNow.ToString("o")); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        public static async Task<string> LatestReleaseShaAsync(string key, CancellationToken ct = default)
        {
            try { return (await CallAsync(key, HttpMethod.Get, "/releases/latest", null, ct))?["target_commitish"]?.GetValue<string>() ?? ""; }
            catch (GitHubException) { return ""; }
        }

        public static async Task RevertStagingAsync(string key, StagingStatus staged, CancellationToken ct = default)
        {
            string head = (await RefShaAsync(key, Main, ct))!;
            await SetRefAsync(key, staged.Branch, head, true, ct);
        }

        // ------------------------------------------------------------------ Rusted Warfare game & Desktop folder sync

        public static List<string> FindGameFolders()
        {
            var roots = new List<string>();
            foreach ((Microsoft.Win32.RegistryKey hive, string sub) in new[]
            {
                (Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam"),
                (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
                (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam"),
            })
            {
                try
                {
                    using var k = hive.OpenSubKey(sub);
                    foreach (string name in new[] { "SteamPath", "InstallPath" })
                        if (k?.GetValue(name) is string v && v.Length > 0) roots.Add(v.Replace('/', '\\'));
                }
                catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException) { }
            }
            var libs = new List<string>(roots);
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch (IOException) { }
            }
            return libs.Distinct(StringComparer.OrdinalIgnoreCase)
                       .Select(l => Path.Combine(l, "steamapps", "common", "Rusted Warfare"))
                       .Where(g => File.Exists(Path.Combine(g, "Rusted Warfare.exe")) || File.Exists(Path.Combine(g, "Rusted Warfare - 64.exe")))
                       .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Bi-directionally syncs Desktop\Zoid Gundam and Rusted Warfare\mods\units\Zoid Gundam so whichever folder you
        /// edited most recently has its changes mirrored to the other before syncing with GitHub.
        /// </summary>
        public static void SyncDesktopAndGameModFolders(string gameUnitsDir)
        {
            try
            {
                string desktopZoid = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Zoid Gundam");
                if (!Directory.Exists(desktopZoid)) return;
                string gameZoid = Path.Combine(gameUnitsDir, "Zoid Gundam");
                Directory.CreateDirectory(gameZoid);

                foreach (string src in Directory.EnumerateFiles(desktopZoid, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(src);
                    if (name.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = Path.GetRelativePath(desktopZoid, src);
                    string dst = Path.Combine(gameZoid, rel);
                    if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst).AddSeconds(2))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        File.Copy(src, dst, true);
                    }
                }
                foreach (string src in Directory.EnumerateFiles(gameZoid, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(src);
                    if (name.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = Path.GetRelativePath(gameZoid, src);
                    string dst = Path.Combine(desktopZoid, rel);
                    if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst).AddSeconds(2))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        File.Copy(src, dst, true);
                    }
                }
            }
            catch (Exception ex) { Log("Local folder sync note: " + ex.Message); }
        }

        private const int ChunkFiles = 25;
        private const long ChunkBytes = 25L * 1024 * 1024;

        private static async Task<string> UploadBlobAsync(string key, string local, IProgress<string>? progress, CancellationToken ct)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    JsonNode? blob = await CallAsync(key, HttpMethod.Post, "/git/blobs", new JsonObject
                    {
                        ["content"] = Convert.ToBase64String(await File.ReadAllBytesAsync(local, ct)),
                        ["encoding"] = "base64"
                    }, ct);
                    return blob!["sha"]!.GetValue<string>();
                }
                catch (GitHubException ex) when ((ex.Status == 403 || ex.Status == 429) && attempt < 6)
                {
                    progress?.Report("GitHub rate limit reached. Waiting 60s, then resuming...");
                    await Task.Delay(TimeSpan.FromSeconds(60), ct);
                }
                catch (Exception ex) when ((ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested)) && attempt < 4)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
                }
            }
        }

        private static async Task<string> CommitOnMainAsync(string key, JsonArray entries, string message, CancellationToken ct)
        {
            for (int attempt = 1; ; attempt++)
            {
                string head = (await RefShaAsync(key, Main, ct))!;
                string baseTree = await TreeOfAsync(key, head, ct);
                JsonNode? tree = await CallAsync(key, HttpMethod.Post, "/git/trees",
                    new JsonObject { ["base_tree"] = baseTree, ["tree"] = JsonNode.Parse(entries.ToJsonString()) }, ct);
                JsonNode? commit = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = message, ["tree"] = tree!["sha"]!.GetValue<string>(), ["parents"] = Parents(head)
                }, ct);
                string sha = commit!["sha"]!.GetValue<string>();
                try
                {
                    await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + Main,
                        new JsonObject { ["sha"] = sha, ["force"] = false }, ct);
                    return sha;
                }
                catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 5) { await Task.Delay(1500, ct); }
            }
        }

        public static async Task<int> SilentStageLocalModAsync(string key, string login, string? installedVersion, DateTime installedUtc,
                                                              string? onlyMod = null, CancellationToken ct = default, bool autoPublish = true,
                                                              IProgress<string>? progress = null)
        {
            int pending = 0;
            try
            {
                string? game = FindGameFolders().FirstOrDefault();
                string unitsDir;
                if (game != null)
                {
                    unitsDir = Path.Combine(game, "mods", "units");
                    Directory.CreateDirectory(unitsDir);
                    SyncDesktopAndGameModFolders(unitsDir);
                }
                else
                {
                    string desktopParent = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!Directory.Exists(Path.Combine(desktopParent, "Zoid Gundam")))
                    {
                        Log("No Rusted Warfare folder or Desktop\\Zoid Gundam found.");
                        return 0;
                    }
                    unitsDir = desktopParent;
                }

                string? head = await RefShaAsync(key, Main, ct);
                if (head == null) return 0;

                string release = autoPublish ? await LatestReleaseShaAsync(key, ct) : "";
                bool packIsCurrent = !autoPublish || (installedVersion != null && release.Length > 0 &&
                                     release.StartsWith(installedVersion, StringComparison.OrdinalIgnoreCase));
                string baseTree = await TreeOfAsync(key, head, ct);
                JsonNode? tree = await CallAsync(key, HttpMethod.Get, "/git/trees/" + baseTree + "?recursive=1", null, ct);
                var onMain = new Dictionary<string, string>(StringComparer.Ordinal);
                var packMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Zoid Gundam" };
                foreach (JsonNode? item in tree?["tree"]?.AsArray() ?? new JsonArray())
                {
                    if (item?["type"]?.GetValue<string>() != "blob") continue;
                    string p = item["path"]!.GetValue<string>();
                    onMain[p] = item["sha"]!.GetValue<string>();
                    string[] parts = p.Split('/');
                    if (parts.Length >= 3 && parts[0] == "mods") packMods.Add(parts[1]);
                }

                var changed = new List<(string Remote, string Local)>();
                int checkedFiles = 0;
                progress?.Report("Auto-syncing your Zoid Gundam units with GitHub...");
                await Task.Run(() =>
                {
                    foreach (string modDir in Directory.GetDirectories(unitsDir))
                    {
                        string folder = Path.GetFileName(modDir);
                        string? mod = packMods.FirstOrDefault(m => m.Equals(folder, StringComparison.OrdinalIgnoreCase));
                        if (mod == null) continue;
                        if (onlyMod != null && !mod.Equals(onlyMod, StringComparison.OrdinalIgnoreCase)) continue;

                        foreach (string file in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories))
                        {
                            var info = new FileInfo(file);
                            if (info.Name.StartsWith("Zoid-Gundam-", StringComparison.OrdinalIgnoreCase)) continue;
                            if (info.Length > 90L * 1024 * 1024) continue;
                            string rel = Path.GetRelativePath(modDir, file).Replace('\\', '/');
                            string ext = info.Extension.ToLowerInvariant();
                            if (ext == ".log" || ext == ".tmp" || ext == ".bak" || ext == ".old" || ext == ".new") continue;
                            string remote = "mods/" + mod + "/" + rel;
                            if (!packIsCurrent && onMain.ContainsKey(remote) && info.LastWriteTimeUtc <= installedUtc.AddMinutes(1)) continue;
                            if (++checkedFiles % 40 == 0) progress?.Report("Checking local mod files... " + checkedFiles + " checked");
                            if (onMain.TryGetValue(remote, out string? sha) && sha == Publisher.GitBlobSha(file)) continue;
                            changed.Add((remote, file));
                        }
                    }
                }, ct);

                pending = changed.Count;
                Log("Checked " + checkedFiles + " file(s), " + changed.Count + " differ from GitHub.");
                if (changed.Count == 0) return 0;

                if (autoPublish)
                {
                    var ordered = changed.OrderBy(c => c.Remote.Split('/')[1], StringComparer.OrdinalIgnoreCase)
                                         .ThenBy(c => new FileInfo(c.Local).Length).ToList();
                    var chunks = new List<List<(string Remote, string Local)>>();
                    var cur = new List<(string Remote, string Local)>();
                    long curBytes = 0;
                    foreach (var c in ordered)
                    {
                        long len = new FileInfo(c.Local).Length;
                        if (cur.Count > 0 && (cur.Count >= ChunkFiles || curBytes + len > ChunkBytes)) { chunks.Add(cur); cur = new List<(string, string)>(); curBytes = 0; }
                        cur.Add(c); curBytes += len;
                    }
                    if (cur.Count > 0) chunks.Add(cur);

                    long total = changed.Sum(c => new FileInfo(c.Local).Length), sent = 0;
                    int done = 0;
                    string lastCommitSha = head;
                    for (int k = 0; k < chunks.Count; k++)
                    {
                        var chunk = chunks[k];
                        var chunkEntries = new JsonArray();
                        foreach ((string remote, string local) in chunk)
                        {
                            progress?.Report("Auto-syncing mod changes: file " + (++done) + " of " + changed.Count +
                                             " (" + (sent / 1048576.0).ToString("0.0") + " / " + (total / 1048576.0).ToString("0.0") + " MB)");
                            string blobSha = await UploadBlobAsync(key, local, progress, ct);
                            sent += new FileInfo(local).Length;
                            chunkEntries.Add(new JsonObject { ["path"] = remote, ["mode"] = "100644", ["type"] = "blob", ["sha"] = blobSha });
                        }
                        bool last = k == chunks.Count - 1;
                        var chunkMods = chunk.Select(c => c.Remote.Split('/')[1]).Distinct().ToList();
                        string message = "Auto-update from " + login + (chunks.Count > 1 ? " (part " + (k + 1) + " of " + chunks.Count + ")" : "") + ": " +
                                         chunk.Count + " file(s) in " + string.Join(", ", chunkMods) +
                                         "\n\nSent automatically by " + login + "'s Zoid Gundam launcher. pack=" + (installedVersion ?? "none") +
                                         (last ? "" : " [skip ci]");
                        lastCommitSha = await CommitOnMainAsync(key, chunkEntries, message, ct);
                        pending -= chunk.Count;
                    }

                    // Immediately update the live GitHub Release zip and release-info.json!
                    await new Publisher(key).RepackageAndUploadReleaseAsync(lastCommitSha, progress, ct);
                    Log("Auto-synced and published " + changed.Count + " file(s) live to GitHub release (" + lastCommitSha + ").");
                    return changed.Count;
                }

                string branch = Prefix + login.ToLowerInvariant();
                var entries = new JsonArray();
                foreach ((string remote, string local) in changed)
                {
                    string blobSha = await UploadBlobAsync(key, local, progress, ct);
                    entries.Add(new JsonObject { ["path"] = remote, ["mode"] = "100644", ["type"] = "blob", ["sha"] = blobSha });
                }
                JsonNode? newTree = await CallAsync(key, HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                string newTreeSha = newTree!["sha"]!.GetValue<string>();
                string? current = await RefShaAsync(key, branch, ct);
                if (current != null && await TreeOfAsync(key, current, ct) == newTreeSha) return changed.Count;

                var mods = changed.Select(c => c.Remote.Split('/')[1]).Distinct();
                JsonNode? commit = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = "Staging: " + login + " changed " + changed.Count + " file(s) in " + string.Join(", ", mods) +
                                  "\n\npack=" + (installedVersion ?? "none") + " [skip ci]",
                    ["tree"] = newTreeSha,
                    ["parents"] = Parents(head)
                }, ct);
                await SetRefAsync(key, branch, commit!["sha"]!.GetValue<string>(), true, ct);
                return changed.Count;
            }
            catch (Exception ex)
            {
                Log("Skipped (error): " + ex.Message + (pending > 0 ? "  (" + pending + " file(s) NOT sent)" : ""));
                return pending > 0 ? -pending : 0;
            }
        }
    }
}
