// ItemsViewModel<T, TFilter>
// Поле рядом с _itemsView. Репозиторий не вызывается.

public ColumnCheckFilterSet ColumnFilters { get; } = new ColumnCheckFilterSet();

// Сразу после создания _itemsView:
ColumnFilters.Bind(_itemsView);

// Существующий обработчик ICollectionView.Filter.
// ViewFilter остаётся текстовым поиском. Колонки проверяются следом.
private bool OnFilter(object obj)
{
    if (!(obj is T item))
        return false;

    if (_viewFilter != null && !_viewFilter(item))
        return false;

    return ColumnFilters.Passes(item);
}

// DataGrid.OnAutoGeneratingColumn, в ветке DataGridTextColumn, после SortMemberPath.
// DataContext грида — view model с ColumnFilters.
private void AttachColumnFilter(DataGridTextColumn column, string propertyName)
{
    var host = DataContext as IColumnCheckFilterHost;
    if (host == null || string.IsNullOrEmpty(propertyName))
        return;

    var title = column.Header as string ?? propertyName;
    column.Header = host.ColumnFilters.Get(propertyName, title).CreateHeader();
}
