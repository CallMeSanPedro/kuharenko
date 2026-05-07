namespace WpfFeatureFlagsDemo.Models;

public sealed class FeatureFlagsConfig
{
    public string CustomerCode { get; init; } = "default";

    public Dictionary<string, bool> Flags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
