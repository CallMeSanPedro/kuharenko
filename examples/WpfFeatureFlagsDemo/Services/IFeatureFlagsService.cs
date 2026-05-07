namespace WpfFeatureFlagsDemo.Services;

public interface IFeatureFlagsService
{
    bool IsEnabled(string featureKey, bool defaultValue = false);
}
