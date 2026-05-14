using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _proxyEnabled;

    [ObservableProperty]
    private string _proxyHost = string.Empty;

    [ObservableProperty]
    private double _proxyPort;

    [ObservableProperty]
    private string _proxyUsername = string.Empty;

    [ObservableProperty]
    private string _proxyPassword = string.Empty;

    public SettingsViewModel()
    {
        _settings = App.Current.Services.GetRequiredService<ISettingsService>();
        _apiKey = _settings.ApiKey;
        _proxyEnabled = _settings.ProxyEnabled;
        _proxyHost = _settings.ProxyHost;
        _proxyPort = _settings.ProxyPort;
        _proxyUsername = _settings.ProxyUsername;
        _proxyPassword = _settings.ProxyPassword;
    }

    public void Save()
    {
        _settings.ApiKey = ApiKey;
        _settings.ProxyEnabled = ProxyEnabled;
        _settings.ProxyHost = ProxyHost;
        _settings.ProxyPort = (int)ProxyPort;
        _settings.ProxyUsername = ProxyUsername;
        _settings.ProxyPassword = ProxyPassword;
    }
}
