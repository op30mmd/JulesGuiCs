namespace JulesClient.Services;

public enum ProxyMode
{
    None,
    Manual,
    System
}

public enum BandwidthMode
{
    None,
    Manual,
    Auto
}

public interface ISettingsService
{
    string ApiKey { get; set; }
    ProxyMode ProxyMode { get; set; }
    bool ProxyEnabled { get; set; } // Kept for transition logic
    string ProxyHost { get; set; }
    int ProxyPort { get; set; }
    string ProxyUsername { get; set; }
    string ProxyPassword { get; set; }
    bool ProxyBypassLocal { get; set; }
    bool IsDemoMode { get; set; }
    BandwidthMode BandwidthMode { get; set; }
    bool BandwidthSavingEnabled { get; set; }
}

public class SettingsService : ISettingsService
{
#if WINDOWS
    private const string ApiKeySetting = "ApiKey";
    private const string ProxyModeSetting = "ProxyMode";
    private const string ProxyEnabledSetting = "ProxyEnabled";
    private const string ProxyHostSetting = "ProxyHost";
    private const string ProxyPortSetting = "ProxyPort";
    private const string ProxyUsernameSetting = "ProxyUsername";
    private const string ProxyPasswordSetting = "ProxyPassword";
    private const string ProxyBypassLocalSetting = "ProxyBypassLocal";
    private const string IsDemoModeSetting = "IsDemoMode";
    private const string BandwidthModeSetting = "BandwidthMode";
    private const string BandwidthSavingEnabledSetting = "BandwidthSavingEnabled";

    private readonly Windows.Storage.ApplicationDataContainer _localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

    public string ApiKey
    {
        get => _localSettings.Values[ApiKeySetting] as string ?? string.Empty;
        set => _localSettings.Values[ApiKeySetting] = value;
    }

    public ProxyMode ProxyMode
    {
        get
        {
            var val = _localSettings.Values[ProxyModeSetting] as int?;
            if (val == null)
            {
                // Transition logic: if ProxyEnabled was true, default to Manual
                return ProxyEnabled ? ProxyMode.Manual : ProxyMode.None;
            }
            return (ProxyMode)val.Value;
        }
        set => _localSettings.Values[ProxyModeSetting] = (int)value;
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

    public bool ProxyBypassLocal
    {
        get => _localSettings.Values[ProxyBypassLocalSetting] as bool? ?? true;
        set => _localSettings.Values[ProxyBypassLocalSetting] = value;
    }

    public bool IsDemoMode
    {
        get => _localSettings.Values[IsDemoModeSetting] as bool? ?? false;
        set => _localSettings.Values[IsDemoModeSetting] = value;
    }

    public BandwidthMode BandwidthMode
    {
        get => (BandwidthMode)(_localSettings.Values[BandwidthModeSetting] as int? ?? (int)BandwidthMode.None);
        set => _localSettings.Values[BandwidthModeSetting] = (int)value;
    }

    public bool BandwidthSavingEnabled
    {
        get => _localSettings.Values[BandwidthSavingEnabledSetting] as bool? ?? false;
        set => _localSettings.Values[BandwidthSavingEnabledSetting] = value;
    }
#else
    public string ApiKey { get; set; } = string.Empty;
    public ProxyMode ProxyMode { get; set; } = ProxyMode.None;
    public bool ProxyEnabled { get; set; }
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; } = 1080;
    public string ProxyUsername { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;
    public bool ProxyBypassLocal { get; set; } = true;
    public bool IsDemoMode { get; set; } = false;
    public BandwidthMode BandwidthMode { get; set; } = BandwidthMode.None;
    public bool BandwidthSavingEnabled { get; set; } = false;
#endif
}
