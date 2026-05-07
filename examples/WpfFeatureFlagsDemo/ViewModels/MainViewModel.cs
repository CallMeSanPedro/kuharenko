using WpfFeatureFlagsDemo.Services;

namespace WpfFeatureFlagsDemo.ViewModels;

public sealed class MainViewModel
{
    public string WindowTitle { get; }

    public string HintText { get; }

    public bool IsOrdersTabVisible { get; }

    public bool IsReportsTabVisible { get; }

    public bool IsAdminTabVisible { get; }

    private MainViewModel(IFeatureFlagsService featureFlags, string customerCode)
    {
        WindowTitle = $"WPF Feature Toggle Demo ({customerCode})";
        HintText = "Это примитив только в клиенте: вкладки скрываются в UI, но бизнес-логику дополнительно проверяйте в командах/сервисах.";

        // Базовую вкладку обычно лучше оставлять всегда доступной.
        IsOrdersTabVisible = featureFlags.IsEnabled("Tab.Orders", defaultValue: true);
        IsReportsTabVisible = featureFlags.IsEnabled("Tab.Reports");
        IsAdminTabVisible = featureFlags.IsEnabled("Tab.Admin");
    }

    public static MainViewModel CreateDefault()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "features.customer-a.json");
        JsonFeatureFlagsService flags = JsonFeatureFlagsService.FromFile(configPath);
        return new MainViewModel(flags, flags.CustomerCode);
    }
}
