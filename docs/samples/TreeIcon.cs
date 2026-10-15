using System.Windows;
using System.Windows.Controls;

namespace Store.Views.Theming
{
    /// <summary>
    /// Ставит иконку дерева через ссылку на ресурс темы.
    /// При смене словаря темы рисунок обновляется сам, без пересоздания строки.
    /// </summary>
    public static class TreeIcon
    {
        public static readonly DependencyProperty TypeProperty = DependencyProperty.RegisterAttached(
            "Type",
            typeof(string),
            typeof(TreeIcon),
            new PropertyMetadata(null, OnTypeChanged));

        static TreeIcon()
        {
            // У технического узла Dopinfo пустой и совпадает со значением по умолчанию,
            // поэтому OnTypeChanged не вызывается. Loaded ставит иконку и в этом случае.
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

            Apply(image);
        }

        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Apply(d as Image);
        }

        private static void Apply(Image image)
        {
            if (image == null)
                return;

            image.SetResourceReference(Image.SourceProperty, KeyFor(GetType(image)));
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
