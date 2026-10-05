using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Store.Views.Filtering
{
    /// <summary>
    /// Закрывает Popup фильтра по клику вне него.
    /// Клик по кнопке шапки и по вложенному списку остаётся внутри.
    /// </summary>
    public static class ColumnFilterPopup
    {
        public static readonly DependencyProperty ClosesOnOutsideClickProperty = DependencyProperty.RegisterAttached(
            "ClosesOnOutsideClick",
            typeof(bool),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(false, OnClosesOnOutsideClickChanged));

        private static readonly DependencyProperty HandlerProperty = DependencyProperty.RegisterAttached(
            "Handler",
            typeof(MouseButtonEventHandler),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(null));

        private static readonly DependencyProperty WindowProperty = DependencyProperty.RegisterAttached(
            "Window",
            typeof(Window),
            typeof(ColumnFilterPopup),
            new PropertyMetadata(null));

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
            Detach(popup);

            if ((bool)e.NewValue)
            {
                popup.Opened += OnOpened;
                popup.Closed += OnClosed;
            }
        }

        private static void OnOpened(object sender, EventArgs e)
        {
            var popup = (Popup)sender;
            Detach(popup);

            var window = Window.GetWindow(popup.PlacementTarget as DependencyObject) ?? Window.GetWindow(popup);
            if (window == null)
                return;

            MouseButtonEventHandler handler = (_, args) =>
            {
                if (!popup.IsOpen)
                    return;
                if (IsInside(args.OriginalSource as DependencyObject, popup))
                    return;

                popup.IsOpen = false;
            };

            popup.SetValue(HandlerProperty, handler);
            popup.SetValue(WindowProperty, window);
            window.AddHandler(UIElement.PreviewMouseDownEvent, handler, true);
        }

        private static void OnClosed(object sender, EventArgs e)
        {
            Detach((Popup)sender);
        }

        private static void Detach(Popup popup)
        {
            var handler = popup.GetValue(HandlerProperty) as MouseButtonEventHandler;
            var window = popup.GetValue(WindowProperty) as Window;
            popup.ClearValue(HandlerProperty);
            popup.ClearValue(WindowProperty);
            if (handler != null && window != null)
                window.RemoveHandler(UIElement.PreviewMouseDownEvent, handler);
        }

        private static bool IsInside(DependencyObject source, Popup popup)
        {
            foreach (var node in AncestorsAndSelf(source))
            {
                if (node == popup || node == popup.Child || node == popup.PlacementTarget)
                    return true;

                var nested = node as Popup;
                if (nested != null && nested != popup && nested.PlacementTarget is DependencyObject target && IsInside(target, popup))
                    return true;
            }

            return false;
        }

        private static IEnumerable<DependencyObject> AncestorsAndSelf(DependencyObject source)
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

                yield return node;

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
        }
    }
}
