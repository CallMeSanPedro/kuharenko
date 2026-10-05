using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Store.Views.Reference.AdditionalAttributes
{
    /// <summary>
    /// Master-строка всегда забирает остаток высоты.
    /// Detail и разделитель занимают место только пока detail открыт,
    /// и вместе не выше MaxRatio доступной высоты.
    /// GridSplitter может только уменьшить detail: после драга master снова *.
    /// </summary>
    public static class MasterDetailRowLayout
    {
        public static readonly DependencyProperty IsDetailOpenProperty = DependencyProperty.RegisterAttached(
            "IsDetailOpen",
            typeof(object),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(null, OnLayoutChanged));

        public static readonly DependencyProperty MaxRatioProperty = DependencyProperty.RegisterAttached(
            "MaxRatio",
            typeof(double),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(0.3d, OnLayoutChanged));

        public static readonly DependencyProperty SplitterRowProperty = DependencyProperty.RegisterAttached(
            "SplitterRow",
            typeof(int),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(1, OnLayoutChanged));

        public static readonly DependencyProperty DetailRowProperty = DependencyProperty.RegisterAttached(
            "DetailRow",
            typeof(int),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(2, OnLayoutChanged));

        public static readonly DependencyProperty SplitterHeightProperty = DependencyProperty.RegisterAttached(
            "SplitterHeight",
            typeof(double),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(7d, OnLayoutChanged));

        private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        private static readonly DependencyProperty IsApplyingProperty = DependencyProperty.RegisterAttached(
            "IsApplying",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        private static readonly DependencyProperty IsDraggingProperty = DependencyProperty.RegisterAttached(
            "IsDragging",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        private static readonly DependencyProperty PendingOpenProperty = DependencyProperty.RegisterAttached(
            "PendingOpen",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        private static readonly DependencyProperty SizedByUserProperty = DependencyProperty.RegisterAttached(
            "SizedByUser",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        private static readonly DependencyProperty UserHeightProperty = DependencyProperty.RegisterAttached(
            "UserHeight",
            typeof(double),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(0d));

        private static readonly DependencyProperty SplitterHookedProperty = DependencyProperty.RegisterAttached(
            "SplitterHooked",
            typeof(bool),
            typeof(MasterDetailRowLayout),
            new PropertyMetadata(false));

        public static object GetIsDetailOpen(DependencyObject obj) => obj.GetValue(IsDetailOpenProperty);

        public static void SetIsDetailOpen(DependencyObject obj, object value) => obj.SetValue(IsDetailOpenProperty, value);

        public static double GetMaxRatio(DependencyObject obj) => (double)obj.GetValue(MaxRatioProperty);

        public static void SetMaxRatio(DependencyObject obj, double value) => obj.SetValue(MaxRatioProperty, value);

        public static int GetSplitterRow(DependencyObject obj) => (int)obj.GetValue(SplitterRowProperty);

        public static void SetSplitterRow(DependencyObject obj, int value) => obj.SetValue(SplitterRowProperty, value);

        public static int GetDetailRow(DependencyObject obj) => (int)obj.GetValue(DetailRowProperty);

        public static void SetDetailRow(DependencyObject obj, int value) => obj.SetValue(DetailRowProperty, value);

        public static double GetSplitterHeight(DependencyObject obj) => (double)obj.GetValue(SplitterHeightProperty);

        public static void SetSplitterHeight(DependencyObject obj, double value) => obj.SetValue(SplitterHeightProperty, value);

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as Grid;
            if (grid == null)
                return;

            if (e.Property == IsDetailOpenProperty && IsOpen(e.NewValue) && !IsOpen(e.OldValue))
                grid.SetValue(PendingOpenProperty, true);

            EnsureHooked(grid);
            if (grid.IsLoaded)
                Apply(grid, ConsumePendingOpen(grid));
        }

        private static void EnsureHooked(Grid grid)
        {
            if ((bool)grid.GetValue(IsHookedProperty))
                return;

            grid.SetValue(IsHookedProperty, true);
            grid.Loaded += OnGridLoaded;
            grid.SizeChanged += OnGridSizeChanged;
        }

        private static void OnGridLoaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            HookSplitter(grid);
            Apply(grid, ConsumePendingOpen(grid));
        }

        private static void OnGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged)
                return;

            var grid = (Grid)sender;
            if ((bool)grid.GetValue(IsDraggingProperty))
                return;

            Apply(grid, false);
        }

        private static bool ConsumePendingOpen(Grid grid)
        {
            var pending = (bool)grid.GetValue(PendingOpenProperty);
            if (pending)
                grid.ClearValue(PendingOpenProperty);
            return pending;
        }

        private static void HookSplitter(Grid grid)
        {
            var splitter = FindSplitter(grid);
            if (splitter == null || (bool)splitter.GetValue(SplitterHookedProperty))
                return;

            splitter.SetValue(SplitterHookedProperty, true);
            splitter.DragStarted += delegate { OnSplitterDragStarted(grid); };
            splitter.DragCompleted += delegate(object sender, DragCompletedEventArgs e) { OnSplitterDragCompleted(grid, e); };
        }

        private static void OnSplitterDragStarted(Grid grid)
        {
            grid.SetValue(IsDraggingProperty, true);
            grid.SetValue(SizedByUserProperty, true);
        }

        private static void OnSplitterDragCompleted(Grid grid, DragCompletedEventArgs e)
        {
            if (e.Canceled)
            {
                grid.SetValue(IsDraggingProperty, false);
                Apply(grid, false);
                return;
            }

            // ShowsPreview коммитит высоту в OnDragCompleted после вызова подписчиков.
            // Читаем ActualHeight уже после прохода layout.
            grid.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate { FinishDrag(grid); }));
        }

        private static void FinishDrag(Grid grid)
        {
            grid.SetValue(IsDraggingProperty, false);
            RowDefinition detailRow;
            RowDefinition splitterRow;
            if (TryGetRows(grid, out splitterRow, out detailRow))
                grid.SetValue(UserHeightProperty, detailRow.ActualHeight);
            grid.SetValue(SizedByUserProperty, true);
            Apply(grid, false);
        }

        private static void Apply(Grid grid, bool justOpened)
        {
            if ((bool)grid.GetValue(IsApplyingProperty) || (bool)grid.GetValue(IsDraggingProperty))
                return;

            RowDefinition splitterRow;
            RowDefinition detailRow;
            if (!TryGetRows(grid, out splitterRow, out detailRow))
                return;

            grid.SetValue(IsApplyingProperty, true);
            try
            {
                HookSplitter(grid);
                if (justOpened)
                {
                    grid.SetValue(SizedByUserProperty, false);
                    grid.ClearValue(UserHeightProperty);
                }

                SetMasterRowsStar(grid);
                var splitter = FindSplitter(grid);
                var open = IsOpen(grid.GetValue(IsDetailOpenProperty));
                var limit = DetailLimit(grid);

                if (!open || limit <= 0)
                {
                    CollapseRow(splitterRow);
                    CollapseRow(detailRow);
                    if (splitter != null)
                        splitter.Visibility = Visibility.Collapsed;
                    return;
                }

                var splitterHeight = SplitterPixels(grid);
                splitterRow.MinHeight = splitterHeight;
                splitterRow.MaxHeight = splitterHeight;
                SetPixelHeight(splitterRow, splitterHeight);
                if (splitter != null)
                    splitter.Visibility = Visibility.Visible;

                detailRow.MinHeight = 0;
                detailRow.MaxHeight = limit;
                SetPixelHeight(detailRow, DetailPixels(grid, limit));
            }
            finally
            {
                grid.ClearValue(IsApplyingProperty);
            }
        }

        private static double DetailPixels(Grid grid, double limit)
        {
            if (!(bool)grid.GetValue(SizedByUserProperty))
                return limit;

            var preferred = (double)grid.GetValue(UserHeightProperty);
            if (double.IsNaN(preferred) || preferred <= 0)
                return limit;

            return preferred > limit ? limit : preferred;
        }

        private static double DetailLimit(Grid grid)
        {
            var height = grid.ActualHeight;
            if (double.IsNaN(height) || height <= 0)
                return 0;

            var ratio = (double)grid.GetValue(MaxRatioProperty);
            if (double.IsNaN(ratio) || ratio <= 0)
                return 0;
            if (ratio > 1)
                ratio = 1;

            return Math.Max(0, height * ratio - SplitterPixels(grid));
        }

        private static void SetPixelHeight(RowDefinition row, double pixels)
        {
            if (row.Height.IsAbsolute && Math.Abs(row.Height.Value - pixels) < 0.5)
                return;

            row.Height = new GridLength(pixels);
        }

        private static double SplitterPixels(Grid grid)
        {
            var splitterHeight = (double)grid.GetValue(SplitterHeightProperty);
            if (double.IsNaN(splitterHeight) || splitterHeight < 0)
                return 0;
            return splitterHeight;
        }

        private static void SetMasterRowsStar(Grid grid)
        {
            var splitterIndex = (int)grid.GetValue(SplitterRowProperty);
            var detailIndex = (int)grid.GetValue(DetailRowProperty);
            for (var i = 0; i < grid.RowDefinitions.Count; i++)
            {
                if (i == splitterIndex || i == detailIndex)
                    continue;

                var row = grid.RowDefinitions[i];
                row.MinHeight = 0;
                row.MaxHeight = double.PositiveInfinity;
                if (row.Height.IsStar && Math.Abs(row.Height.Value - 1d) < 0.001)
                    continue;

                row.Height = new GridLength(1, GridUnitType.Star);
            }
        }

        private static void CollapseRow(RowDefinition row)
        {
            row.MinHeight = 0;
            row.MaxHeight = 0;
            row.Height = new GridLength(0);
        }

        private static bool TryGetRows(Grid grid, out RowDefinition splitterRow, out RowDefinition detailRow)
        {
            splitterRow = null;
            detailRow = null;
            var splitterIndex = (int)grid.GetValue(SplitterRowProperty);
            var detailIndex = (int)grid.GetValue(DetailRowProperty);
            if (splitterIndex < 0 || detailIndex < 0 || splitterIndex == detailIndex)
                return false;
            if (splitterIndex >= grid.RowDefinitions.Count || detailIndex >= grid.RowDefinitions.Count)
                return false;

            splitterRow = grid.RowDefinitions[splitterIndex];
            detailRow = grid.RowDefinitions[detailIndex];
            return true;
        }

        private static GridSplitter FindSplitter(Grid grid)
        {
            for (var i = 0; i < grid.Children.Count; i++)
            {
                var splitter = grid.Children[i] as GridSplitter;
                if (splitter != null)
                    return splitter;
            }

            return null;
        }

        private static bool IsOpen(object value)
        {
            if (value == null || value == DependencyProperty.UnsetValue)
                return false;

            var visible = value as Visibility?;
            if (visible.HasValue)
                return visible.Value == Visibility.Visible;

            var flag = value as bool?;
            if (flag.HasValue)
                return flag.Value;

            var text = value.ToString();
            if (string.Equals(text, "Visible", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(text, "True", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
