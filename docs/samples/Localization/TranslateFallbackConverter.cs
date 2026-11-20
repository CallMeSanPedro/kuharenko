using System;
using System.Globalization;
using System.Windows.Data;

namespace Store.Views.Localization
{
    /// <summary>
    /// Нет ключа (null) — оставить ShortName, переданный ConverterParameter.
    /// </summary>
    public sealed class TranslateFallbackConverter : IValueConverter
    {
        public static readonly TranslateFallbackConverter Instance = new TranslateFallbackConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            return string.IsNullOrEmpty(text) ? parameter : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
