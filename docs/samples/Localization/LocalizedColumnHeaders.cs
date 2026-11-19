using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Store.Views.Localization
{
    /// <summary>
    /// Заголовки колонок из [Display(ShortName = "...")].
    /// Ключ ресурса: ИмяТипа_ИмяСвойства. ShortName остаётся, если перевода нет.
    /// Смена языка обновляет шапку: Header — привязка к Translation.Instance.
    /// Фильтр колонки подменяет Header своим ContentControl; его Title переводится отдельно.
    /// </summary>
    public static class LocalizedColumnHeaders
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(LocalizedColumnHeaders),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null)
                return;

            if ((bool)e.NewValue)
            {
                grid.AutoGeneratingColumn += OnAutoGeneratingColumn;
                grid.Loaded += OnLoaded;
                DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))
                    .AddValueChanged(grid, OnItemsSourceChanged);
                ApplyExisting(grid);
            }
            else
            {
                grid.AutoGeneratingColumn -= OnAutoGeneratingColumn;
                grid.Loaded -= OnLoaded;
                DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))
                    .RemoveValueChanged(grid, OnItemsSourceChanged);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyExisting(sender as DataGrid);
        }

        private static void OnItemsSourceChanged(object sender, EventArgs e)
        {
            ApplyExisting(sender as DataGrid);
        }

        private static void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var grid = sender as DataGrid;
            var descriptor = e.PropertyDescriptor as PropertyDescriptor;
            var itemType = descriptor?.ComponentType ?? ItemType(grid);
            Apply(e.Column, itemType, e.PropertyName);
        }

        private static void ApplyExisting(DataGrid grid)
        {
            if (grid == null)
                return;

            var itemType = ItemType(grid);
            foreach (var column in grid.Columns)
            {
                if (string.IsNullOrEmpty(column.SortMemberPath))
                    continue;
                Apply(column, itemType, column.SortMemberPath);
            }
        }

        public static void Apply(DataGridColumn column, Type itemType, string propertyName)
        {
            if (column == null || string.IsNullOrEmpty(propertyName))
                return;
            if (column.Header is ContentControl)
                return;
            if (BindingOperations.GetBinding(column, DataGridColumn.HeaderProperty) != null)
                return;

            var property = itemType?.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            var typeName = property?.DeclaringType?.Name ?? itemType?.Name;
            if (string.IsNullOrEmpty(typeName))
                return;

            var fallback = ReadShortName(property) ?? propertyName;
            var binding = new Binding("[" + typeName + "_" + propertyName + "]")
            {
                Source = Translation.Instance,
                Mode = BindingMode.OneWay,
                Converter = TranslateFallbackConverter.Instance,
                ConverterParameter = fallback
            };
            BindingOperations.SetBinding(column, DataGridColumn.HeaderProperty, binding);
        }

        private static string ReadShortName(PropertyInfo property)
        {
            if (property == null)
                return null;

            foreach (var attribute in property.GetCustomAttributes(true))
            {
                var type = attribute.GetType();
                if (type.Name == "DisplayAttribute")
                {
                    var shortName = type.GetMethod("GetShortName", Type.EmptyTypes)?.Invoke(attribute, null) as string;
                    if (!string.IsNullOrEmpty(shortName))
                        return shortName;

                    var name = type.GetMethod("GetName", Type.EmptyTypes)?.Invoke(attribute, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                if (type.Name == "ShortNameAttribute")
                {
                    var name = type.GetProperty("Name")?.GetValue(attribute, null) as string
                        ?? type.GetProperty("ShortName")?.GetValue(attribute, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }

            return null;
        }

        private static Type ItemType(DataGrid grid)
        {
            var source = grid?.ItemsSource;
            if (source == null)
                return null;

            var type = source.GetType();
            var types = new List<Type> { type };
            types.AddRange(type.GetInterfaces());
            foreach (var candidate in types)
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return candidate.GetGenericArguments()[0];
            }

            foreach (var item in grid.Items)
            {
                if (item == null)
                    continue;
                if (item.GetType().FullName == "MS.Internal.NamedObject")
                    continue;
                return item.GetType();
            }

            return null;
        }
    }
}
