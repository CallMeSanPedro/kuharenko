using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DatumNode.ViewModels;
using Infrastructure.Common.ViewModels;
using Store.Models.Tree;
using Store.ViewModels;
using Store.ViewModels.Params;
using Store.ViewModels.Reference.History;
using Store.ViewModels.Tree;
using Store.Views.Utils;

namespace Store.Views.Tree
{
	public partial class StoreTreeView 
	{
        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.Register(
                nameof(IsCollapsed),
                typeof(bool),
                typeof(StoreTreeView),
                new FrameworkPropertyMetadata(false, OnIsCollapsedChanged));

        public bool IsCollapsed
        {
            get => (bool)GetValue(IsCollapsedProperty);
            set => SetValue(IsCollapsedProperty, value);
        }

        public static event EventHandler<bool> IsCollapsedChanged;

        private double _expandedWidth = 260;

		public StoreTreeView()
		{
			InitializeComponent();

            Loaded += StoreTreeView_Loaded;
		}

        private void StoreTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            AttachToggleHover(CollapseButton, CollapseIcon, CollapseLabel);
            UpdateCollapsedVisuals(IsCollapsed);
        }

        private void AttachToggleHover(Border border, Path icon, TextBlock label = null)
        {
            if (border == null) return;
            border.MouseEnter += (s, ev) =>
            {
                if (TryFindResource("Sidebar.ItemHoverBg") is Brush hoverBg)
                    border.Background = hoverBg;
                if (TryFindResource("Sidebar.ItemHoverFg") is Brush hoverFg)
                {
                    if (icon != null) icon.Fill = hoverFg;
                    if (label != null) label.Foreground = hoverFg;
                }
            };

            border.MouseLeave += (s, ev) =>
            {
                border.Background = Brushes.Transparent;
                var idleFg = (TryFindResource("Sidebar.ItemIdleFg") as Brush) ?? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                if (icon != null) icon.Fill = idleFg;
                if (label != null) label.Foreground = idleFg;
            };
        }

        private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StoreTreeView view && e.NewValue is bool isCollapsed)
            {
                view.UpdateCollapsedVisuals(isCollapsed);
                IsCollapsedChanged?.Invoke(view, isCollapsed);
            }
        }

        public void UpdateCollapsedVisuals(bool isCollapsed)
        {
            if (isCollapsed)
            {
                if (ActualWidth > 80)
                    _expandedWidth = ActualWidth;

                Width = 56;
                MinWidth = 56;
                MaxWidth = 56;
                HorizontalAlignment = HorizontalAlignment.Stretch;

                if (HeaderTitleText != null) HeaderTitleText.Visibility = Visibility.Collapsed;
                if (HeaderWarehouseIcon != null)
                {
                    HeaderWarehouseIcon.Visibility = Visibility.Visible;
                    HeaderWarehouseIcon.HorizontalAlignment = HorizontalAlignment.Center;
                    HeaderWarehouseIcon.Margin = new Thickness(0);
                }
                if (smbx != null) smbx.Visibility = Visibility.Collapsed;
                if (CollapseLabel != null) CollapseLabel.Visibility = Visibility.Collapsed;

                if (TreeScrollViewer != null)
                {
                    TreeScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    TreeScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }

                if (CollapseIcon != null && TryFindResource("SidebarIconExpand") is Geometry expandGeom)
                    CollapseIcon.Data = expandGeom;

                if (CollapseToolTip != null)
                    CollapseToolTip.Content = Store.Localization.Translation.Instance.Get("Sidebar_Expand", "Развернуть");
            }
            else
            {
                Width = double.NaN;
                MinWidth = 180;
                MaxWidth = double.PositiveInfinity;
                HorizontalAlignment = HorizontalAlignment.Stretch;

                if (HeaderTitleText != null) HeaderTitleText.Visibility = Visibility.Visible;
                if (HeaderWarehouseIcon != null)
                {
                    HeaderWarehouseIcon.Visibility = Visibility.Visible;
                    HeaderWarehouseIcon.HorizontalAlignment = HorizontalAlignment.Left;
                    HeaderWarehouseIcon.Margin = new Thickness(0, 0, 6, 0);
                }
                if (smbx != null) smbx.Visibility = Visibility.Visible;
                if (CollapseLabel != null) CollapseLabel.Visibility = Visibility.Visible;

                if (TreeScrollViewer != null)
                {
                    TreeScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    TreeScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }

                if (CollapseIcon != null && TryFindResource("SidebarIconCollapse") is Geometry collapseGeom)
                    CollapseIcon.Data = collapseGeom;

                if (CollapseToolTip != null)
                    CollapseToolTip.Content = Store.Localization.Translation.Instance.Get("Sidebar_Collapse", "Свернуть");
            }
        }

        private void CollapseButton_Click(object sender, MouseButtonEventArgs e)
        {
            IsCollapsed = !IsCollapsed;
        }

        private void TreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TreeScrollViewer != null && !e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = MouseWheelEvent,
                    Source = sender
                };
                TreeScrollViewer.RaiseEvent(eventArg);
            }
        }

		private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if(Store.Views.Utils.MouseDoubleClick.IsDoubleClick(sender,e))
			{
                var tr = ((StoreTreeViewModel)DataContext);
                if (tr == null) return;
                if (!tr.CanShowHistoryCommand()) return;

                var t = ((TreeItemViewModel<StoreTree>)(((tr.CurrentStoreItem).Container).CurrentItem)).Item.EquipId;
				var vm = new RowHistoryViewModel();

				if (t.HasValue && vm != null)
                {
                    vm.ShowHistory("store_infouzel", t.Value, tr);
                }
			}
		}

		private void StoreTextChange(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			if (DataContext != null)
			{
				var tr = ((StoreTreeViewModel)DataContext);
				if (sender != null)
				{
					var findText = ((System.Windows.Controls.TextBox)sender).Text;
					if (findText.Length >= 3 || findText.Length==0)
					{                        
						tr.Filter.FindStoreText = findText;
						tr.Filter.AcceptChanges();
						tr.MySearch();
					}
				}
			}
		}

		private void ShowHiddenStore_Checked(object sender, RoutedEventArgs e)
		{
			var tr = ((StoreTreeViewModel)DataContext);
			tr.Filter.ShowArchiveStores = true;
		}

		private void ShowHiddenStore_Unchecked(object sender, RoutedEventArgs e)
		{
			var tr = ((StoreTreeViewModel)DataContext);
			tr.Filter.ShowArchiveStores = false;
		}
    }
}
