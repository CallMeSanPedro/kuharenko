using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Store.ViewModels.Device.TechnoReserv;

namespace Store.Views.Controls
{
    public partial class ModelShareChart : UserControl
    {
        static readonly Color[] Palette =
        {
            Color.FromRgb(0x25, 0x63, 0xEB),
            Color.FromRgb(0x0F, 0x76, 0x6E),
            Color.FromRgb(0xD9, 0x77, 0x06),
            Color.FromRgb(0x7C, 0x3A, 0xED),
            Color.FromRgb(0xDB, 0x27, 0x77),
            Color.FromRgb(0x08, 0x91, 0xB2),
            Color.FromRgb(0x65, 0xA3, 0x0D),
            Color.FromRgb(0x94, 0xA3, 0xB8)
        };

        INotifyCollectionChanged _source;

        public ModelShareChart()
        {
            InitializeComponent();
            Unloaded += (s, e) => Watch(null);
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ModelShareChart),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var chart = (ModelShareChart)sender;
            chart.Watch(args.NewValue as INotifyCollectionChanged);
            chart.Redraw();
        }

        void Watch(INotifyCollectionChanged source)
        {
            if (_source != null)
                _source.CollectionChanged -= OnSourceChanged;
            _source = source;
            if (_source != null)
                _source.CollectionChanged += OnSourceChanged;
        }

        void OnSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Redraw();
        }

        void Redraw()
        {
            Pie.Children.Clear();
            Legend.Children.Clear();

            var shares = (ItemsSource == null
                    ? Enumerable.Empty<ModelShare>()
                    : ItemsSource.OfType<ModelShare>())
                .Where(x => x != null && x.Value > 0)
                .ToList();
            var total = shares.Sum(x => x.Value);
            if (shares.Count == 0 || total <= 0)
            {
                Empty.Visibility = Visibility.Visible;
                return;
            }

            Empty.Visibility = Visibility.Collapsed;
            var angle = -90.0;
            for (var i = 0; i < shares.Count; i++)
            {
                var share = shares[i];
                var sweep = i == shares.Count - 1
                    ? Math.Max(0, 270 - angle)
                    : share.Value / total * 360.0;
                var brush = BrushFor(i, share.Label);
                Pie.Children.Add(new Path
                {
                    Fill = brush,
                    Data = Slice(100, 100, 96, angle, sweep),
                    ToolTip = share.Label + "  " + FormatValue(share.Value) + "  " + share.Percent.ToString("P0")
                });
                Legend.Children.Add(LegendRow(share, brush));
                angle += sweep;
            }
        }

        static string FormatValue(double value)
        {
            return value.ToString("0.##", CultureInfo.CurrentCulture);
        }

        static Brush BrushFor(int index, string label)
        {
            var color = string.Equals(label, "Прочие", StringComparison.Ordinal)
                ? Palette[Palette.Length - 1]
                : Palette[index % Palette.Length];
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        static Geometry Slice(double cx, double cy, double radius, double startAngle, double sweep)
        {
            if (sweep >= 359.9)
                return new EllipseGeometry(new Point(cx, cy), radius, radius);

            var start = PointOnCircle(cx, cy, radius, startAngle);
            var end = PointOnCircle(cx, cy, radius, startAngle + sweep);
            if ((start - end).Length < 0.4)
                return Geometry.Empty;

            var figure = new PathFigure
            {
                StartPoint = new Point(cx, cy),
                IsClosed = true
            };
            figure.Segments.Add(new LineSegment(start, true));
            figure.Segments.Add(new ArcSegment(
                end,
                new Size(radius, radius),
                0,
                sweep > 180,
                SweepDirection.Clockwise,
                true));
            return new PathGeometry(new[] { figure });
        }

        static Point PointOnCircle(double cx, double cy, double radius, double angleDegrees)
        {
            var radians = angleDegrees * Math.PI / 180.0;
            return new Point(cx + radius * Math.Cos(radians), cy + radius * Math.Sin(radians));
        }

        static UIElement LegendRow(ModelShare share, Brush brush)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var mark = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Background = brush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = share.Label,
                Foreground = Frozen(0x0F, 0x17, 0x2A),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            var value = new TextBlock
            {
                Text = FormatValue(share.Value),
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = Frozen(0x64, 0x74, 0x8B),
                VerticalAlignment = VerticalAlignment.Center
            };
            var percent = new TextBlock
            {
                Text = share.Percent.ToString("P0"),
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = Frozen(0x64, 0x74, 0x8B),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(mark, 0);
            Grid.SetColumn(label, 1);
            Grid.SetColumn(value, 2);
            Grid.SetColumn(percent, 3);
            row.Children.Add(mark);
            row.Children.Add(label);
            row.Children.Add(value);
            row.Children.Add(percent);
            return row;
        }

        static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
