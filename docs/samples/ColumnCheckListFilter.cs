using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Store.Views.Filtering
{
    public interface IColumnCheckFilterHost
    {
        ColumnCheckFilterSet ColumnFilters { get; }
    }

    public sealed class ColumnFilterOption : INotifyPropertyChanged
    {
        private bool _isChecked;

        public ColumnFilterOption(string key, bool isChecked)
        {
            Key = key ?? string.Empty;
            _isChecked = isChecked;
        }

        public string Key { get; }

        public string Text => Key.Length == 0 ? "Пусто" : Key;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler CheckedChanged;
    }

    /// <summary>
    /// Список флажков одной колонки. Внутри колонки значения объединяются через «или».
    /// Пока отмечены все пункты, колонка строки не отсекает.
    /// </summary>
    public sealed class ColumnCheckFilter : INotifyPropertyChanged
    {
        private readonly Func<object, string> _read;
        private readonly HashSet<string> _checked = new HashSet<string>(StringComparer.CurrentCulture);
        private string _searchText = string.Empty;
        private bool _reload;
        private bool? _allChecked = true;

        public ColumnCheckFilter(string propertyName, string title, Func<object, string> read)
        {
            PropertyName = propertyName;
            Title = string.IsNullOrEmpty(title) ? propertyName : title;
            _read = read;
            Options = new ObservableCollection<ColumnFilterOption>();
            VisibleOptions = CollectionViewSource.GetDefaultView(Options);
            VisibleOptions.Filter = MatchesSearch;
        }

        public string PropertyName { get; }

        public string Title { get; }

        public ObservableCollection<ColumnFilterOption> Options { get; }

        public ICollectionView VisibleOptions { get; }

        public bool IsActive => Options.Count > 0 && _checked.Count < Options.Count;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;
                _searchText = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
                VisibleOptions.Refresh();
            }
        }

        public bool? AllChecked
        {
            get => _allChecked;
            set
            {
                var check = value == true;
                foreach (var option in Options)
                    option.IsChecked = check;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;

        public bool Passes(object item)
        {
            if (!IsActive)
                return true;
            return _checked.Contains(_read(item) ?? string.Empty);
        }

        public void Reload(IEnumerable source)
        {
            var keys = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (source != null)
            {
                foreach (var item in source)
                {
                    if (item != null)
                        keys.Add(_read(item) ?? string.Empty);
                }
            }

            if (keys.Count == 0)
                return;

            var previous = new HashSet<string>(_checked, StringComparer.CurrentCulture);
            var first = Options.Count == 0;
            _reload = true;
            Options.Clear();
            _checked.Clear();
            foreach (var key in keys)
            {
                var check = first || previous.Contains(key);
                var option = new ColumnFilterOption(key, check);
                option.CheckedChanged += OnOptionChecked;
                Options.Add(option);
                if (check)
                    _checked.Add(key);
            }
            _reload = false;
            Publish();
        }

        public FrameworkElement CreateHeader()
        {
            var root = new Grid { DataContext = this };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = Title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(title, 0);
            root.Children.Add(title);

            var button = new ToggleButton
            {
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Content = CreateIcon(button);
            button.SetBinding(Control.ForegroundProperty, new Binding(nameof(IsActive))
            {
                Converter = ActiveBrushConverter.Instance
            });
            Grid.SetColumn(button, 1);
            root.Children.Add(button);

            var popup = new Popup
            {
                PlacementTarget = button,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(ToggleButton.IsCheckedProperty.Name)
            {
                Source = button,
                Mode = BindingMode.TwoWay
            });
            popup.Child = CreatePopup();
            root.Children.Add(popup);
            return root;
        }

        private UIElement CreatePopup()
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xDE, 0xE9)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Width = 240,
                MaxHeight = 320
            };

            var panel = new DockPanel();
            var search = new TextBox { Height = 32, Margin = new Thickness(0, 0, 0, 8) };
            search.SetBinding(TextBox.TextProperty, new Binding(nameof(SearchText))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            DockPanel.SetDock(search, Dock.Top);
            panel.Children.Add(search);

            var reset = new Button
            {
                Content = "Сбросить",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            reset.Click += (_, _) => AllChecked = true;
            DockPanel.SetDock(reset, Dock.Bottom);
            panel.Children.Add(reset);

            var all = new CheckBox { Content = "Выбрать все", Margin = new Thickness(0, 0, 0, 6) };
            all.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(AllChecked)) { Mode = BindingMode.TwoWay });
            DockPanel.SetDock(all, Dock.Top);
            panel.Children.Add(all);

            var list = new ItemsControl();
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(VisibleOptions)));
            var itemTemplate = new DataTemplate();
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 2));
            check.SetBinding(ContentControl.ContentProperty, new Binding(nameof(ColumnFilterOption.Text)));
            check.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ColumnFilterOption.IsChecked))
            {
                Mode = BindingMode.TwoWay
            });
            itemTemplate.VisualTree = check;
            list.ItemTemplate = itemTemplate;

            panel.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = list
            });
            card.Child = panel;
            return card;
        }

        private static UIElement CreateIcon(ToggleButton button)
        {
            var icon = new Path
            {
                Data = Geometry.Parse("M 0,0 L 10,0 L 6.2,4.6 L 6.2,9 L 3.8,9 L 3.8,4.6 Z"),
                Width = 10,
                Height = 9,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetBinding(Shape.FillProperty, new Binding(Control.ForegroundProperty.Name) { Source = button });
            return icon;
        }

        private bool MatchesSearch(object item)
        {
            var option = (ColumnFilterOption)item;
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;
            return option.Text.IndexOf(_searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void OnOptionChecked(object sender, EventArgs e)
        {
            if (_reload)
                return;
            var option = (ColumnFilterOption)sender;
            if (option.IsChecked)
                _checked.Add(option.Key);
            else
                _checked.Remove(option.Key);
            Publish();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Publish()
        {
            _allChecked = Options.Count == 0 || _checked.Count == Options.Count
                ? true
                : _checked.Count == 0 ? false : (bool?)null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllChecked)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }

        private sealed class ActiveBrushConverter : IValueConverter
        {
            public static readonly ActiveBrushConverter Instance = new ActiveBrushConverter();

            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var active = value is bool flag && flag;
                return active
                    ? new SolidColorBrush(Color.FromRgb(0x1B, 0x3F, 0x6F))
                    : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }
    }

    public sealed class ColumnCheckFilterSet
    {
        private readonly Dictionary<string, ColumnCheckFilter> _filters = new Dictionary<string, ColumnCheckFilter>(StringComparer.Ordinal);
        private ICollectionView _view;

        public event EventHandler Changed;

        public ColumnCheckFilter Get(string propertyName, string title)
        {
            ColumnCheckFilter filter;
            if (!_filters.TryGetValue(propertyName, out filter))
            {
                filter = new ColumnCheckFilter(propertyName, title, item => Read(item, propertyName));
                filter.Changed += (_, _) =>
                {
                    _view?.Refresh();
                    Changed?.Invoke(this, EventArgs.Empty);
                };
                _filters[propertyName] = filter;
                if (_view != null)
                    filter.Reload(_view.SourceCollection);
            }
            return filter;
        }

        public void Bind(ICollectionView view)
        {
            _view = view;
            var source = view?.SourceCollection;
            foreach (var filter in _filters.Values)
                filter.Reload(source);
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

        private static string Read(object item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return string.Empty;
            var property = item.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            var value = property?.GetValue(item, null);
            return value?.ToString() ?? string.Empty;
        }
    }
}
