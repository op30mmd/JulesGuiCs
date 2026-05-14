using Windows.Storage;

namespace JulesClient.Services;

public interface ISettingsService
{
    string ApiKey { get; set; }
    bool ProxyEnabled { get; set; }
    string ProxyHost { get; set; }
    int ProxyPort { get; set; }
    string ProxyUsername { get; set; }
    string ProxyPassword { get; set; }
}

public class SettingsService : ISettingsService
{
    private const string ApiKeySetting = "ApiKey";
    private const string ProxyEnabledSetting = "ProxyEnabled";
    private const string ProxyHostSetting = "ProxyHost";
    private const string ProxyPortSetting = "ProxyPort";
    private const string ProxyUsernameSetting = "ProxyUsername";
    private const string ProxyPasswordSetting = "ProxyPassword";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public string ApiKey
    {
        get => _localSettings.Values[ApiKeySetting] as string ?? string.Empty;
        set => _localSettings.Values[ApiKeySetting] = value;
    }

    public bool ProxyEnabled
    {
        get => _localSettings.Values[ProxyEnabledSetting] as bool? ?? false;
        set => _localSettings.Values[ProxyEnabledSetting] = value;
    }

    public string ProxyHost
    {
        get => _localSettings.Values[ProxyHostSetting] as string ?? string.Empty;
        set => _localSettings.Values[ProxyHostSetting] = value;
    }

    public int ProxyPort
    {
        get => _localSettings.Values[ProxyPortSetting] as int? ?? 1080;
        set => _localSettings.Values[ProxyPortSetting] = value;
    }

    public string ProxyUsername
    {
        get => _localSettings.Values[ProxyUsernameSetting] as string ?? string.Empty;
        set => _localSettings.Values[ProxyUsernameSetting] = value;
    }

    public string ProxyPassword
    {
        get => _localSettings.Values[ProxyPasswordSetting] as string ?? string.Empty;
        set => _localSettings.Values[ProxyPasswordSetting] = value;
    }
}
