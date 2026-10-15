using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Store.Views.Theming
{
    /// <summary>
    /// Ставит иконку дерева через ссылку на ресурс темы.
    /// При смене словаря темы рисунок обновляется сам, без пересоздания строки.
    /// </summary>
    public static class TreeIcon
    {
        // Не null: у технического узла Dopinfo как раз null, и иначе свойство «не меняется».
        private const string Unassigned = "\u0000";

        public static readonly DependencyProperty TypeProperty = DependencyProperty.RegisterAttached(
            "Type",
            typeof(string),
            typeof(TreeIcon),
            new PropertyMetadata(Unassigned, OnTypeChanged));

        static TreeIcon()
        {
            EventManager.RegisterClassHandler(
                typeof(Image),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnImageLoaded),
                true);
        }

        public static void SetType(DependencyObject element, string value)
        {
            element.SetValue(TypeProperty, value);
        }

        public static string GetType(DependencyObject element)
        {
            return (string)element.GetValue(TypeProperty);
        }

        private static void OnImageLoaded(object sender, RoutedEventArgs e)
        {
            var image = (Image)sender;
            if (image.ReadLocalValue(TypeProperty) == DependencyProperty.UnsetValue)
                return;

            image.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Apply(image)));
        }

        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null)
                return;

            Apply(image);
            image.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Apply(image)));
        }

        private static void Apply(Image image)
        {
            if (image == null)
                return;

            var type = GetType(image);
            if (type == Unassigned)
            {
                // Привязка есть, но null так и не записался в свойство — это технический узел.
                if (!(image.ReadLocalValue(TypeProperty) is BindingExpression))
                    return;
                type = null;
            }

            image.SetResourceReference(Image.SourceProperty, KeyFor(type));
        }

        public static string KeyFor(string type)
        {
            switch (type)
            {
                case "1": return "TreeIcon.RepairStore";
                case "2": return "TreeIcon.DistributionStore";
                case "3": return "TreeIcon.KittingStore";
                case "4": return "TreeIcon.KittingStore";
                case "5": return "TreeIcon.ReturnStore";
                case "6": return "TreeIcon.WriteOffStore";
                case "7": return "TreeIcon.SiteStore";
                case "8": return "TreeIcon.InstallerBackpack";
                case "9": return "TreeIcon.SubscriberStore";
                case "10": return "TreeIcon.NetworkStore";
                default: return "TreeIcon.TechnicalNode";
            }
        }
    }
}
