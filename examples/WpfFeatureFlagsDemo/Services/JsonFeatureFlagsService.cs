using System.Text.Json;
using WpfFeatureFlagsDemo.Models;

namespace WpfFeatureFlagsDemo.Services;

public sealed class JsonFeatureFlagsService : IFeatureFlagsService
{
    private readonly Dictionary<string, bool> _flags;

    public JsonFeatureFlagsService(Dictionary<string, bool> flags)
    {
        _flags = flags;
    }

    public string CustomerCode { get; private init; } = "default";

    public static JsonFeatureFlagsService FromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonFeatureFlagsService(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
        }

        string json = File.ReadAllText(path);
        FeatureFlagsConfig? config = JsonSerializer.Deserialize<FeatureFlagsConfig>(json);

        Dictionary<string, bool> flags = config?.Flags is null
            ? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, bool>(config.Flags, StringComparer.OrdinalIgnoreCase);

        return new JsonFeatureFlagsService(flags)
        {
            CustomerCode = config?.CustomerCode ?? "default",
        };
    }

    public bool IsEnabled(string featureKey, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return defaultValue;
        }

        return _flags.TryGetValue(featureKey, out bool value) ? value : defaultValue;
    }
}
