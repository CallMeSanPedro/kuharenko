// Модель экрана: class TechReservViewModel : ItemsViewModel<...>, IColumnHeaderFilterHost
// Базовые ItemsViewModel и DataGrid не меняются.
// В шапку попадают только колонки, которые сюда добавлены.
// Имена свойств замените на фактические: это значение SortMemberPath, его базовый грид
// ставит равным имени свойства в OnAutoGeneratingColumn.

public ColumnHeaderFilterSet HeaderFilters { get; } = new ColumnHeaderFilterSet();

public ColumnHeaderFilter Find(string propertyName) => HeaderFilters.Find(propertyName);

public TechReservViewModel()
    : base(MainViewModel.Resolve<IRepository<TechReserv, TechReservFilter>>())
{
    HeaderFilters.Add(nameof(TechReserv.MarkacommName), "Модель", item => ((TechReserv)item).MarkacommName);
    HeaderFilters.AddText(nameof(TechReserv.SerialNumber), "Серийный номер", item => ((TechReserv)item).SerialNumber);
    HeaderFilters.Changed += (s, e) => ItemsView?.Refresh();

    // Одна лямбда. Второй ViewFilter += затрёт результат первой.
    ViewFilter += item => Filter.Match(item) && HeaderFilters.Passes(item);
}

protected override void OnRefreshed(EventArgs e)
{
    base.OnRefreshed(e);
    HeaderFilters.Reload(Items);
}
