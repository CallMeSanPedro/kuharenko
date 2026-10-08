using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Store.ViewModels.Tree
{
    public static class PinnedStoresStorage
    {
        private const string DefaultTheme = "Default";
        private static readonly object Gate = new object();
        private static readonly string[] ThemeNames = { "Default", "Modern", "Legacy", "Contrast" };
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static string HashLogin(string login)
        {
            if (string.IsNullOrEmpty(login))
                return string.Empty;

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(login));
                return Convert.ToHexString(bytes).ToLowerInvariant();
            }
        }

        public static List<decimal> Load(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return new List<decimal>();

                lock (Gate)
                {
                    var document = Read();
                    List<decimal> list;
                    if (document.Pinned != null && document.Pinned.TryGetValue(key, out list) && list != null)
                        return list;
                }
            }
            catch
            {
                // повреждённый или недоступный файл — игнорируем
            }

            return new List<decimal>();
        }

        public static void Save(string key, List<decimal> unids)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return;

                lock (Gate)
                {
                    var document = Read();
                    if (document.Pinned == null)
                        document.Pinned = new Dictionary<string, List<decimal>>();
                    document.Pinned[key] = unids ?? new List<decimal>();
                    Write(document);
                }
            }
            catch
            {
                // нет прав на запись — игнорируем
            }
        }

        public static string LoadTheme(string login)
        {
            try
            {
                var key = HashLogin(login);
                if (string.IsNullOrEmpty(key))
                    return DefaultTheme;

                lock (Gate)
                {
                    var document = Read();
                    string themeName;
                    if (document.Theme != null && document.Theme.TryGetValue(key, out themeName) && IsKnownTheme(themeName))
                        return themeName;
                }
            }
            catch
            {
                // повреждённый или недоступный файл — игнорируем
            }

            return DefaultTheme;
        }

        public static void SaveTheme(string login, string themeName)
        {
            try
            {
                var key = HashLogin(login);
                if (string.IsNullOrEmpty(key) || !IsKnownTheme(themeName))
                    return;

                lock (Gate)
                {
                    var document = Read();
                    if (document.Theme == null)
                        document.Theme = new Dictionary<string, string>();
                    document.Theme[key] = themeName;
                    Write(document);
                }
            }
            catch
            {
                // нет прав на запись — игнорируем
            }
        }

        private static string GetFilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Store",
                "settings.json");
        }

        private static bool IsKnownTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return false;

            foreach (var name in ThemeNames)
            {
                if (string.Equals(name, themeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static UserSettingsDocument Read()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new UserSettingsDocument();

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new UserSettingsDocument();

            using (var parsed = JsonDocument.Parse(json))
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                    return new UserSettingsDocument();

                if (IsLegacyPinFile(parsed.RootElement))
                {
                    var pins = JsonSerializer.Deserialize<Dictionary<string, List<decimal>>>(json)
                               ?? new Dictionary<string, List<decimal>>();
                    var migrated = new UserSettingsDocument { Pinned = pins };
                    Write(migrated);
                    return migrated;
                }
            }

            var document = JsonSerializer.Deserialize<UserSettingsDocument>(json, Options) ?? new UserSettingsDocument();
            if (document.Pinned == null)
                document.Pinned = new Dictionary<string, List<decimal>>();
            if (document.Theme == null)
                document.Theme = new Dictionary<string, string>();
            return document;
        }

        private static bool IsLegacyPinFile(JsonElement root)
        {
            var any = false;
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals("pinned", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("theme", StringComparison.OrdinalIgnoreCase))
                    return false;
                any = true;
                if (property.Value.ValueKind != JsonValueKind.Array)
                    return false;
            }

            return any;
        }

        private static void Write(UserSettingsDocument document)
        {
            if (document.Pinned == null)
                document.Pinned = new Dictionary<string, List<decimal>>();
            if (document.Theme == null)
                document.Theme = new Dictionary<string, string>();

            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(document, Options));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        private sealed class UserSettingsDocument
        {
            public Dictionary<string, List<decimal>> Pinned { get; set; }

            public Dictionary<string, string> Theme { get; set; }
        }
    }
}
