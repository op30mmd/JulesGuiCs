using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JulesClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JulesClient;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

        // Wire up navigation - THIS IS CRITICAL
        Nav.ItemInvoked += (s, e) =>
        {
            if (e.IsSettingsInvoked)
            {
                if (ContentFrame.Content?.GetType() != typeof(Views.SettingsPage))
                {
                    ContentFrame.Navigate(typeof(Views.SettingsPage));
                }
                return;
            }

            var tag = e.InvokedItemContainer?.Tag?.ToString();
            System.Diagnostics.Debug.WriteLine($"[NAV] Clicked: {tag}");

            Type? pageType = tag switch
            {
                "Sources" => typeof(Views.SourcesPage),
                "Sessions" => typeof(Views.SessionsPage),
                _ => null
            };

            if (pageType != null && ContentFrame.Content?.GetType() != pageType)
            {
                System.Diagnostics.Debug.WriteLine($"[NAV] Navigating to {pageType.Name}");
                ContentFrame.Navigate(pageType);
            }
        };

        // Load default page on startup
        var settings = ((App)Application.Current).Services.GetRequiredService<ISettingsService>();
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            ContentFrame.Navigate(typeof(Views.SettingsPage));
            System.Diagnostics.Debug.WriteLine("[NAV] No API key, navigating to SettingsPage");
        }
        else
        {
            ContentFrame.Navigate(typeof(Views.SourcesPage));
            System.Diagnostics.Debug.WriteLine("[NAV] Loaded default: SourcesPage");
        }
    }
}
