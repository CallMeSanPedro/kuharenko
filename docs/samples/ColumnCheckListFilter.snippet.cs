// Только TechReservViewModel и разметка экрана.
// ItemsViewModel и DataGrid не изменяются.

public sealed class TechReservViewModel : ItemsViewModel<TechReserv, TechReservFilter>, IColumnCheckFilterHost
{
    public ColumnCheckFilterSet ColumnFilters { get; } = new ColumnCheckFilterSet();

    public TechReservViewModel()
        : base(MainViewModel.Resolve<IRepository<TechReserv, TechReservFilter>>())
    {
        // Один предикат. += здесь нельзя: у Predicate останется результат только последнего метода.
        ViewFilter = item =>
            (
                string.IsNullOrEmpty(LoweredFilterText) ||
                (item.ManufacturerName ?? string.Empty).ToLower().Contains(LoweredFilterText) ||
                (item.ModelName ?? string.Empty).ToLower().Contains(LoweredFilterText) ||
                (item.CategoryName ?? string.Empty).ToLower().Contains(LoweredFilterText)
            )
            && ColumnFilters.Passes(item);
    }
}

// В XAML грида, рядом с остальными атрибутами:
// xmlns:filtering="clr-namespace:Store.Views.Filtering"
//
// <controls2:DataGrid filtering:ColumnCheckFilterBehavior.IsEnabled="True" ... />
