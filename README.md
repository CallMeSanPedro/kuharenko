# kuharenko

## WPF feature flags demo

В репозиторий добавлен минимальный пример примитивного "конструктора" только на стороне клиента:

- путь: `examples/WpfFeatureFlagsDemo`
- назначение: скрывать/показывать отдельные вкладки по feature-флагам из JSON

### Что внутри

- `Services/IFeatureFlagsService.cs` — контракт для проверки фич
- `Services/JsonFeatureFlagsService.cs` — загрузка флагов из файла
- `ViewModels/MainViewModel.cs` — флаги для вкладок
- `MainWindow.xaml` — вкладки с `Visibility` через binding
- `Configuration/features.customer-*.json` — примеры конфигов заказчиков

### Как использовать в своем WPF клиенте

1. Добавьте `IFeatureFlagsService` и реализацию чтения конфигурации.
2. В `ViewModel` заведите bool-свойства видимости вкладок.
3. Привяжите `TabItem.Visibility` к этим свойствам через `BooleanToVisibilityConverter`.
4. Для разных заказчиков подгружайте свой JSON (или позже замените источник на Oracle/API).

> Важно: это только UI-скрытие. Доступ к операциям нужно дополнительно проверять в командах/сервисном слое.
