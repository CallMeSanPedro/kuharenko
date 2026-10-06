// StoreDeviceViewModel. Имена в AddServerText — это SortMemberPath колонки.
// Запрос уходит только из Changed: кнопка «Найти», Enter и крестик чипа.
// Ввод в поле сам по себе грид и базу не трогает.

public ColumnHeaderFilterSet HeaderFilters { get; } = new ColumnHeaderFilterSet();

private void RegisterHeaderFilters()
{
    HeaderFilters.AddServerText(
        "MarkacommName",
        "Модель",
        _ => string.Empty,
        value => Filter.EquipMarkaName = value);

    HeaderFilters.AddServerText(
        "ResponsibleName",
        "МОЛ",
        _ => string.Empty,
        value => Filter.ResponsibleName = value);

    // Последний аргумент — общая группа. «Найти» в одной колонке само снимает чипы остальных.
    // serialType и macType — элементы справочника, тот же тип, что у Filter.DeviceTypeNum.
    HeaderFilters.AddServerText("SerialNumber", "Серийный №", _ => string.Empty, value => ApplyNumber(value, serialType), "Number");
    HeaderFilters.AddServerText("Mac", "MAC", _ => string.Empty, value => ApplyNumber(value, macType), "Number");

    HeaderFilters.Changed += (s, e) =>
    {
        Filter.AcceptChanges();
        Refresh();
    };
}

private void ApplyNumber(string value, DeviceTypeNum type)
{
    if (string.IsNullOrEmpty(value))
    {
        if (Filter.DeviceTypeNum != null && Filter.DeviceTypeNum.TypnumKod == type.TypnumKod)
        {
            Filter.Number = null;
            Filter.DeviceTypeNum = null;
        }
        return;
    }

    Filter.Number = value;
    Filter.DeviceTypeNum = type;
}

// В FillFromBalanceRow, после записи в Filter и до Refresh:
// HeaderFilters.ShowApplied("MarkacommName", Filter.EquipMarkaName);
// HeaderFilters.ShowApplied("ResponsibleName", Filter.ResponsibleName);

// В ClearBalanceRow, после очистки полей Filter и до Refresh:
// HeaderFilters.ShowApplied("MarkacommName", null);
// HeaderFilters.ShowApplied("ResponsibleName", null);
