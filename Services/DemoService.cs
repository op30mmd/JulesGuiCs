namespace JulesClient.Services;

public class DemoService : IDemoService
{
    private readonly ISettingsService _settings;

    public DemoService(ISettingsService settings)
    {
        _settings = settings;
    }

    public bool IsDemoMode
    {
        get => _settings.IsDemoMode;
        set => _settings.IsDemoMode = value;
    }
}
