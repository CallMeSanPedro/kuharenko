using System;
using System.Globalization;
using System.Windows.Data;

namespace Store.Views.Theming
{
    /// <summary>
    /// Код статуса -> тон плашки: Success, Neutral, Warning, Info.
    /// В Tag уходит тон. Подпись колонки биндится отдельно и в конвертер не попадает.
    /// </summary>
    public sealed class StatusToneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Neutral";

            switch (value.ToString())
            {
                case "InWork":
                case "NewCondition":
                    return "Success";
                case "InWarehouse":
                    return "Neutral";
                case "InRepair":
                    return "Warning";
                case "NewDocument":
                    return "Info";
                default:
                    return "Neutral";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
