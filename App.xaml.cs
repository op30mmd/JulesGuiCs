using Microsoft.UI.Xaml;

namespace JulesClient;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
