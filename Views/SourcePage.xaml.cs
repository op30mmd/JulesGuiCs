using Microsoft.UI.Xaml;
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
        this.Loaded += (s, e) => { if (ViewModel.Sources.Count == 0) ViewModel.LoadSourcesCommand.Execute(null); };
    }

    private async void OnNewSessionClick(object sender, RoutedEventArgs e)
    {
        var source = (Source)((Button)sender).Tag;
        PromptTextBox.Text = string.Empty;
        CreateSessionDialog.XamlRoot = this.XamlRoot;
        var result = await CreateSessionDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var prompt = PromptTextBox.Text;
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                try
                {
                    var api = App.Current.Services.GetRequiredService<Services.IJulesApiClient>();
                    await api.CreateSessionAsync(new CreateSessionRequest(source.Name, prompt));
                    // Success, maybe navigate to sessions
                    Frame.Navigate(typeof(SessionsPage));
                }
                catch (Exception ex)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to create session: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }
    }
}
