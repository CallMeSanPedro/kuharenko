using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Store.Views.Filtering
{
    /// <summary>
    /// Закрывает Popup фильтра по клику вне кнопки шапки.
    /// Клики внутри Popup до окна не всплывают, поэтому список и вложенный
    /// MultiSelectComboBox остаются открытыми без обхода их дерева.
    /// </summary>
    public static class ColumnFilterPopup
    {
        public static readonly DependencyProperty ClosesOnOutsideClickProperty = DependencyProperty.RegisterAttached(
            "ClosesOnOutsideClick",
            typeof(bool),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(false, OnClosesOnOutsideClickChanged));

        private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached(
            "Session",
            typeof(CloseSession),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(null));

        private static readonly DependencyProperty RetriedProperty = DependencyProperty.RegisterAttached(
            "Retried",
            typeof(bool),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(false));

        public static bool GetClosesOnOutsideClick(DependencyObject element)
        {
            return (bool)element.GetValue(ClosesOnOutsideClickProperty);
        }

        public static void SetClosesOnOutsideClick(DependencyObject element, bool value)
        {
            element.SetValue(ClosesOnOutsideClickProperty, value);
        }

        private static void OnClosesOnOutsideClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var popup = d as Popup;
            if (popup == null)
                return;

            popup.Opened -= OnOpened;
            popup.Closed -= OnClosed;
            EndSession(popup);

            if (!(bool)e.NewValue)
            {
                popup.IsOpen = false;
                return;
            }

            popup.Opened += OnOpened;
            popup.Closed += OnClosed;
            if (popup.IsOpen)
                OnOpened(popup, EventArgs.Empty);
        }

        private static void OnOpened(object sender, EventArgs e)
        {
            var popup = (Popup)sender;
            EndSession(popup);

            var window = Window.GetWindow(popup.PlacementTarget as DependencyObject);
            if (window == null)
            {
                if ((bool)popup.GetValue(RetriedProperty))
                    return;

                popup.SetValue(RetriedProperty, true);
                popup.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (popup.IsOpen && GetClosesOnOutsideClick(popup) && GetSession(popup) == null)
                        OnOpened(popup, EventArgs.Empty);
                }));
                return;
            }

            popup.ClearValue(RetriedProperty);
            var session = new CloseSession(popup, window);
            popup.SetValue(SessionProperty, session);
            session.Attach();
        }

        private static void OnClosed(object sender, EventArgs e)
        {
            EndSession((Popup)sender);
        }

        private static void EndSession(Popup popup)
        {
            var session = popup.GetValue(SessionProperty) as CloseSession;
            popup.ClearValue(SessionProperty);
            session?.Detach();
        }

        private static CloseSession GetSession(Popup popup)
        {
            return popup.GetValue(SessionProperty) as CloseSession;
        }

        private sealed class CloseSession
        {
            private readonly Popup _popup;
            private readonly Window _window;
            private readonly MouseButtonEventHandler _mouseDown;
            private readonly MouseWheelEventHandler _mouseWheel;
            private readonly UIElement _child;
            private readonly KeyEventHandler _keyDown;
            private readonly EventHandler _deactivated;
            private readonly RoutedEventHandler _unloaded;

            public CloseSession(Popup popup, Window window)
            {
                _popup = popup;
                _window = window;
                _child = popup.Child as UIElement;
                _mouseDown = OnMouseDown;
                _mouseWheel = OnMouseWheel;
                _keyDown = OnKeyDown;
                _deactivated = OnDeactivated;
                _unloaded = OnUnloaded;
            }

            public void Attach()
            {
                _window.AddHandler(UIElement.PreviewMouseDownEvent, _mouseDown, true);
                _window.AddHandler(UIElement.PreviewMouseWheelEvent, _mouseWheel, true);
                _window.Deactivated += _deactivated;
                _child?.AddHandler(UIElement.PreviewKeyDownEvent, _keyDown, true);
                _popup.Unloaded += _unloaded;
            }

            public void Detach()
            {
                _window.RemoveHandler(UIElement.PreviewMouseDownEvent, _mouseDown);
                _window.RemoveHandler(UIElement.PreviewMouseWheelEvent, _mouseWheel);
                _window.Deactivated -= _deactivated;
                _child?.RemoveHandler(UIElement.PreviewKeyDownEvent, _keyDown);
                _popup.Unloaded -= _unloaded;
            }

            private void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                if (!_popup.IsOpen || IsInside(e.OriginalSource as DependencyObject))
                    return;

                _popup.IsOpen = false;
            }

            private void OnMouseWheel(object sender, MouseWheelEventArgs e)
            {
                if (!_popup.IsOpen || IsInside(e.OriginalSource as DependencyObject))
                    return;

                _popup.IsOpen = false;
            }

            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                if (!_popup.IsOpen || e.Key != Key.Escape)
                    return;

                _popup.IsOpen = false;
                e.Handled = true;
            }

            private void OnDeactivated(object sender, EventArgs e)
            {
                if (!_popup.IsOpen)
                    return;

                // Клик по Popup активирует его окно и снимает активацию с основного.
                // Закрывать можно только если фокус и курсор уже не внутри фильтра.
                _popup.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(CloseIfLeft));
            }

            private void CloseIfLeft()
            {
                if (!_popup.IsOpen)
                    return;
                if (IsInside(Mouse.DirectlyOver as DependencyObject) || IsInside(Keyboard.FocusedElement as DependencyObject))
                    return;

                _popup.IsOpen = false;
            }

            private void OnUnloaded(object sender, RoutedEventArgs e)
            {
                var wasOpen = _popup.IsOpen;
                _popup.IsOpen = false;
                if (!wasOpen)
                    EndSession(_popup);
            }

            private bool IsInside(DependencyObject source)
            {
                var seen = new HashSet<DependencyObject>();
                var pending = new Stack<DependencyObject>();
                if (source != null)
                    pending.Push(source);

                while (pending.Count > 0)
                {
                    var node = pending.Pop();
                    if (node == null || !seen.Add(node))
                        continue;
                    if (node == _popup || node == _popup.Child || node == _popup.PlacementTarget)
                        return true;

                    var nested = node as Popup;
                    if (nested != null && nested != _popup && nested.PlacementTarget is DependencyObject target)
                        pending.Push(target);

                    if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                    {
                        var visualParent = VisualTreeHelper.GetParent(node);
                        if (visualParent != null)
                            pending.Push(visualParent);
                    }

                    var logicalParent = LogicalTreeHelper.GetParent(node);
                    if (logicalParent != null)
                        pending.Push(logicalParent);

                    var element = node as FrameworkElement;
                    if (element?.Parent != null)
                        pending.Push(element.Parent);

                    var content = node as FrameworkContentElement;
                    if (content?.Parent != null)
                        pending.Push(content.Parent);
                }

                return false;
            }
        }
    }
}
