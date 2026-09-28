// Добавки к Store.ViewModels.Device.TechnoReserv.TechReservViewModel.
// ItemsViewModel, фильтр колонок и привилегии не меняются.
// ModelShare.cs положить в сборку Store.ViewModels.
// Перед сборкой вписать имя свойства колонки «На складе» в ModelShareBuilder.OnStorePropertyName.

public ObservableCollection<ModelShare> ModelShares { get; } = new ObservableCollection<ModelShare>();

// Заменить существующую строку
// HeaderFilters.Changed += (s, e) => ItemsView?.Refresh();
HeaderFilters.Changed += (s, e) =>
{
    ItemsView?.Refresh();
    ModelShareBuilder.Fill(ModelShares, ItemsView);
};

// Заменить существующий OnRefreshed целиком.
protected override void OnRefreshed(EventArgs e)
{
    base.OnRefreshed(e);
    HeaderFilters.Reload(Items);
    ModelShareBuilder.Fill(ModelShares, ItemsView);
}
