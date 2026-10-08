using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Store.ViewModels.Tree
{
    public static class UserSettingsStorage
    {
        public const string PinnedSection = "pinned";
        public const string ThemeSection = "theme";

        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
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

        public static T Load<T>(string section, string key, T fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                    return fallback;

                lock (Gate)
                {
                    var document = Read();
                    Dictionary<string, JsonElement> values;
                    JsonElement element;
                    if (document.TryGetValue(section, out values)
                        && values != null
                        && values.TryGetValue(key, out element))
                        return JsonSerializer.Deserialize<T>(element.GetRawText(), Options);
                }
            }
            catch
            {
                // повреждённый или недоступный файл — игнорируем
            }

            return fallback;
        }

        public static void Save<T>(string section, string key, T value)
        {
            try
            {
                if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                    return;

                lock (Gate)
                {
                    var document = Read();
                    Dictionary<string, JsonElement> values;
                    if (!document.TryGetValue(section, out values) || values == null)
                    {
                        values = new Dictionary<string, JsonElement>();
                        document[section] = values;
                    }

                    values[key] = ToElement(value);
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

        private static Dictionary<string, Dictionary<string, JsonElement>> Read()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);

            using (var parsed = JsonDocument.Parse(json))
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                    return new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);

                if (IsLegacyPinFile(parsed.RootElement))
                {
                    var pins = JsonSerializer.Deserialize<Dictionary<string, List<decimal>>>(json)
                               ?? new Dictionary<string, List<decimal>>();
                    var migrated = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
                    {
                        [PinnedSection] = ToSection(pins)
                    };
                    Write(migrated);
                    return migrated;
                }
            }

            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options)
                       ?? new Dictionary<string, JsonElement>();
            var document = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in root)
            {
                if (section.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var values = new Dictionary<string, JsonElement>();
                foreach (var item in section.Value.EnumerateObject())
                    values[item.Name] = item.Value.Clone();
                document[section.Key] = values;
            }

            return document;
        }

        private static bool IsLegacyPinFile(JsonElement root)
        {
            var any = false;
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(PinnedSection, StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals(ThemeSection, StringComparison.OrdinalIgnoreCase))
                    return false;
                any = true;
                if (property.Value.ValueKind != JsonValueKind.Array)
                    return false;
            }

            return any;
        }

        private static void Write(Dictionary<string, Dictionary<string, JsonElement>> document)
        {
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

        private static Dictionary<string, JsonElement> ToSection(Dictionary<string, List<decimal>> pins)
        {
            var section = new Dictionary<string, JsonElement>();
            foreach (var pin in pins)
                section[pin.Key] = ToElement(pin.Value ?? new List<decimal>());
            return section;
        }

        private static JsonElement ToElement<T>(T value)
        {
            using (var parsed = JsonDocument.Parse(JsonSerializer.Serialize(value, Options)))
                return parsed.RootElement.Clone();
        }
    }
}
