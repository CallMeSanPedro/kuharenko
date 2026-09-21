using System;
using System.Windows;
using System.Windows.Media;

namespace Store.Views.Theming
{
    public static class UiTheme
    {
        public static void Apply(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Application.Current == null)
                return;

            try
            {
                var uri = new Uri(
                    $"pack://application:,,,/Store.Views;component/Themes/TabTheme.{name}.xaml",
                    UriKind.Absolute);

                var dict = new ResourceDictionary { Source = uri };
                var appResources = Application.Current.Resources;

                // Перезаписываем глобальные ключи кистей
                foreach (var key in dict.Keys)
                {
                    appResources[key] = dict[key];
                }
            }
            catch
            {
                // Fallback прямо в коде, если словарь не найден в ресурсах сборки
                ApplyFallback(name);
            }
        }

        private static void ApplyFallback(string name)
        {
            var res = Application.Current.Resources;

            SolidColorBrush headerBg, headerText, headerMuted;
            SolidColorBrush pageBg, cardBg, cardBorder;
            SolidColorBrush textPrimary, textSecondary;
            SolidColorBrush accent, accentSoft, inputBorder;
            SolidColorBrush tabIdle, tabSelected, tabUnderline;

            switch (name)
            {
                case "Contrast":
                    headerBg = new SolidColorBrush(Color.FromRgb(0x0A, 0x1E, 0x3D));
                    headerText = Brushes.White;
                    headerMuted = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD));

                    pageBg = new SolidColorBrush(Color.FromRgb(0xED, 0xF2, 0xF7));
                    cardBg = Brushes.White;
                    cardBorder = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

                    textPrimary = Brushes.Black;
                    textSecondary = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));

                    accent = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                    accentSoft = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE));
                    inputBorder = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                    tabIdle = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                    tabSelected = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                    tabUnderline = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                    break;

                case "Classic":
                    headerBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));
                    headerText = Brushes.White;
                    headerMuted = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));

                    pageBg = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                    cardBg = Brushes.White;
                    cardBorder = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));

                    textPrimary = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
                    textSecondary = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));

                    accent = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
                    accentSoft = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
                    inputBorder = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));

                    tabIdle = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
                    tabSelected = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
                    tabUnderline = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
                    break;

                case "Legacy":
                    headerBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x28, 0x33));
                    headerText = Brushes.White;
                    headerMuted = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));

                    pageBg = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF));
                    cardBg = Brushes.White;
                    cardBorder = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

                    textPrimary = Brushes.Black;
                    textSecondary = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

                    accent = new SolidColorBrush(Color.FromRgb(0x00, 0x72, 0xC6));
                    accentSoft = new SolidColorBrush(Color.FromRgb(0xCD, 0xE6, 0xF7));
                    inputBorder = new SolidColorBrush(Color.FromRgb(0xAB, 0xAD, 0xB3));

                    tabIdle = Brushes.Black;
                    tabSelected = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
                    tabUnderline = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
                    break;

                default: // Calm
                    headerBg = new SolidColorBrush(Color.FromRgb(0x0F, 0x2D, 0x5B));
                    headerText = Brushes.White;
                    headerMuted = new SolidColorBrush(Color.FromRgb(0xC7, 0xD2, 0xE8));

                    pageBg = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xFB));
                    cardBg = Brushes.White;
                    cardBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));

                    textPrimary = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                    textSecondary = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                    accent = new SolidColorBrush(Color.FromRgb(0x1B, 0x3F, 0x6F));
                    accentSoft = new SolidColorBrush(Color.FromRgb(0xE8, 0xF1, 0xFB));
                    inputBorder = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

                    tabIdle = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                    tabSelected = new SolidColorBrush(Color.FromRgb(0x1B, 0x3F, 0x6F));
                    tabUnderline = new SolidColorBrush(Color.FromRgb(0x1B, 0x3F, 0x6F));
                    break;
            }

            res["HeaderBarBg"] = headerBg;
            res["HeaderBarText"] = headerText;
            res["HeaderBarMuted"] = headerMuted;

            res["PageBg"] = pageBg;
            res["CardBg"] = cardBg;
            res["CardBorder"] = cardBorder;

            res["TextPrimary"] = textPrimary;
            res["TextSecondary"] = textSecondary;

            res["Accent"] = accent;
            res["AccentSoft"] = accentSoft;
            res["InputBorder"] = inputBorder;

            res["Tab.IdleForeground"] = tabIdle;
            res["Tab.SelectedForeground"] = tabSelected;
            res["Tab.SelectedUnderline"] = tabUnderline;
        }
    }
}
