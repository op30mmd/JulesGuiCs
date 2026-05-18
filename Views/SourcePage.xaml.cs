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

            var titleBox = new TextBox { Header = "Session Title (Optional)", PlaceholderText = "e.g. Fix login bug" };
            var promptBox = new TextBox { Header = "Goal / Prompt", PlaceholderText = "What should Jules do?", AcceptsReturn = true, Height = 100 };
            var branchBox = new TextBox { Header = "Starting Branch", PlaceholderText = "main" };
            var approvalCheck = new CheckBox { Content = "Require Plan Approval" };
            var prCheck = new CheckBox { Content = "Auto-Create Pull Request" };

            var binding1 = new Microsoft.UI.Xaml.Data.Binding { Path = new Microsoft.UI.Xaml.Data.PropertyPath(nameof(ViewModel.NewSessionTitle)), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, Source = ViewModel };
            titleBox.SetBinding(TextBox.TextProperty, binding1);
            var binding2 = new Microsoft.UI.Xaml.Data.Binding { Path = new Microsoft.UI.Xaml.Data.PropertyPath(nameof(ViewModel.NewSessionPrompt)), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, Source = ViewModel };
            promptBox.SetBinding(TextBox.TextProperty, binding2);
            var binding3 = new Microsoft.UI.Xaml.Data.Binding { Path = new Microsoft.UI.Xaml.Data.PropertyPath(nameof(ViewModel.NewSessionBranch)), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, Source = ViewModel };
            branchBox.SetBinding(TextBox.TextProperty, binding3);
            var binding4 = new Microsoft.UI.Xaml.Data.Binding { Path = new Microsoft.UI.Xaml.Data.PropertyPath(nameof(ViewModel.RequirePlanApproval)), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, Source = ViewModel };
            approvalCheck.SetBinding(CheckBox.IsCheckedProperty, binding4);
            var binding5 = new Microsoft.UI.Xaml.Data.Binding { Path = new Microsoft.UI.Xaml.Data.PropertyPath(nameof(ViewModel.AutoCreatePR)), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, Source = ViewModel };
            prCheck.SetBinding(CheckBox.IsCheckedProperty, binding5);

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Create New Session",
                PrimaryButtonText = "Create",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = new StackPanel { Spacing = 12, Width = 400, Children = { titleBox, promptBox, branchBox, approvalCheck, prCheck } }
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                bool success = await ViewModel.CreateSessionAsync(source);
                if (success && this.Parent is Frame parentFrame)
                {
                    parentFrame.Navigate(typeof(SessionsPage));
                }
            }
        }
    }
}
