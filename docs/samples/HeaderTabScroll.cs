using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Store.Views
{
    /// <summary>
    /// Стрелки влево и вправо у вкладок шапки. Пока пункты влезают, стрелок нет.
    /// </summary>
    public static class HeaderTabScroll
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HeaderTabScroll),
            new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(HeaderTabScroll),
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
            var tabs = d as TabControl;
            if (tabs == null)
                return;

            var state = tabs.GetValue(StateProperty) as State;
            if ((bool)e.NewValue)
            {
                if (state != null)
                    return;

                state = new State(tabs);
                tabs.SetValue(StateProperty, state);
                tabs.Loaded += state.OnLoaded;
                tabs.Unloaded += state.OnUnloaded;
                if (tabs.IsLoaded)
                    state.OnLoaded(tabs, new RoutedEventArgs());
            }
            else if (state != null)
            {
                state.Detach();
                tabs.ClearValue(StateProperty);
            }
        }

        private sealed class State
        {
            private readonly TabControl _tabs;
            private ScrollViewer _scroll;
            private RepeatButton _left;
            private RepeatButton _right;

            public State(TabControl tabs)
            {
                _tabs = tabs;
            }

            public void OnLoaded(object sender, RoutedEventArgs e)
            {
                _tabs.ApplyTemplate();
                UnhookParts();

                if (_tabs.Template == null)
                    return;

                _scroll = _tabs.Template.FindName("PART_HeaderScroll", _tabs) as ScrollViewer;
                _left = _tabs.Template.FindName("PART_ScrollLeft", _tabs) as RepeatButton;
                _right = _tabs.Template.FindName("PART_ScrollRight", _tabs) as RepeatButton;

                if (_scroll != null)
                {
                    _scroll.ScrollChanged += OnScrollChanged;
                    _scroll.SizeChanged += OnScrollSizeChanged;
                }

                if (_left != null)
                    _left.Click += OnLeftClick;
                if (_right != null)
                    _right.Click += OnRightClick;

                _tabs.SelectionChanged -= OnSelectionChanged;
                _tabs.SelectionChanged += OnSelectionChanged;
                _tabs.PreviewMouseWheel -= OnPreviewMouseWheel;
                _tabs.PreviewMouseWheel += OnPreviewMouseWheel;

                UpdateButtons();
                BringSelectedIntoView();
            }

            public void OnUnloaded(object sender, RoutedEventArgs e)
            {
                UnhookParts();
            }

            public void Detach()
            {
                _tabs.Loaded -= OnLoaded;
                _tabs.Unloaded -= OnUnloaded;
                _tabs.SelectionChanged -= OnSelectionChanged;
                _tabs.PreviewMouseWheel -= OnPreviewMouseWheel;
                UnhookParts();
            }

            private void UnhookParts()
            {
                if (_scroll != null)
                {
                    _scroll.ScrollChanged -= OnScrollChanged;
                    _scroll.SizeChanged -= OnScrollSizeChanged;
                    _scroll = null;
                }

                if (_left != null)
                {
                    _left.Click -= OnLeftClick;
                    _left = null;
                }

                if (_right != null)
                {
                    _right.Click -= OnRightClick;
                    _right = null;
                }
            }

            private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
            {
                UpdateButtons();
            }

            private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e)
            {
                UpdateButtons();
            }

            private void OnLeftClick(object sender, RoutedEventArgs e)
            {
                ScrollBy(-1);
            }

            private void OnRightClick(object sender, RoutedEventArgs e)
            {
                ScrollBy(1);
            }

            private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
            {
                if (_scroll == null || _scroll.ScrollableWidth <= 0.5)
                    return;

                ScrollBy(e.Delta < 0 ? 1 : -1);
                e.Handled = true;
            }

            private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                BringSelectedIntoView();
                UpdateButtons();
            }

            private void UpdateButtons()
            {
                if (_scroll == null)
                    return;

                bool overflow = _scroll.ScrollableWidth > 0.5;
                SetVisible(_left, overflow && _scroll.HorizontalOffset > 0.5);
                SetVisible(_right, overflow && _scroll.HorizontalOffset < _scroll.ScrollableWidth - 0.5);
            }

            private static void SetVisible(UIElement element, bool visible)
            {
                if (element == null)
                    return;

                var next = visible ? Visibility.Visible : Visibility.Collapsed;
                if (element.Visibility != next)
                    element.Visibility = next;
            }

            private void ScrollBy(int direction)
            {
                if (_scroll == null || direction == 0)
                    return;

                double offset = _scroll.HorizontalOffset;
                double view = _scroll.ViewportWidth;
                double target = offset + (direction * 96);

                for (int i = 0; i < _tabs.Items.Count; i++)
                {
                    var tab = Container(i);
                    if (tab == null)
                        continue;

                    double left;
                    if (!TryContentLeft(tab, out left))
                        continue;

                    double right = left + tab.ActualWidth;
                    if (direction > 0 && right > offset + view + 1)
                    {
                        target = right - view;
                        break;
                    }

                    if (direction < 0 && left < offset - 1)
                        target = left;
                }

                if (target < 0)
                    target = 0;
                if (target > _scroll.ScrollableWidth)
                    target = _scroll.ScrollableWidth;

                _scroll.ScrollToHorizontalOffset(target);
            }

            private void BringSelectedIntoView()
            {
                if (_scroll == null || _tabs == null)
                    return;

                var tab = _tabs.ItemContainerGenerator.ContainerFromItem(_tabs.SelectedItem) as TabItem;
                if (tab == null)
                    tab = _tabs.SelectedItem as TabItem;
                if (tab == null)
                    return;

                double left;
                if (!TryContentLeft(tab, out left))
                    return;

                double right = left + tab.ActualWidth;
                double offset = _scroll.HorizontalOffset;
                double view = _scroll.ViewportWidth;
                if (left < offset)
                    _scroll.ScrollToHorizontalOffset(left);
                else if (right > offset + view)
                    _scroll.ScrollToHorizontalOffset(Math.Max(0, right - view));
            }

            private TabItem Container(int index)
            {
                var tab = _tabs.ItemContainerGenerator.ContainerFromIndex(index) as TabItem;
                if (tab == null)
                    tab = _tabs.Items[index] as TabItem;
                if (tab == null || tab.Visibility != Visibility.Visible)
                    return null;
                return tab;
            }

            private bool TryContentLeft(TabItem tab, out double left)
            {
                left = 0;
                if (tab == null || _scroll == null || !tab.IsVisible)
                    return false;

                try
                {
                    var point = tab.TransformToVisual(_scroll).Transform(new Point(0, 0));
                    left = point.X + _scroll.HorizontalOffset;
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }
}
