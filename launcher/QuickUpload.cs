// ============================================================================================================
//  QUICK UPLOAD (Creator window) for Zoid Gundam Mod (Rusted Warfare):
//  Pick or drag a friend's .zip, unit folder, .ini, .png, .gif, .ogg, or .dll into the box, choose the mod name
//  (defaults to "Zoid Gundam"), and stage it for scanning/publishing.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace ZoidGundamLauncher
{
    public static class QuickUpload
    {
        public static readonly string StageRoot = Path.Combine(Path.GetTempPath(), "ZoidGundam-QuickUpload");
        private static readonly string[] OtherArchives = { ".rar", ".7z", ".tar", ".gz", ".tgz" };

        public static bool IsValidName(string name) => Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9_.\-\s]{0,63}$");

        public static string GuessName(IReadOnlyList<string> picked)
        {
            if (picked.Count == 0) return "Zoid Gundam";
            string first = picked[0];
            string name = Directory.Exists(first) ? Path.GetFileName(first.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(first);
            if (name.IndexOf("Zoid", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Gundam", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Zoid Gundam";
            return "Zoid Gundam";
        }

        public sealed class Result
        {
            public string Folder { get; init; } = "";
            public List<string> Included { get; } = new List<string>();
            public List<string> Skipped { get; } = new List<string>();
            public bool Multi => Included.Count > 1 || Skipped.Count > 0;
        }

        public static Result Stage(IReadOnlyList<string> picked, string modName, IReadOnlyCollection<string> knownMods)
        {
            try { if (Directory.Exists(StageRoot)) Directory.Delete(StageRoot, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }

            if (string.IsNullOrWhiteSpace(modName)) modName = "Zoid Gundam";
            string root = Path.Combine(StageRoot, Guid.NewGuid().ToString("N"));
            var result = new Result { Folder = root };
            Directory.CreateDirectory(Path.Combine(root, "mods"));

            foreach (string p in picked)
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                string? source = null;
                if (Directory.Exists(p)) source = ModRoot(p);
                else if (ext == ".zip")
                {
                    string unzip = Path.Combine(root, "unzip-" + Guid.NewGuid().ToString("N"));
                    ZipFile.ExtractToDirectory(p, unzip);
                    source = ModRoot(unzip);
                }
                else if (OtherArchives.Contains(ext))
                    throw new InvalidOperationException(Path.GetFileName(p) + " is a " + ext + " file. Extract it first, then pick the extracted folder.");

                if (!IsValidName(modName)) throw new InvalidOperationException("Type the mod name first (e.g. Zoid Gundam).");
                string dest = Path.Combine(root, "mods", modName);
                Directory.CreateDirectory(dest);
                if (source != null) CopyTree(source, dest);
                else File.Copy(p, Path.Combine(dest, Path.GetFileName(p)), true);
                if (!result.Included.Contains(modName, StringComparer.OrdinalIgnoreCase)) result.Included.Add(modName);
            }

            if (!Directory.EnumerateFiles(Path.Combine(root, "mods"), "*", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("There were no files in what you picked.");
            return result;
        }

        public static bool IsReservedName(string name) =>
            name.Equals("maps", StringComparison.OrdinalIgnoreCase) || name.Equals("saves", StringComparison.OrdinalIgnoreCase);

        public static string Short(List<string> names) => string.Join(", ", names.Take(6)) + (names.Count > 6 ? " and " + (names.Count - 6) + " more" : "");

        private static string ModRoot(string dir)
        {
            for (int i = 0; i < 6; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "BUILDINGS")) || Directory.Exists(Path.Combine(dir, "units"))) break;
                string[] files = Directory.GetFiles(dir);
                string[] dirs = Directory.GetDirectories(dir);
                if (files.Length == 0 && dirs.Length == 1) { dir = dirs[0]; continue; }
                break;
            }
            return dir;
        }

        private static void CopyTree(string from, string to)
        {
            foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }
    }
}
