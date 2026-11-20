// ColumnHeaderFilter.cs лежит в проекте модели представления.
// Во View остаётся только ColumnCheckFilterBehavior.
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
    // Второй аргумент может быть ключом ресурса или русским ShortName.
    // Шапку всё равно переводит ключ колонки, его берёт ColumnCheckFilterBehavior.
    HeaderFilters.Add(nameof(TechReserv.MarkacommName), "TechReserv_MarkacommName", item => ((TechReserv)item).MarkacommName);
    HeaderFilters.AddText(nameof(TechReserv.SerialNumber), "TechReserv_SerialNumber", item => ((TechReserv)item).SerialNumber);
    // AddServerText не фильтрует уже загруженные строки: write кладёт текст в фильтр запроса.
    // Повторный вызов с тем же propertyName затирает предыдущий фильтр этого свойства.
    // Enter или кнопка «Найти» вызывают Apply. Общий exclusiveGroup оставляет один фильтр
    // и пишет null в write остальных.
    // HeaderFilters.AddServerText(nameof(StoreDevice.SerialNumber), "Серийный номер",
    //     item => ((StoreDevice)item).SerialNumber, value => Filter.SerialNumber = value, "identity");
    HeaderFilters.Changed += (s, e) => ItemsView?.Refresh();

    // Одна лямбда. Второй ViewFilter += затрёт результат первой.
    ViewFilter += item => Filter.Match(item) && HeaderFilters.Passes(item);
}

protected override void OnRefreshed(EventArgs e)
{
    base.OnRefreshed(e);
    HeaderFilters.Reload(Items);
}
