using Microsoft.UI.Xaml.Controls;
using JulesClient.ViewModels;
using JulesClient.Models;
using Microsoft.UI.Xaml;

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
            ViewModel.NewSessionTitle = $"Session with {source.GitHubRepo?.Repo ?? "Repository"}";
            ViewModel.NewSessionPrompt = "";

            var titleBox = new TextBox { Header = "Session Title (Optional)", PlaceholderText = "e.g. Fix login bug", Text = ViewModel.NewSessionTitle };
            var promptBox = new TextBox { Header = "Goal / Prompt", PlaceholderText = "What should Jules do?", AcceptsReturn = true, Height = 100, Text = ViewModel.NewSessionPrompt };
            var approvalCheck = new CheckBox { Content = "Require Plan Approval", IsChecked = ViewModel.RequirePlanApproval };
            var prCheck = new CheckBox { Content = "Auto-Create Pull Request", IsChecked = ViewModel.AutoCreatePR };

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Create New Session",
                PrimaryButtonText = "Create",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = new StackPanel { Spacing = 12, Width = 400, Children = { titleBox, promptBox, approvalCheck, prCheck } }
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.NewSessionTitle = titleBox.Text;
                ViewModel.NewSessionPrompt = promptBox.Text;
                ViewModel.RequirePlanApproval = approvalCheck.IsChecked == true;
                ViewModel.AutoCreatePR = prCheck.IsChecked == true;

                bool success = await ViewModel.CreateSessionAsync(source);
                if (success && this.Parent is Frame parentFrame)
                {
                    parentFrame.Navigate(typeof(SessionsPage));
                }
            }
        }
    }
}
