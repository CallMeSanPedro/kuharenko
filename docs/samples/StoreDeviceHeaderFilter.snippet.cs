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

    // CreateSelectCommand читает DeviceTypeNum.TypnumKod, когда Number не пустой.
    // Если тип номера не задан, перед записью Number его нужно выставить, иначе селектор упадёт.
    HeaderFilters.AddServerText(
        "Number",
        "Номер",
        _ => string.Empty,
        value => Filter.Number = value);

    HeaderFilters.Changed += (s, e) =>
    {
        Filter.AcceptChanges();
        Refresh();
    };
}

// В FillFromBalanceRow, после записи в Filter и до Refresh:
// HeaderFilters.ShowApplied("MarkacommName", Filter.EquipMarkaName);
// HeaderFilters.ShowApplied("ResponsibleName", Filter.ResponsibleName);

// В ClearBalanceRow, после очистки полей Filter и до Refresh:
// HeaderFilters.ShowApplied("MarkacommName", null);
// HeaderFilters.ShowApplied("ResponsibleName", null);
