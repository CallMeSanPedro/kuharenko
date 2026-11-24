using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Store.Device.ViewModels;
using Store.Models.Device;
using Store.ViewModels.Device;

namespace Store.Views.Device
{
	public partial class InventoryListView 
	{
		public InventoryListView()			
		{
			InitializeComponent();
		}

		//private void FilterExpander_Expanded(object sender, System.Windows.RoutedEventArgs e)
		//{
		//	FilterExpander.Header = "Фильтр.";
		//}

		//private void FilterExpander_Collapsed(object sender, System.Windows.RoutedEventArgs e)
		//{
		//	var tr = ((InventoryListViewModel)DataContext);
		//	if (tr.Filter.ExpanderText != null)
		//		FilterExpander.Header = tr.Filter.ExpanderText.Trim().Length > 0 ? tr.Filter.ExpanderText : "Фильтр.";

		//}

        private void QuickSearch(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var tr = ((InventoryListViewModel)DataContext);
                if (tr != null)
                    tr.Search();
            }

        }

        private void QuickSearchList(object sender, SelectionChangedEventArgs e)
        {
            var tr = ((InventoryListViewModel)DataContext);
            if (tr!=null)
                tr.Search();
        }

        private static readonly HashSet<string> HiddenRowDetailsProperties = new HashSet<string>
        {
            nameof(InventoryList.SerialDevices),
            nameof(InventoryList.IsLoadingSerials),
            nameof(InventoryList.LoadedSerialCount),
            nameof(InventoryList.SerialsLoaded),
            nameof(InventoryList.SerialsError),
            nameof(InventoryList.HasSerials),
            nameof(InventoryList.HasSerialsError),
            nameof(InventoryList.HasPreviewTruncated)
        };

        private void cdgInventory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is DataGridDetailsPresenter)
                    return;
                dep = VisualTreeHelper.GetParent(dep);
            }

            ShowSerials();
        }

        //private void OnInventoryDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //	ShowSerials();
        //}

        //private voin OnInventoryKeyDown(object sender, KeyEventArgs e)
        //{
        //	if
        //}

        private void ShowSerials()
        {
            var viewmodel = DataContext as InventoryListViewModel;
            if (viewmodel == null || viewmodel.ShowSerialCommand == null)
                return;

            if (viewmodel.ShowSerialCommand.CanExecute(null))
                viewmodel.ShowSerialCommand.Execute(null);
        }
        private bool _cellsReady;

        private void cdgInventory_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_cellsReady || !cdgInventory.IsVisible != true)
                return;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (_cellsReady || !cdgInventory.IsVisible || cdgInventory.Items.Count == 0)
                    return;
                _cellsReady = true;
                cdgInventory.Items.Refresh();

            }));

        }

        private void cdgInventory_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (HiddenRowDetailsProperties.Contains(e.PropertyName))
            {
                e.Cancel = true;
                return;
            }

            if (e.PropertyName != "CurrentCount")
                return;

            e.Column = new DataGridTemplateColumn
            {
                Header = "Количество",
                CellTemplate = (DataTemplate)Application.Current.Resources["CurrentCountTemplate"],
                SortMemberPath = "CurrentCount",
                CanUserSort = true
            };

        }

        private void DetailsDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var innerGrid = sender as DependencyObject;
            var scrollViewer = FindVisualChild<ScrollViewer>(innerGrid);

            var atTop = scrollViewer == null || scrollViewer.VerticalOffset <= 0;
            var atBottom = scrollViewer == null || scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight;
            var shouldBubble = scrollViewer == null || scrollViewer.ScrollableHeight <= 0 || (e.Delta > 0 && atTop) || (e.Delta < 0 && atBottom);

            if (!shouldBubble)
                return;

            e.Handled = true;
            var bubbleArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };
            var parent = VisualTreeHelper.GetParent(innerGrid) as UIElement ?? cdgInventory;
            parent?.RaiseEvent(bubbleArgs);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    return typed;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
