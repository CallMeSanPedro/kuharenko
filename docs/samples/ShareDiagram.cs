using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Store.ViewModels.Charts
{
    /// <summary>
    /// Имя и количество одного сектора.
    /// Сюда попадают либо готовые свойства, либо значения, снятые с сущности.
    /// </summary>
    public sealed class DiagramShare
    {
        public DiagramShare()
        {
        }

        public DiagramShare(string name, object quantity)
        {
            Name = name;
            Quantity = ShareDiagram.ToNumber(quantity);
        }

        public string Name { get; set; }

        public double Quantity { get; set; }

        public double Percent { get; set; }

        public bool IsOther { get; set; }
    }

    /// <summary>
    /// Собирает круговую диаграмму из любых сущностей.
    /// Сектор — сумма количества по одинаковому имени.
    /// </summary>
    public sealed class ShareDiagram
    {
        public const int DefaultSliceLimit = 8;

        int _sliceLimit = DefaultSliceLimit;

        public ShareDiagram()
        {
            Shares = new ObservableCollection<DiagramShare>();
            EmptyName = "Без имени";
            OtherName = "Прочие";
        }

        public ObservableCollection<DiagramShare> Shares { get; private set; }

        public string EmptyName { get; set; }

        public string OtherName { get; set; }

        public int SliceLimit
        {
            get { return _sliceLimit; }
            set { _sliceLimit = value < 1 ? 1 : value; }
        }

        /// <summary>
        /// Сущности, у которых уже есть свойства Name и Quantity.
        /// </summary>
        public void Load(IEnumerable entities)
        {
            var shares = entities as IEnumerable<DiagramShare>;
            if (shares != null)
            {
                Load(shares, item => item.Name, item => (object)item.Quantity);
                return;
            }

            Load(entities, "Name", "Quantity");
        }

        /// <summary>
        /// Любые сущности. Имя и количество читаются из указанных свойств.
        /// Количество может быть числом или строкой вида «2 (1/1)»: берётся число до скобки.
        /// </summary>
        public void Load(IEnumerable entities, string nameProperty, string quantityProperty)
        {
            var names = new Dictionary<Type, PropertyInfo>();
            var quantities = new Dictionary<Type, PropertyInfo>();
            Load(
                entities,
                entity => Convert.ToString(ReadProperty(entity, nameProperty, names), CultureInfo.CurrentCulture),
                entity => ReadProperty(entity, quantityProperty, quantities));
        }

        /// <summary>
        /// Любые сущности. Имя и количество — выбранные свойства.
        /// </summary>
        public void Load<T>(IEnumerable<T> entities, Func<T, string> name, Func<T, object> quantity)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (quantity == null)
                throw new ArgumentNullException(nameof(quantity));

            Load(
                (IEnumerable)entities,
                entity => name((T)entity),
                entity => quantity((T)entity));
        }

        public void Load(IEnumerable entities, Func<object, string> name, Func<object, object> quantity)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (quantity == null)
                throw new ArgumentNullException(nameof(quantity));

            var rows = new List<KeyValuePair<string, double>>();
            if (entities != null)
            {
                foreach (var entity in entities)
                {
                    if (entity == null)
                        continue;
                    rows.Add(new KeyValuePair<string, double>(name(entity), ToNumber(quantity(entity))));
                }
            }

            Publish(rows);
        }

        void Publish(List<KeyValuePair<string, double>> rows)
        {
            var emptyName = string.IsNullOrWhiteSpace(EmptyName) ? "Без имени" : EmptyName;
            var otherName = string.IsNullOrWhiteSpace(OtherName) ? "Прочие" : OtherName;
            var groups = rows
                .GroupBy(row => string.IsNullOrWhiteSpace(row.Key) ? emptyName : row.Key)
                .Select(group => new DiagramShare
                {
                    Name = group.Key,
                    Quantity = group.Sum(row => row.Value)
                })
                .Where(share => share.Quantity > 0)
                .OrderByDescending(share => share.Quantity)
                .ToList();

            if (groups.Count > SliceLimit)
            {
                var rest = groups.Skip(SliceLimit - 1).Sum(share => share.Quantity);
                groups = groups.Take(SliceLimit - 1).ToList();
                groups.Add(new DiagramShare
                {
                    Name = otherName,
                    Quantity = rest,
                    IsOther = true
                });
            }

            var total = groups.Sum(share => share.Quantity);
            foreach (var share in groups)
                share.Percent = total <= 0 ? 0 : share.Quantity / total;

            Shares.Clear();
            foreach (var share in groups)
                Shares.Add(share);
        }

        static object ReadProperty(object entity, string propertyName, Dictionary<Type, PropertyInfo> cache)
        {
            if (entity == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var type = entity.GetType();
            PropertyInfo property;
            if (!cache.TryGetValue(type, out property))
            {
                property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                cache[type] = property;
            }

            return property == null ? null : property.GetValue(entity, null);
        }

        public static double ToNumber(object value)
        {
            if (value == null)
                return 0;

            if (IsNumber(value))
            {
                try
                {
                    return Convert.ToDouble(value, CultureInfo.CurrentCulture);
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

        static bool IsNumber(object value)
        {
            return value is byte
                || value is sbyte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal;
        }
    }
}
