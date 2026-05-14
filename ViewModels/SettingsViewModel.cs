using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    public SettingsViewModel()
    {
        _settings = App.Current.Services.GetRequiredService<ISettingsService>();
        _apiKey = _settings.ApiKey;
    }

    public void Save()
    {
        _settings.ApiKey = ApiKey;
    }
}
