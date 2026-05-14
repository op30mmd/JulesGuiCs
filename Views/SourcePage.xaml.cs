using Microsoft.UI.Xaml.Controls;
using JulesClient.ViewModels;
using JulesClient.Models;

namespace JulesClient.Views;

public sealed partial class SourcesPage : Page
{
    public SourcesViewModel ViewModel { get; } = new();

    public SourcesPage()
    {
        this.InitializeComponent();
        this.Loaded += (s, e) => _ = ViewModel.LoadSourcesAsync();
    }

    private void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        _ = ViewModel.LoadSourcesAsync();
    }

    private async void OnSourceClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Source source)
        {
            ViewModel.NewSessionTitle = $"Session with {source.GitHubRepo?.Repo}";
            ViewModel.NewSessionPrompt = "";

            var result = await CreateSessionDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                bool success = await ViewModel.CreateSessionAsync(source);
                if (success)
                {
                    // Navigate to Sessions
                    Frame.Navigate(typeof(SessionsPage));
                }
            }
        }
    }
}
