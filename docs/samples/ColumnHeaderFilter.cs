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

    public enum ColumnFilterMode
    {
        CheckList,
        Text
    }

    /// <summary>
    /// Данные одного фильтра в шапке колонки.
    /// Пустой выбор и пустая строка поиска не скрывают строки.
    /// </summary>
    public sealed class ColumnHeaderFilter : INotifyPropertyChanged
    {
        private readonly Func<object, string> _read;
        private IEnumerable<object> _selectedItems;
        private HashSet<string> _selected;
        private string _text = string.Empty;
        private bool _updating;

        public ColumnHeaderFilter(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode)
        {
            PropertyName = propertyName;
            Title = title;
            Mode = mode;
            _read = read;
            Options = new ObservableCollection<object>();
        }

        public string PropertyName { get; }

        public string Title { get; }

        public ColumnFilterMode Mode { get; }

        public bool IsCheckList => Mode == ColumnFilterMode.CheckList;

        public bool IsText => Mode == ColumnFilterMode.Text;

        public string Text
        {
            get => _text;
            set
            {
                var next = value ?? string.Empty;
                if (_text == next)
                    return;
                _text = next;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

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
            var value = _read(item) ?? string.Empty;
            if (Mode == ColumnFilterMode.Text)
            {
                if (string.IsNullOrWhiteSpace(_text))
                    return true;
                return value.IndexOf(_text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (_selected == null || _selected.Count == 0)
                return true;
            return _selected.Contains(value);
        }

        public void Reload(IEnumerable source)
        {
            if (Mode == ColumnFilterMode.Text)
                return;

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
            return Add(propertyName, title, read, ColumnFilterMode.CheckList);
        }

        public ColumnHeaderFilter AddText(string propertyName, string title, Func<object, string> read)
        {
            return Add(propertyName, title, read, ColumnFilterMode.Text);
        }

        private ColumnHeaderFilter Add(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode)
        {
            var filter = new ColumnHeaderFilter(propertyName, title, read, mode);
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
