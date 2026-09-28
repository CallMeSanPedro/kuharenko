// using Store.ViewModels.Charts;
// ShareDiagram.cs — в сборку Store.ViewModels.
// Класс не знает про TechReserv: на вход сущность и два её свойства, имя и количество.

// Свойство колонки «На складе». Из «2 (1/1)» в количество попадает 2.
const string StoreQuantityProperty = "";

public ShareDiagram ModelDiagram { get; } = new ShareDiagram();

// Заменить существующую строку
// HeaderFilters.Changed += (s, e) => ItemsView?.Refresh();
HeaderFilters.Changed += (s, e) =>
{
    ItemsView?.Refresh();
    LoadModelDiagram();
};

// В конец существующего OnRefreshed, после HeaderFilters.Reload(Items):
LoadModelDiagram();

void LoadModelDiagram()
{
    // Свойства сущности по имени.
    ModelDiagram.Load(ItemsView, nameof(TechReserv.MarkacommName), StoreQuantityProperty);

    // Либо сами свойства:
    // ModelDiagram.Load(
    //     ItemsView.OfType<TechReserv>(),
    //     item => item.MarkacommName,
    //     item => item.<количество>);
}
