using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Store.Views
{
    /// <summary>
    /// Панель вкладок шапки. Влезшие пункты остаются в строке, остальные переносятся в меню «Ещё».
    /// Видимость самих TabItem не меняется: привязки CanMenuOebs / CanMenuAudit остаются на месте.
    /// </summary>
    public class HeaderOverflowPanel : Panel
    {
        public static readonly DependencyProperty MoreMenuProperty = DependencyProperty.RegisterAttached(
            "MoreMenu",
            typeof(MenuItem),
            typeof(HeaderOverflowPanel),
            new PropertyMetadata(null, OnMoreMenuChanged));

        public static readonly DependencyProperty ContainsSelectionProperty = DependencyProperty.RegisterAttached(
            "ContainsSelection",
            typeof(bool),
            typeof(HeaderOverflowPanel),
            new PropertyMetadata(false));

        public static MenuItem GetMoreMenu(DependencyObject obj)
        {
            return (MenuItem)obj.GetValue(MoreMenuProperty);
        }

        public static void SetMoreMenu(DependencyObject obj, MenuItem value)
        {
            obj.SetValue(MoreMenuProperty, value);
        }

        public static bool GetContainsSelection(DependencyObject obj)
        {
            return (bool)obj.GetValue(ContainsSelectionProperty);
        }

        public static void SetContainsSelection(DependencyObject obj, bool value)
        {
            obj.SetValue(ContainsSelectionProperty, value);
        }

        private readonly List<TabItem> _overflow = new List<TabItem>();
        private TabControl _tabs;
        private bool _menuUpdateQueued;

        public HeaderOverflowPanel()
        {
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Hook();
            InvalidateMeasure();
        }

        private static void OnMoreMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabs = d as TabControl;
            if (tabs == null)
                return;

            tabs.Dispatcher.BeginInvoke(new Action(() =>
            {
                var panel = FindPanel(tabs);
                if (panel != null)
                    panel.InvalidateMeasure();
            }), DispatcherPriority.Loaded);
        }

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

            double width = double.IsInfinity(constraint.Width) ? sum : Math.Min(sum, constraint.Width);
            return new Size(width, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Hook();

            var visible = new List<TabItem>();
            foreach (UIElement child in InternalChildren)
            {
                var tab = child as TabItem;
                if (tab != null && tab.Visibility == Visibility.Visible)
                    visible.Add(tab);
            }

            var shown = new List<TabItem>();
            var overflow = new List<TabItem>();
            if (!CanMoveToMenu())
            {
                shown.AddRange(visible);
            }
            else
            {
                double sum = 0;
                foreach (var tab in visible)
                    sum += tab.DesiredSize.Width;

                if (sum <= finalSize.Width + 0.5)
                {
                    shown.AddRange(visible);
                }
                else
                {
                    double used = 0;
                    bool overflowing = false;
                    foreach (var tab in visible)
                    {
                        double w = tab.DesiredSize.Width;
                        if (!overflowing && used + w <= finalSize.Width + 0.5)
                        {
                            shown.Add(tab);
                            used += w;
                        }
                        else
                        {
                            overflowing = true;
                            overflow.Add(tab);
                        }
                    }
                }
            }

            double shownWidth = 0;
            foreach (var tab in shown)
                shownWidth += tab.DesiredSize.Width;

            double x = 0;
            if (overflow.Count == 0 && shownWidth < finalSize.Width)
                x = Math.Max(0, (finalSize.Width - shownWidth) / 2);

            foreach (UIElement child in InternalChildren)
            {
                var tab = child as TabItem;
                if (tab == null || tab.Visibility != Visibility.Visible || !shown.Contains(tab))
                {
                    child.Arrange(new Rect(0, 0, 0, 0));
                    if (tab != null && tab.Visibility == Visibility.Visible)
                        tab.IsHitTestVisible = false;
                    continue;
                }

                double w = tab.DesiredSize.Width;
                tab.IsHitTestVisible = true;
                tab.Arrange(new Rect(x, 0, w, finalSize.Height));
                x += w;
            }

            UpdateOverflow(overflow);
            return finalSize;
        }

        private bool CanMoveToMenu()
        {
            var tabs = TemplatedParent as TabControl;
            return tabs != null && GetMoreMenu(tabs) != null;
        }

        private void UpdateOverflow(List<TabItem> overflow)
        {
            if (SameOverflow(overflow))
                return;

            _overflow.Clear();
            _overflow.AddRange(overflow);
            QueueMenuUpdate();
        }

        private bool SameOverflow(List<TabItem> next)
        {
            if (_overflow.Count != next.Count)
                return false;

            for (int i = 0; i < _overflow.Count; i++)
            {
                if (!ReferenceEquals(_overflow[i], next[i]))
                    return false;
            }

            return true;
        }

        private void QueueMenuUpdate()
        {
            if (_menuUpdateQueued)
                return;

            _menuUpdateQueued = true;
            Dispatcher.BeginInvoke(new Action(FlushMenu), DispatcherPriority.Loaded);
        }

        private void FlushMenu()
        {
            _menuUpdateQueued = false;
            var more = _tabs == null ? null : GetMoreMenu(_tabs);
            if (more == null)
                return;

            var host = more.Parent as UIElement;
            if (host == null)
                host = more;

            host.Visibility = _overflow.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            more.Items.Clear();
            var itemStyle = _tabs.TryFindResource("HeaderDropdownMenuItem") as Style;
            foreach (var tab in _overflow)
            {
                var item = new MenuItem();
                item.Header = tab.Header is string ? tab.Header : (tab.Header == null ? null : tab.Header.ToString());
                item.Tag = tab;
                item.Style = itemStyle;
                item.Click += OnOverflowClick;
                more.Items.Add(item);
            }

            ApplySelectionState();
        }

        private void OnOverflowClick(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            var tab = item == null ? null : item.Tag as TabItem;
            if (tab == null)
                return;

            tab.IsSelected = true;
        }

        private void Hook()
        {
            var tabs = TemplatedParent as TabControl;
            if (tabs == null || ReferenceEquals(tabs, _tabs))
                return;

            Unhook();
            _tabs = tabs;
            _tabs.SelectionChanged += OnSelectionChanged;
            _tabs.Unloaded += OnTabsUnloaded;
        }

        private void Unhook()
        {
            if (_tabs == null)
                return;

            _tabs.SelectionChanged -= OnSelectionChanged;
            _tabs.Unloaded -= OnTabsUnloaded;
            _tabs = null;
        }

        private void OnTabsUnloaded(object sender, RoutedEventArgs e)
        {
            Unhook();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySelectionState();
        }

        private void ApplySelectionState()
        {
            var more = _tabs == null ? null : GetMoreMenu(_tabs);
            if (more == null)
                return;

            bool contains = false;
            foreach (var entry in more.Items)
            {
                var item = entry as MenuItem;
                var tab = item == null ? null : item.Tag as TabItem;
                bool selected = tab != null && tab.IsSelected;
                if (item != null)
                    item.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
                if (selected)
                    contains = true;
            }

            SetContainsSelection(more, contains);
        }

        private static HeaderOverflowPanel FindPanel(DependencyObject root)
        {
            if (root == null)
                return null;

            var panel = root as HeaderOverflowPanel;
            if (panel != null)
                return panel;

            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var found = FindPanel(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
