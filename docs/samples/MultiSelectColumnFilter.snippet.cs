// Куски для уже существующего фильтра экрана.
// DataGrid и ItemsViewModel не меняются.
// Имена Condition / ExploitationStatus замените на свойства своей строки.

public class ConditionOption : Entity
{
    public ConditionOption()
    {
        TrackChanges = false;
    }

    public string Name { get; set; }

    public override string ToString() => Name ?? string.Empty;
}

// В класс фильтра, рядом с остальными полями.
private IEnumerable<object> _conditions;
private HashSet<string> _conditionSet;

public IEnumerable<object> Conditions
{
    get { return _conditions; }
    set
    {
        _conditions = value;
        RebuildConditionSet();
        NotifyPropertyChanged(() => Conditions);
    }
}

private void RebuildConditionSet()
{
    if (_conditions == null || !_conditions.Any())
    {
        _conditionSet = null;
        return;
    }

    _conditionSet = new HashSet<string>(
        _conditions.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)),
        StringComparer.OrdinalIgnoreCase);
}

// Внутри Match, вместе с остальными проверками.
// Пустой набор не ограничивает строки.
if (_conditionSet != null && _conditionSet.Count > 0)
{
    if (!_conditionSet.Contains(item.Condition ?? string.Empty))
        return false;
}

// В Clear:
Conditions = null;

// В view model экрана.
private ObservableCollection<object> _conditionOptions = new ObservableCollection<object>();
public ObservableCollection<object> ConditionOptions
{
    get { return _conditionOptions; }
    set { SetValue(ref _conditionOptions, value, () => ConditionOptions); }
}

// Один предикат, как в каталоге номенклатуры.
ViewFilter += item => Filter.Match(item);
Filter.PropertyChanged += (s, e) => ItemsView?.Refresh();

protected override void OnRefreshed(EventArgs e)
{
    base.OnRefreshed(e);
    RebuildConditionOptions();
}

private void RebuildConditionOptions()
{
    var selected = new HashSet<string>(
        (Filter.Conditions ?? Enumerable.Empty<object>())
            .Select(x => x?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x)),
        StringComparer.OrdinalIgnoreCase);

    var options = (Items ?? Enumerable.Empty<TechReserv>())
        .Select(x => x.Condition)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .Select(name => new ConditionOption { Name = name })
        .ToList();

    ConditionOptions = new ObservableCollection<object>(options.Cast<object>());

    Filter.Conditions = selected.Count == 0
        ? null
        : options.Where(x => selected.Contains(x.Name)).Cast<object>().ToList();
}
