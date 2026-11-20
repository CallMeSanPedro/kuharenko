using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using DatumNode.Models;

// Файл собирается проектом модели представления.
// View на неё ссылается и записывает перевод в SetTitle. Модель на View не ссылается.
namespace Store.ViewModels.Filtering
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
        Text,
        ServerText
    }

    public sealed class ColumnHeaderFilter : INotifyPropertyChanged
    {
        private readonly Func<object, string> _read;
        private readonly Action<string> _write;
        private readonly string _sourceTitle;
        private string _title;
        private string _captionKey;
        private string _fallback;
        private IEnumerable<object> _selectedItems;
        private HashSet<string> _selected;
        private string _text = string.Empty;
        private string _applied = string.Empty;
        private bool _updating;

        public ColumnHeaderFilter(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode)
            : this(propertyName, title, read, mode, null)
        {
        }

        public ColumnHeaderFilter(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode, Action<string> write)
            : this(propertyName, title, read, mode, write, null)
        {
        }

        public ColumnHeaderFilter(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode, Action<string> write, string exclusiveGroup)
        {
            PropertyName = propertyName;
            _sourceTitle = title;
            _title = title;
            _captionKey = title;
            _fallback = title;
            Mode = mode;
            _read = read;
            _write = write;
            ExclusiveGroup = exclusiveGroup;
            Options = new ObservableCollection<object>();
            ClearCommand = new ClearOneCommand(this);
            ApplyCommand = new ApplyServerFilterCommand(this);
        }

        public ICommand ClearCommand { get; }

        public ICommand ApplyCommand { get; }

        public string PropertyName { get; }

        public string SourceTitle => _sourceTitle;

        public string CaptionKey => _captionKey;

        public string CaptionFallback => string.IsNullOrEmpty(_fallback) ? _sourceTitle : _fallback;

        public string Title => _title;

        public string ExclusiveGroup { get; }

        public ColumnFilterMode Mode { get; }

        public bool IsCheckList => Mode == ColumnFilterMode.CheckList;

        public bool IsText => Mode == ColumnFilterMode.Text;

        public bool IsServerText => Mode == ColumnFilterMode.ServerText;

        public bool ShowsText => IsText || IsServerText;

        public void UseCaption(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key))
                _captionKey = key;
            if (!string.IsNullOrEmpty(fallback))
                _fallback = fallback;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptionKey)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptionFallback)));
        }

        public void SetTitle(string text)
        {
            var next = string.IsNullOrEmpty(text) ? _sourceTitle : text;
            if (_title == next)
                return;
            _title = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }

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
                if (Mode == ColumnFilterMode.ServerText)
                    return;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
                if (!_updating)
                    Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;

        public bool IsActive
        {
            get
            {
                if (Mode == ColumnFilterMode.ServerText)
                    return !string.IsNullOrWhiteSpace(_applied);
                if (Mode == ColumnFilterMode.Text)
                    return !string.IsNullOrWhiteSpace(_text);
                return _selected != null && _selected.Count > 0;
            }
        }

        public bool Passes(object item)
        {
            if (Mode == ColumnFilterMode.ServerText)
                return true;

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

        public string Summary => BuildSummary(2);

        public string FullSummary => BuildSummary(int.MaxValue);

        public void Apply()
        {
            if (Mode != ColumnFilterMode.ServerText)
                return;

            var next = _text.Trim();
            if (string.Equals(_applied, next, StringComparison.Ordinal))
                return;

            _applied = next;
            _write?.Invoke(_applied.Length == 0 ? null : _applied);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ShowApplied(string value)
        {
            var next = (value ?? string.Empty).Trim();
            _text = next;
            _applied = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
        }

        public void Reload(IEnumerable source)
        {
            if (Mode == ColumnFilterMode.Text || Mode == ColumnFilterMode.ServerText)
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

        public bool Clear()
        {
            var hasText = _text.Length != 0 || _applied.Length != 0;
            var hasSelected = _selected != null && _selected.Count > 0;
            if (!hasText && !hasSelected)
                return false;

            _updating = true;
            try
            {
                if (hasText)
                {
                    _text = string.Empty;
                    _applied = string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                    if (Mode == ColumnFilterMode.ServerText)
                        _write?.Invoke(null);
                }

                if (hasSelected)
                {
                    _selectedItems = null;
                    _selected = null;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItems)));
                }
            }
            finally
            {
                _updating = false;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
            return true;
        }

        public void ClearSingle()
        {
            if (Clear())
                Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void ClearWritten()
        {
            var hasText = _text.Length != 0 || _applied.Length != 0;
            var hasSelected = _selected != null && _selected.Count > 0;
            if (!hasText && !hasSelected)
                return;

            _text = string.Empty;
            _applied = string.Empty;
            _selectedItems = null;
            _selected = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItems)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSummary)));
            if (Mode == ColumnFilterMode.ServerText)
                _write?.Invoke(null);
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

        private string BuildSummary(int limit)
        {
            if (Mode == ColumnFilterMode.Text)
                return _text.Trim();
            if (Mode == ColumnFilterMode.ServerText)
                return _applied.Trim();
            if (_selected == null || _selected.Count == 0)
                return string.Empty;

            var names = new List<string>(_selected);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (names.Count <= limit)
                return string.Join(", ", names);

            return names[0] + ", " + names[1] + " +" + (names.Count - 2);
        }

        private sealed class ClearOneCommand : ICommand
        {
            private readonly ColumnHeaderFilter _filter;

            public ClearOneCommand(ColumnHeaderFilter filter)
            {
                _filter = filter;
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => _filter.ClearSingle();
        }

        private sealed class ApplyServerFilterCommand : ICommand
        {
            private readonly ColumnHeaderFilter _filter;

            public ApplyServerFilterCommand(ColumnHeaderFilter filter)
            {
                _filter = filter;
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => _filter.Apply();
        }
    }

    public sealed class ColumnHeaderFilterSet : IColumnHeaderFilterHost, INotifyPropertyChanged
    {
        private readonly Dictionary<string, ColumnHeaderFilter> _filters = new Dictionary<string, ColumnHeaderFilter>(StringComparer.Ordinal);
        private readonly List<ColumnHeaderFilter> _order = new List<ColumnHeaderFilter>();
        private readonly ClearHeaderFiltersCommand _clearCommand;

        public ColumnHeaderFilterSet()
        {
            _clearCommand = new ClearHeaderFiltersCommand(this);
            Active = new ObservableCollection<ColumnHeaderFilter>();
        }

        public ObservableCollection<ColumnHeaderFilter> Active { get; }

        public bool HasMultiple => Active.Count > 1;

        public event EventHandler Changed;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsActive
        {
            get
            {
                foreach (var filter in _filters.Values)
                {
                    if (filter.IsActive)
                        return true;
                }

                return false;
            }
        }

        public ICommand ClearCommand => _clearCommand;

        public void Clear()
        {
            var any = false;
            foreach (var filter in _filters.Values)
            {
                if (filter.Clear())
                    any = true;
            }

            if (!any)
                return;

            Publish();
        }

        public ColumnHeaderFilter Add(string propertyName, string title, Func<object, string> read)
        {
            return Add(propertyName, title, read, ColumnFilterMode.CheckList);
        }

        public ColumnHeaderFilter AddText(string propertyName, string title, Func<object, string> read)
        {
            return Add(propertyName, title, read, ColumnFilterMode.Text);
        }

        public ColumnHeaderFilter AddServerText(string propertyName, string title, Func<object, string> read, Action<string> write, string exclusiveGroup = null)
        {
            return Add(propertyName, title, read, ColumnFilterMode.ServerText, write, exclusiveGroup);
        }

        private ColumnHeaderFilter Add(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode)
        {
            return Add(propertyName, title, read, mode, null, null);
        }

        private ColumnHeaderFilter Add(string propertyName, string title, Func<object, string> read, ColumnFilterMode mode, Action<string> write, string exclusiveGroup = null)
        {
            var filter = new ColumnHeaderFilter(propertyName, title, read, mode, write, exclusiveGroup);
            filter.Changed += (_, _) =>
            {
                ReleaseGroup(filter);
                Publish();
            };
            _filters[propertyName] = filter;
            _order.Add(filter);
            return filter;
        }

        private void ReleaseGroup(ColumnHeaderFilter owner)
        {
            if (!owner.IsActive || string.IsNullOrEmpty(owner.ExclusiveGroup))
                return;

            foreach (var other in _order)
            {
                if (other == owner || !string.Equals(other.ExclusiveGroup, owner.ExclusiveGroup, StringComparison.Ordinal))
                    continue;
                other.ClearWritten();
            }
        }

        private void Publish()
        {
            SyncActive();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMultiple)));
            Changed?.Invoke(this, EventArgs.Empty);
            _clearCommand.RaiseCanExecuteChanged();
        }

        private void SyncActive()
        {
            Active.Clear();
            foreach (var filter in _order)
            {
                if (filter.IsActive)
                    Active.Add(filter);
            }
        }

        public void ShowApplied(string propertyName, string value)
        {
            var filter = Find(propertyName);
            if (filter == null)
                return;

            filter.ShowApplied(value);
            SyncActive();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMultiple)));
            _clearCommand.RaiseCanExecuteChanged();
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
                if (string.Equals(filter.SourceTitle, title, StringComparison.CurrentCulture)
                    || string.Equals(filter.Title, title, StringComparison.CurrentCulture)
                    || string.Equals(filter.CaptionFallback, title, StringComparison.CurrentCulture))
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

        private sealed class ClearHeaderFiltersCommand : ICommand
        {
            private readonly ColumnHeaderFilterSet _set;

            public ClearHeaderFiltersCommand(ColumnHeaderFilterSet set)
            {
                _set = set;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter) => _set.IsActive;

            public void Execute(object parameter) => _set.Clear();

            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
