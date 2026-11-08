// ============================================================================================================
//  Roster data for the Zoid Gundam Launcher: unit/mecha cards, faction/building tiles and game mode tiles.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZoidGundamLauncher
{
    public sealed class CharacterEntry
    {
        public string Name { get; init; } = "";
        public string Tagline { get; init; } = "";
        public DateTimeOffset Added { get; init; }
        public ImageSource? Portrait { get; init; }
        public Color AccentColor { get; init; } = Color.FromRgb(0xFF, 0xC9, 0x28);
        public Brush AccentBrush => new SolidColorBrush(AccentColor);
        public bool IsNew { get; set; }
        public Visibility NewBadgeVisibility => IsNew ? Visibility.Visible : Visibility.Collapsed;

        public string Status { get; init; } = "";
        public string Version { get; init; } = "";
        public string Note { get; init; } = "";

        public string StatusText
        {
            get
            {
                string ver = string.IsNullOrWhiteSpace(Version) ? "" : " · v" + Version.TrimStart('v', 'V');
                return Status.ToLowerInvariant() switch
                {
                    "green" => "COMPLETE" + ver,
                    "active" or "working" => "ACTIVE WIP" + ver,
                    "yellow" => "PLAYABLE" + (string.IsNullOrWhiteSpace(Version) ? "" : " v" + Version.TrimStart('v', 'V')),
                    "red" => "STAGED" + ver,
                    _ => ""
                };
            }
        }

        public Brush StatusBrush => Status.ToLowerInvariant() switch
        {
            "green" => new SolidColorBrush(Color.FromRgb(0x4C, 0xD9, 0x64)),
            "active" or "working" => new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
            "yellow" => new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x2E)),
            "red" => new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)),
            _ => Brushes.Transparent
        };
        public Visibility StatusVisibility => StatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string CardTooltip
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Note)) return Tagline;
                return string.IsNullOrWhiteSpace(Tagline) ? Note : Tagline + "\n" + Note;
            }
        }
    }

    public sealed class TileEntry
    {
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string Badge { get; init; } = "";
        public ImageSource? Thumbnail { get; init; }
        public Visibility BadgeVisibility => string.IsNullOrWhiteSpace(Badge) ? Visibility.Collapsed : Visibility.Visible;
    }

    public sealed class RosterData
    {
        public List<CharacterEntry> Characters { get; } = new List<CharacterEntry>();
        public List<TileEntry> Maps { get; } = new List<TileEntry>();
        public List<TileEntry> Modes { get; } = new List<TileEntry>();

        public static RosterData Parse(string json, Func<string, Uri> imageBase)
        {
            var data = new RosterData();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("characters", out JsonElement chars))
                foreach (JsonElement c in chars.EnumerateArray())
                {
                    data.Characters.Add(new CharacterEntry
                    {
                        Name = Str(c, "name"),
                        Tagline = Str(c, "tagline"),
                        Added = DateTimeOffset.TryParse(Str(c, "added"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset d) ? d : DateTimeOffset.MinValue,
                        Portrait = Image(Str(c, "image"), imageBase),
                        AccentColor = ParseColor(Str(c, "accent")),
                        Status = Str(c, "status"),
                        Version = Str(c, "version"),
                        Note = Str(c, "note"),
                        IsNew = c.TryGetProperty("new", out JsonElement n) && n.ValueKind == JsonValueKind.True
                    });
                }

            ReadTiles(root, "maps", data.Maps, imageBase);
            ReadTiles(root, "modes", data.Modes, imageBase);

            data.Characters.Sort((a, b) => b.Added.CompareTo(a.Added));

            DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-14);
            foreach (CharacterEntry c in data.Characters.Take(3))
                if (c.Added >= cutoff) c.IsNew = true;

            return data;
        }

        private static void ReadTiles(JsonElement root, string key, List<TileEntry> into, Func<string, Uri> imageBase)
        {
            if (!root.TryGetProperty(key, out JsonElement list)) return;
            foreach (JsonElement t in list.EnumerateArray())
                into.Add(new TileEntry
                {
                    Title = Str(t, "title"),
                    Subtitle = Str(t, "subtitle"),
                    Badge = Str(t, "badge"),
                    Thumbnail = Image(Str(t, "image"), imageBase)
                });
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static Color ParseColor(string hex)
        {
            try { if (!string.IsNullOrWhiteSpace(hex)) return (Color)ColorConverter.ConvertFromString(hex); }
            catch (FormatException) { }
            return Color.FromRgb(0xFF, 0xC9, 0x28);
        }

        private static ImageSource? Image(string path, Func<string, Uri> imageBase)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(path) : imageBase(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 400;
                bmp.EndInit();
                return bmp;
            }
            catch (Exception ex) when (ex is UriFormatException || ex is System.IO.IOException || ex is NotSupportedException)
            {
                return null;
            }
        }
    }
}
