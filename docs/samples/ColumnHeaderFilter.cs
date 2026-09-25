using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DatumNode.Models;

namespace Store.Views.Filtering
{
    public interface IColumnHeaderFilterHost
    {
        ColumnHeaderFilter Find(string propertyName);
    }

    public sealed class ColumnFilterValue : Entity
    {
        public ColumnFilterValue()
        {
            TrackChanges = false;
        }

        public string Name { get; set; }

        public override string ToString() => Name ?? string.Empty;
    }

    /// <summary>
    /// Данные одного фильтра в шапке колонки.
    /// Пустой выбор не скрывает строки, как GisNames.
    /// </summary>
    public sealed class ColumnHeaderFilter : INotifyPropertyChanged
    {
        private readonly Func<object, string> _read;
        private IEnumerable<object> _selectedItems;
        private HashSet<string> _selected;
        private bool _updating;

        public ColumnHeaderFilter(string propertyName, string title, Func<object, string> read)
        {
            PropertyName = propertyName;
            Title = title;
            _read = read;
            Options = new ObservableCollection<object>();
        }

        public string PropertyName { get; }

        public string Title { get; }

        public ObservableCollection<object> Options { get; }

        public IEnumerable<object> SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value;
                RebuildSelected();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItems)));
                if (!_updating)
                    Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;

        public bool Passes(object item)
        {
            if (_selected == null || _selected.Count == 0)
                return true;
            return _selected.Contains(_read(item) ?? string.Empty);
        }

        public void Reload(IEnumerable source)
        {
            var selected = new HashSet<string>(
                (_selectedItems ?? Enumerable.Empty<object>())
                    .Select(x => x?.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);

            var names = new List<string>();
            if (source != null)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in source)
                {
                    var name = _read(item);
                    if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                        continue;
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            Options.Clear();
            var options = new List<ColumnFilterValue>();
            foreach (var name in names)
            {
                var option = new ColumnFilterValue { Name = name };
                options.Add(option);
                Options.Add(option);
            }

            _updating = true;
            try
            {
                SelectedItems = selected.Count == 0
                    ? null
                    : options.Where(x => selected.Contains(x.Name)).Cast<object>().ToList();
            }
            finally
            {
                _updating = false;
            }
        }

        private void RebuildSelected()
        {
            if (_selectedItems == null || !_selectedItems.Any())
            {
                _selected = null;
                return;
            }

            _selected = new HashSet<string>(
                _selectedItems.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class ColumnHeaderFilterSet : IColumnHeaderFilterHost
    {
        private readonly Dictionary<string, ColumnHeaderFilter> _filters = new Dictionary<string, ColumnHeaderFilter>(StringComparer.Ordinal);

        public event EventHandler Changed;

        public ColumnHeaderFilter Add(string propertyName, string title, Func<object, string> read)
        {
            var filter = new ColumnHeaderFilter(propertyName, title, read);
            filter.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            _filters[propertyName] = filter;
            return filter;
        }

        public ColumnHeaderFilter Find(string propertyName)
        {
            ColumnHeaderFilter filter;
            return propertyName != null && _filters.TryGetValue(propertyName, out filter) ? filter : null;
        }

        public ColumnHeaderFilter FindByTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            foreach (var filter in _filters.Values)
            {
                if (string.Equals(filter.Title, title, StringComparison.CurrentCulture))
                    return filter;
            }

            return null;
        }

        public bool Passes(object item)
        {
            foreach (var filter in _filters.Values)
            {
                if (!filter.Passes(item))
                    return false;
            }
            return true;
        }

        public void Reload(IEnumerable source)
        {
            foreach (var filter in _filters.Values)
                filter.Reload(source);
        }
    }
}
