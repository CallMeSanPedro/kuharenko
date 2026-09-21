using System;
using System.Windows;

namespace Store.Views.Theming
{
    public static class UiTheme
    {
        public static void Apply(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Application.Current == null)
                return;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            for (var i = dictionaries.Count - 1; i >= 0; i--)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (source != null && source.IndexOf("TabTheme.", StringComparison.OrdinalIgnoreCase) >= 0)
                    dictionaries.RemoveAt(i);
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/Store.Views;component/Themes/TabTheme.{name}.xaml",
                    UriKind.Absolute)
            });
        }
    }
}
