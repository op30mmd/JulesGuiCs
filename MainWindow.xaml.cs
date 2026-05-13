using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JulesClient;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wire up navigation - THIS IS CRITICAL
        Nav.ItemInvoked += (s, e) =>
        {
            var tag = e.InvokedItemContainer?.Tag?.ToString();
            System.Diagnostics.Debug.WriteLine($"[NAV] Clicked: {tag}");

            Type? pageType = tag switch
            {
                "Sources" => typeof(Views.SourcesPage),
                "Sessions" => typeof(Views.SessionsPage),
                "Settings" => typeof(Views.SettingsPage),
                _ => null
            };

            if (pageType != null && ContentFrame.Content?.GetType() != pageType)
            {
                System.Diagnostics.Debug.WriteLine($"[NAV] Navigating to {pageType.Name}");
                ContentFrame.Navigate(pageType);
            }
        };

        // Load default page on startup
        ContentFrame.Navigate(typeof(Views.SourcesPage));
        System.Diagnostics.Debug.WriteLine("[NAV] Loaded default: SourcesPage");
    }
}
