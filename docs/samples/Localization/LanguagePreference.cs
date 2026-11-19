using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Store.Views.Localization
{
    /// <summary>
    /// Выбранный язык рядом с темой. Свой файл, настройки темы и закреплений не трогает.
    /// %LocalAppData%/Store/language.json, ключ — SHA256 логина, как у темы.
    /// </summary>
    public static class LanguagePreference
    {
        public const string Russian = "ru";
        public const string English = "en";

        public static string FilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Store",
            "language.json");

        public static string Load(string login)
        {
            var all = Read();
            string value;
            return all.TryGetValue(Key(login), out value) ? Normalize(value) : Russian;
        }

        public static void Save(string login, string culture)
        {
            var all = Read();
            all[Key(login)] = Normalize(culture);
            Write(all);
        }

        /// <summary>
        /// culture == null — взять сохранённый язык и применить.
        /// Иначе сохранить и применить. Вызывать после входа, до Show главного окна, и из пункта меню.
        /// </summary>
        public static void Apply(string login, string culture = null)
        {
            var name = culture == null ? Load(login) : Normalize(culture);
            if (culture != null)
                Save(login, name);
            Translation.Instance.SetCulture(name);
        }

        public static string Normalize(string culture)
        {
            if (string.Equals(culture, English, StringComparison.OrdinalIgnoreCase))
                return English;
            return Russian;
        }

        private static string Key(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                return "default";

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(login));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private static Dictionary<string, string> Read()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                return new Dictionary<string, string>();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }

        private static void Write(Dictionary<string, string> values)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
