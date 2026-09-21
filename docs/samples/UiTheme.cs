using System;
using System.Linq;
using System.Windows;

namespace Store.Views.Theming
{
    public static class UiTheme
    {
        private const string ThemePrefix = "Theme.";

        /// <summary>
        /// Переключает активную тему всего приложения ("Modern", "Legacy", "Contrast").
        /// Заменяет целый ResourceDictionary темы: обновляются цвета, форма табов, скругления рамок.
        /// </summary>
        public static void Apply(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName) || Application.Current == null)
                return;

            var merged = Application.Current.Resources.MergedDictionaries;

            // 1. Ищем и удаляем ранее загруженный словарь темы
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source?.OriginalString;
                if (!string.IsNullOrEmpty(source) && source.Contains(ThemePrefix))
                {
                    merged.RemoveAt(i);
                }
            }

            // 2. Подключаем новый словарь темы
            var newThemeUri = new Uri(
                $"pack://application:,,,/Store.Views;component/Themes/Theme.{themeName}.xaml",
                UriKind.Absolute);

            try
            {
                merged.Add(new ResourceDictionary { Source = newThemeUri });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
            }
        }
    }
}
