using Windows.Storage;

namespace JulesClient.Services;

public interface ISettingsService
{
    string ApiKey { get; set; }
}

public class SettingsService : ISettingsService
{
    private const string ApiKeySetting = "ApiKey";
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public string ApiKey
    {
        get => _localSettings.Values[ApiKeySetting] as string ?? string.Empty;
        set => _localSettings.Values[ApiKeySetting] = value;
    }
}
