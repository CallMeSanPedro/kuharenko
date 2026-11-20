using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Store.Views.Filtering
{
    /// <summary>
    /// Закрывает Popup по щелчку в окне вне кнопки фильтра.
    /// StaysOpen остаётся True: выпадающий список внутри фильтра живёт в своём Popup
    /// и не должен гасить шапку.
    /// </summary>
    public static class FilterPopup
    {
        public static readonly DependencyProperty CloseOnOutsideClickProperty = DependencyProperty.RegisterAttached(
            "CloseOnOutsideClick",
            typeof(bool),
            typeof(FilterPopup),
            new PropertyMetadata(false, OnCloseOnOutsideClickChanged));

        public static bool GetCloseOnOutsideClick(DependencyObject element)
        {
            return (bool)element.GetValue(CloseOnOutsideClickProperty);
        }

        public static void SetCloseOnOutsideClick(DependencyObject element, bool value)
        {
            element.SetValue(CloseOnOutsideClickProperty, value);
        }

        private static readonly DependencyProperty HandlerProperty = DependencyProperty.RegisterAttached(
            "Handler",
            typeof(MouseButtonEventHandler),
            typeof(FilterPopup),
            new PropertyMetadata(null));

        private static readonly DependencyProperty WindowProperty = DependencyProperty.RegisterAttached(
            "Window",
            typeof(Window),
            typeof(FilterPopup),
            new PropertyMetadata(null));

        private static void OnCloseOnOutsideClickChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null)
                return;

            if ((bool)e.NewValue)
            {
                popup.Opened += OnOpened;
                popup.Closed += OnClosed;
            }
            else
            {
                popup.Opened -= OnOpened;
                popup.Closed -= OnClosed;
                Detach(popup);
            }
        }

        private static void OnOpened(object sender, EventArgs e)
        {
            var popup = (Popup)sender;
            Detach(popup);
            var window = Window.GetWindow(popup.PlacementTarget);
            if (window == null)
                return;

            MouseButtonEventHandler handler = (_, args) => OnWindowMouseDown(popup, args);
            window.AddHandler(UIElement.PreviewMouseDownEvent, handler, true);
            popup.SetValue(WindowProperty, window);
            popup.SetValue(HandlerProperty, handler);
        }

        private static void OnClosed(object sender, EventArgs e)
        {
            Detach((Popup)sender);
        }

        private static void Detach(Popup popup)
        {
            var window = popup.GetValue(WindowProperty) as Window;
            var handler = popup.GetValue(HandlerProperty) as MouseButtonEventHandler;
            if (window != null && handler != null)
                window.RemoveHandler(UIElement.PreviewMouseDownEvent, handler);
            popup.ClearValue(WindowProperty);
            popup.ClearValue(HandlerProperty);
        }

        private static void OnWindowMouseDown(Popup popup, MouseButtonEventArgs e)
        {
            if (!popup.IsOpen)
                return;

            var source = e.OriginalSource as DependencyObject;
            var target = popup.PlacementTarget as DependencyObject;
            if (target != null && IsWithin(target, source))
                return;

            popup.IsOpen = false;
        }

        private static bool IsWithin(DependencyObject root, DependencyObject node)
        {
            while (node != null)
            {
                if (node == root)
                    return true;

                DependencyObject parent = null;
                if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                    parent = VisualTreeHelper.GetParent(node);
                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(node);
                node = parent;
            }

            return false;
        }
    }
}
