using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            private TabControl _tabs;
            private HeaderTabStrip _strip;
            private FrameworkElement _left;
            private FrameworkElement _right;
            private FrameworkElement _tools;
            private bool _wrap;
            private bool _applied;
            private bool _updating;
            private double _lastPreferred = double.NaN;
            private double _lastAvailable = double.NaN;

            public State(Grid grid)
            {
                _grid = grid;
            }

            public void OnLoaded(object sender, RoutedEventArgs e)
            {
                _grid.LayoutUpdated -= OnLayoutUpdated;
                _grid.LayoutUpdated += OnLayoutUpdated;
                Resolve();
                Update();
            }

            public void OnUnloaded(object sender, RoutedEventArgs e)
            {
                _grid.LayoutUpdated -= OnLayoutUpdated;
            }

            public void Detach()
            {
                _grid.Loaded -= OnLoaded;
                _grid.Unloaded -= OnUnloaded;
                _grid.LayoutUpdated -= OnLayoutUpdated;
            }

            public void OnLayoutUpdated(object sender, EventArgs e)
            {
                if (_updating)
                    return;

                if (_strip == null || VisualTreeHelper.GetParent(_strip) == null)
                    Resolve();

                Update();
            }

            private void Resolve()
            {
                _tabs = null;
                _left = null;
                _right = null;
                _tools = null;

                foreach (UIElement child in _grid.Children)
                {
                    var tabs = child as TabControl;
                    if (tabs != null)
                    {
                        _tabs = tabs;
                        continue;
                    }

                    if (Grid.GetRow(child) != 0)
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

                _strip = _tabs == null ? null : FindStrip(_tabs);
            }

            private void Update()
            {
                if (_updating || _grid.ActualWidth <= 0 || _strip == null || _tabs == null)
                    return;

                if (_grid.RowDefinitions.Count < 2)
                    return;

                double preferred = _strip.PreferredWidth;
                double available = Available();
                if (_applied
                    && !double.IsNaN(_lastPreferred)
                    && Math.Abs(preferred - _lastPreferred) < 0.5
                    && Math.Abs(available - _lastAvailable) < 0.5)
                    return;

                bool wrap = preferred > 0 && preferred > available + 1;
                _lastPreferred = preferred;
                _lastAvailable = available;
                if (_applied && wrap == _wrap)
                    return;

                _wrap = wrap;
                _applied = true;
                _updating = true;
                try
                {
                    if (wrap)
                    {
                        Grid.SetRow(_tabs, 1);
                        Grid.SetColumn(_tabs, 0);
                        Grid.SetColumnSpan(_tabs, Math.Max(1, _grid.ColumnDefinitions.Count));
                        _grid.RowDefinitions[1].Height = GridLength.Auto;
                    }
                    else
                    {
                        Grid.SetRow(_tabs, 0);
                        Grid.SetColumn(_tabs, 1);
                        Grid.SetColumnSpan(_tabs, 1);
                        _grid.RowDefinitions[1].Height = new GridLength(0);
                    }
                }
                finally
                {
                    _updating = false;
                }
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

            private static HeaderTabStrip FindStrip(DependencyObject root)
            {
                if (root == null)
                    return null;

                var strip = root as HeaderTabStrip;
                if (strip != null)
                    return strip;

                int count = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < count; i++)
                {
                    var found = FindStrip(VisualTreeHelper.GetChild(root, i));
                    if (found != null)
                        return found;
                }

                return null;
            }
        }
    }
}
