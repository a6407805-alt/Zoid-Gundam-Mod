// ============================================================================================================
//  Password-locked keys for Zoid Gundam Mod (AES-256-GCM + PBKDF2-SHA256).
//  Also automatically checks %APPDATA%\BrokenCharacters so the owner/friends who already play the RoR2 pack
//  don't have to re-enter their keys.
// ============================================================================================================

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ZoidGundamLauncher
{
    public static class KeyVault
    {
        private const int Iterations = 600_000;
        public const int MinPasswordLength = 8;
        public const string ExtrasPath = "launcher/keys/extras.json";
        public const string StagingPath = "launcher/keys/staging.json";

        public static string? ReadExtrasKey()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (string folder in new[] { "ZoidGundam", "BrokenCharacters" })
            {
                string f = Path.Combine(appData, folder, "extras-key.txt");
                try { if (File.Exists(f)) { string k = File.ReadAllText(f).Trim(); if (k.Length > 0) return k; } }
                catch (IOException) { }
            }
            return null;
        }

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoidGundamLauncher", "1.0"));
            return c;
        }

        public static bool LooksLikeKey(string text) =>
            text.StartsWith("github_pat_", StringComparison.Ordinal) || text.StartsWith("ghp_", StringComparison.Ordinal);

        public static string Lock(string secret, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            byte[] plain = Encoding.UTF8.GetBytes(secret);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];
            using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
            return new JsonObject
            {
                ["v"] = 1,
                ["iterations"] = Iterations,
                ["salt"] = Convert.ToBase64String(salt),
                ["nonce"] = Convert.ToBase64String(nonce),
                ["data"] = Convert.ToBase64String(cipher),
                ["tag"] = Convert.ToBase64String(tag),
            }.ToJsonString() + "\n";
        }

        public static string? Unlock(string lockedJson, string password)
        {
            try
            {
                JsonNode n = JsonNode.Parse(lockedJson)!;
                byte[] salt = Convert.FromBase64String(n["salt"]!.GetValue<string>());
                byte[] nonce = Convert.FromBase64String(n["nonce"]!.GetValue<string>());
                byte[] cipher = Convert.FromBase64String(n["data"]!.GetValue<string>());
                byte[] tag = Convert.FromBase64String(n["tag"]!.GetValue<string>());
                int rounds = n["iterations"]?.GetValue<int>() ?? Iterations;
                byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, 32);
                byte[] plain = new byte[cipher.Length];
                using (var aes = new AesGcm(key, 16)) aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException) { return null; }
            catch (Exception ex) when (ex is FormatException || ex is NullReferenceException || ex is System.Text.Json.JsonException) { return null; }
        }

        public static async Task<string?> FetchAsync(string repoPath, CancellationToken ct = default)
        {
            string url = "https://raw.githubusercontent.com/" + Publisher.Repo + "/main/" + repoPath + "?t=" + DateTime.UtcNow.Ticks;
            using HttpResponseMessage resp = await Http.GetAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }

        public static async Task<(string? Key, string? Error)> UnlockFromRepoAsync(string repoPath, string password, CancellationToken ct = default)
        {
            string? locked;
            try { locked = await FetchAsync(repoPath, ct); }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return (null, "Couldn't reach GitHub. Check your internet and try again.");
            }
            if (locked == null) return (null, "No password has been set up for that yet. Ask the pack owner.");
            string? key = await Task.Run(() => Unlock(locked, password), ct);
            return key == null ? (null, "Wrong password.") : (key, null);
        }
    }
}
