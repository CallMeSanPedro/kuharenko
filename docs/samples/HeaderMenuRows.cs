using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Store.Views
{
    /// <summary>
    /// Полоса вкладок шапки. Хранит ширину одной строки, чтобы шапка могла опустить меню вниз.
    /// </summary>
    public class HeaderTabStrip : Panel
    {
        public double PreferredWidth { get; private set; }

        protected override Size MeasureOverride(Size constraint)
        {
            double heightLimit = double.IsInfinity(constraint.Height) ? 52 : constraint.Height;
            double sum = 0;
            double maxHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, heightLimit));
                if (child.Visibility != Visibility.Visible)
                    continue;

                sum += child.DesiredSize.Width;
                if (child.DesiredSize.Height > maxHeight)
                    maxHeight = child.DesiredSize.Height;
            }

            PreferredWidth = sum;
            if (maxHeight <= 0)
                maxHeight = 52;

            var lines = BuildLines(constraint.Width);
            double blockHeight = 0;
            foreach (var line in lines)
            {
                double lineHeight = 0;
                foreach (var child in line)
                {
                    if (child.DesiredSize.Height > lineHeight)
                        lineHeight = child.DesiredSize.Height;
                }
                if (lineHeight <= 0)
                    lineHeight = maxHeight;
                blockHeight += lineHeight;
            }

            if (blockHeight <= 0)
                blockHeight = maxHeight;

            double width = double.IsInfinity(constraint.Width) ? sum : Math.Min(sum, Math.Max(0, constraint.Width));
            return new Size(width, blockHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var lines = BuildLines(finalSize.Width);
            double y = 0;

            foreach (var line in lines)
            {
                double lineWidth = 0;
                double lineHeight = 0;
                foreach (var child in line)
                {
                    lineWidth += child.DesiredSize.Width;
                    if (child.DesiredSize.Height > lineHeight)
                        lineHeight = child.DesiredSize.Height;
                }

                if (lineHeight <= 0)
                    lineHeight = finalSize.Height;

                double x = Math.Max(0, (finalSize.Width - lineWidth) / 2);
                foreach (var child in line)
                {
                    child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineHeight));
                    x += child.DesiredSize.Width;
                }

                y += lineHeight;
            }

            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility != Visibility.Visible)
                    child.Arrange(new Rect(0, 0, 0, 0));
            }

            return finalSize;
        }

        private List<List<UIElement>> BuildLines(double limit)
        {
            var lines = new List<List<UIElement>>();
            var line = new List<UIElement>();
            double lineWidth = 0;
            bool unlimited = double.IsInfinity(limit) || limit <= 0;

            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility != Visibility.Visible)
                    continue;

                double w = child.DesiredSize.Width;
                if (!unlimited && line.Count > 0 && lineWidth + w > limit + 0.5)
                {
                    lines.Add(line);
                    line = new List<UIElement>();
                    lineWidth = 0;
                }

                line.Add(child);
                lineWidth += w;
            }

            if (line.Count > 0)
                lines.Add(line);

            return lines;
        }
    }

    /// <summary>
    /// Если вкладки не входят между левым и правым блоком, переносит весь ряд меню на вторую строку.
    /// </summary>
    public static class HeaderMenuRows
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HeaderMenuRows),
            new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(HeaderMenuRows),
            new PropertyMetadata(null));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as Grid;
            if (grid == null)
                return;

            var state = grid.GetValue(StateProperty) as State;
            if ((bool)e.NewValue)
            {
                if (state != null)
                    return;

                state = new State(grid);
                grid.SetValue(StateProperty, state);
                grid.Loaded += state.OnLoaded;
                grid.Unloaded += state.OnUnloaded;
                grid.LayoutUpdated += state.OnLayoutUpdated;
                if (grid.IsLoaded)
                    state.OnLoaded(grid, new RoutedEventArgs());
            }
            else if (state != null)
            {
                state.Detach();
                grid.ClearValue(StateProperty);
            }
        }

        private sealed class State
        {
            private readonly Grid _grid;
            private FrameworkElement _menuHost;
            private TabControl _tabs;
            private HeaderTabStrip _strip;
            private FrameworkElement _left;
            private FrameworkElement _right;
            private FrameworkElement _tools;
            private HorizontalAlignment _savedAlignment;
            private bool _savedAlignmentSet;
            private bool _wrap;
            private bool _applied;
            private bool _updating;
            private bool _queued;
            private bool _parentsRelaxed;
            private double _lastPreferred = double.NaN;
            private double _lastAvailable = double.NaN;
            private readonly List<SavedRow> _savedRows = new List<SavedRow>();
            private readonly List<SavedHeight> _savedHeights = new List<SavedHeight>();

            public State(Grid grid)
            {
                _grid = grid;
            }

            public void OnLoaded(object sender, RoutedEventArgs e)
            {
                _grid.LayoutUpdated -= OnLayoutUpdated;
                _grid.LayoutUpdated += OnLayoutUpdated;
                _grid.SizeChanged -= OnSizeChanged;
                _grid.SizeChanged += OnSizeChanged;
                Resolve();
                QueueUpdate();
            }

            public void OnUnloaded(object sender, RoutedEventArgs e)
            {
                _grid.LayoutUpdated -= OnLayoutUpdated;
                _grid.SizeChanged -= OnSizeChanged;
            }

            public void Detach()
            {
                _grid.Loaded -= OnLoaded;
                _grid.Unloaded -= OnUnloaded;
                _grid.LayoutUpdated -= OnLayoutUpdated;
                _grid.SizeChanged -= OnSizeChanged;
                if (_tabs != null)
                    _tabs.ItemContainerGenerator.StatusChanged -= OnGeneratorStatusChanged;
                RestoreParents();
            }

            public void OnLayoutUpdated(object sender, EventArgs e)
            {
                if (_queued || _updating)
                    return;

                if (_tabs == null || VisualTreeHelper.GetParent(_tabs) == null)
                    Resolve();

                if (NeedsUpdate())
                    QueueUpdate();
            }

            private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            {
                QueueUpdate();
            }

            private void OnGeneratorStatusChanged(object sender, EventArgs e)
            {
                if (_tabs != null && _tabs.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    QueueUpdate();
            }

            private void QueueUpdate()
            {
                if (_queued)
                    return;

                _queued = true;
                _grid.Dispatcher.BeginInvoke(new Action(RunUpdate), DispatcherPriority.ContextIdle);
            }

            private void RunUpdate()
            {
                _queued = false;
                if (_tabs == null || VisualTreeHelper.GetParent(_tabs) == null)
                    Resolve();
                Update();
            }

            private bool NeedsUpdate()
            {
                if (_grid.ActualWidth <= 0 || _tabs == null || _menuHost == null || _grid.RowDefinitions.Count < 2)
                    return !_applied;

                double preferred = PreferredWidth();
                double available = Available();
                if (double.IsNaN(_lastPreferred))
                    return true;

                if (Math.Abs(preferred - _lastPreferred) < 0.5 && Math.Abs(available - _lastAvailable) < 0.5)
                    return _wrap && !_parentsRelaxed;

                return true;
            }

            private void Resolve()
            {
                if (_tabs != null)
                    _tabs.ItemContainerGenerator.StatusChanged -= OnGeneratorStatusChanged;

                _menuHost = null;
                _tabs = null;
                _left = null;
                _right = null;
                _tools = null;
                _strip = null;

                foreach (UIElement child in _grid.Children)
                {
                    var element = child as FrameworkElement;
                    if (element == null)
                        continue;

                    var tabs = element as TabControl;
                    if (tabs == null)
                        tabs = FindChild<TabControl>(element);
                    if (tabs == null)
                        continue;

                    _menuHost = element;
                    _tabs = tabs;
                    break;
                }

                if (_tabs != null)
                {
                    _tabs.ItemContainerGenerator.StatusChanged += OnGeneratorStatusChanged;
                    _strip = FindChild<HeaderTabStrip>(_tabs);
                }

                foreach (UIElement child in _grid.Children)
                {
                    if (ReferenceEquals(child, _menuHost) || Grid.GetRow(child) != 0)
                        continue;

                    var element = child as FrameworkElement;
                    int column = Grid.GetColumn(child);
                    if (column == 0)
                        _left = element;
                    else if (column == 2)
                        _right = element;
                    else if (column == 3)
                        _tools = element;
                }
            }

            private void Update()
            {
                if (_updating || _grid.ActualWidth <= 0 || _tabs == null || _menuHost == null)
                    return;

                if (_grid.RowDefinitions.Count < 2)
                    return;

                double preferred = PreferredWidth();
                double available = Available();
                bool wrap = preferred > 0 && preferred > available + 1;

                if (_applied
                    && wrap == _wrap
                    && !double.IsNaN(_lastPreferred)
                    && Math.Abs(preferred - _lastPreferred) < 0.5
                    && Math.Abs(available - _lastAvailable) < 0.5)
                {
                    if (wrap)
                        EnsureParentCanGrow();
                    return;
                }

                _lastPreferred = preferred;
                _lastAvailable = available;
                if (_applied && wrap == _wrap)
                {
                    if (wrap)
                        EnsureParentCanGrow();
                    return;
                }

                _wrap = wrap;
                _applied = true;
                _updating = true;
                try
                {
                    int columns = Math.Max(1, _grid.ColumnDefinitions.Count);
                    if (wrap)
                    {
                        _grid.RowDefinitions[1].Height = GridLength.Auto;
                        Grid.SetRow(_menuHost, 1);
                        Grid.SetColumn(_menuHost, 0);
                        Grid.SetColumnSpan(_menuHost, columns);
                        if (!_savedAlignmentSet)
                        {
                            _savedAlignment = _menuHost.HorizontalAlignment;
                            _savedAlignmentSet = true;
                        }
                        _menuHost.HorizontalAlignment = HorizontalAlignment.Stretch;
                        if (!double.IsNaN(_grid.Height) && _grid.Height <= 80)
                            _grid.Height = double.NaN;
                        _grid.MinHeight = 104;
                        EnsureParentCanGrow();
                    }
                    else
                    {
                        Grid.SetRow(_menuHost, 0);
                        Grid.SetColumn(_menuHost, 1);
                        Grid.SetColumnSpan(_menuHost, 1);
                        _grid.RowDefinitions[1].Height = new GridLength(0);
                        if (_savedAlignmentSet)
                            _menuHost.HorizontalAlignment = _savedAlignment;
                        _grid.MinHeight = 52;
                        RestoreParents();
                    }

                    _grid.InvalidateMeasure();
                }
                finally
                {
                    _updating = false;
                }
            }

            private double PreferredWidth()
            {
                if (_strip != null && _strip.PreferredWidth > 0)
                    return _strip.PreferredWidth;

                if (_tabs == null)
                    return 0;

                double sum = 0;
                int visible = 0;
                for (int i = 0; i < _tabs.Items.Count; i++)
                {
                    var tab = _tabs.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                    if (tab == null)
                        tab = _tabs.Items[i] as TabItem;
                    if (tab == null || tab.Visibility != Visibility.Visible)
                        continue;

                    visible++;
                    sum += tab.DesiredSize.Width;
                }

                return visible == 0 ? 0 : sum;
            }

            private double Available()
            {
                double used = OuterWidth(_left) + OuterWidth(_right) + OuterWidth(_tools);
                return Math.Max(0, _grid.ActualWidth - used);
            }

            private static double OuterWidth(FrameworkElement element)
            {
                if (element == null || element.Visibility == Visibility.Collapsed)
                    return 0;

                return element.ActualWidth + element.Margin.Left + element.Margin.Right;
            }

            private void EnsureParentCanGrow()
            {
                if (_parentsRelaxed || _grid.Parent == null)
                    return;

                DependencyObject current = _grid;
                for (int depth = 0; depth < 6 && current != null; depth++)
                {
                    var element = current as FrameworkElement;
                    if (element != null && !(element is Window))
                    {
                        if (element.Height > 0 && element.Height <= 80)
                        {
                            _savedHeights.Add(new SavedHeight { Element = element, Height = element.Height, MaxHeight = element.MaxHeight });
                            element.Height = double.NaN;
                            if (element.MaxHeight > 0 && element.MaxHeight <= 80)
                                element.MaxHeight = double.PositiveInfinity;
                        }

                        var parentGrid = element.Parent as Grid;
                        if (parentGrid != null && parentGrid.RowDefinitions.Count > 0)
                        {
                            int row = Grid.GetRow(element);
                            if (row >= 0 && row < parentGrid.RowDefinitions.Count)
                            {
                                var definition = parentGrid.RowDefinitions[row];
                                if (definition.Height.IsAbsolute && definition.Height.Value > 0 && definition.Height.Value <= 80)
                                {
                                    _savedRows.Add(new SavedRow { Row = definition, Height = definition.Height });
                                    definition.Height = GridLength.Auto;
                                }
                            }
                        }
                    }

                    current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
                }

                _parentsRelaxed = true;
            }

            private void RestoreParents()
            {
                for (int i = 0; i < _savedRows.Count; i++)
                    _savedRows[i].Row.Height = _savedRows[i].Height;
                _savedRows.Clear();

                for (int i = 0; i < _savedHeights.Count; i++)
                {
                    _savedHeights[i].Element.Height = _savedHeights[i].Height;
                    _savedHeights[i].Element.MaxHeight = _savedHeights[i].MaxHeight;
                }
                _savedHeights.Clear();
                _parentsRelaxed = false;
            }

            private static T FindChild<T>(DependencyObject root) where T : DependencyObject
            {
                if (root == null)
                    return null;

                var found = root as T;
                if (found != null)
                    return found;

                int count = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < count; i++)
                {
                    var child = FindChild<T>(VisualTreeHelper.GetChild(root, i));
                    if (child != null)
                        return child;
                }

                return null;
            }

            private struct SavedRow
            {
                public RowDefinition Row;
                public GridLength Height;
            }

            private struct SavedHeight
            {
                public FrameworkElement Element;
                public double Height;
                public double MaxHeight;
            }
        }
    }
}
