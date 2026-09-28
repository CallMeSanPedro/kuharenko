using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

// Файл для сборки Store.ViewModels, рядом с TechReservViewModel.
// Если TechReserv здесь не виден, добавьте тот же using, что в TechReservViewModel.cs.

namespace Store.ViewModels.Device.TechnoReserv
{
    public sealed class ModelShare
    {
        public string Label { get; set; }

        public double Value { get; set; }

        public double Percent { get; set; }
    }

    /// <summary>
    /// Доли моделей для круговой диаграммы на TechReservView.
    /// Считает только строки текущего ItemsView, то есть после фильтра колонок.
    /// </summary>
    public static class ModelShareBuilder
    {
        /// <summary>
        /// Имя свойства TechReserv, из которого колонка «На складе» показывает текст вида «2 (1/1)».
        /// В сектор идёт число до скобки. Пока имя пустое, диаграмма пишет «Нет данных».
        /// </summary>
        public const string OnStorePropertyName = "";

        const int SliceLimit = 8;

        public static void Fill(ObservableCollection<ModelShare> target, IEnumerable source)
        {
            var groups = (source == null ? Enumerable.Empty<TechReserv>() : source.OfType<TechReserv>())
                .Select(item => new { Name = item.MarkacommName, Qty = ReadOnStore(item) })
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Name) ? "Без модели" : x.Name)
                .Select(g => new ModelShare { Label = g.Key, Value = g.Sum(x => x.Qty) })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            if (groups.Count > SliceLimit)
            {
                var rest = groups.Skip(SliceLimit - 1).Sum(x => x.Value);
                groups = groups.Take(SliceLimit - 1).ToList();
                groups.Add(new ModelShare { Label = "Прочие", Value = rest });
            }

            var total = groups.Sum(x => x.Value);
            foreach (var share in groups)
                share.Percent = total <= 0 ? 0 : share.Value / total;

            target.Clear();
            foreach (var share in groups)
                target.Add(share);
        }

        static double ReadOnStore(TechReserv item)
        {
            if (string.IsNullOrWhiteSpace(OnStorePropertyName))
                return 0;

            var property = typeof(TechReserv).GetProperty(
                OnStorePropertyName,
                BindingFlags.Instance | BindingFlags.Public);
            return LeadingNumber(property?.GetValue(item));
        }

        static double LeadingNumber(object value)
        {
            if (value == null)
                return 0;

            if (value is IConvertible convertible && !(value is string))
            {
                try
                {
                    return convertible.ToDouble(CultureInfo.CurrentCulture);
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
            }

            var text = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            text = text.Trim();
            var length = 0;
            while (length < text.Length && (char.IsDigit(text[length]) || text[length] == ',' || text[length] == '.'))
                length++;

            if (length == 0)
                return 0;

            var token = text.Substring(0, length).Replace(',', '.');
            return double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;
        }
    }
}
