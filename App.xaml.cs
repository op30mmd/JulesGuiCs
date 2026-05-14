using Microsoft.UI.Xaml;

namespace JulesClient;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public static MainWindow? MainWindow { get; private set; }
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Services.ISettingsService, Services.SettingsService>();
        services.AddSingleton<Services.IJulesApiClient, Services.JulesApiClient>();
        services.AddSingleton<Services.IPollingService, Services.PollingService>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
