using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using JulesClient.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;

namespace JulesClient.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; } = new();

    public SessionsPage()
    {
        this.InitializeComponent();
        ViewModel.Activities.CollectionChanged += OnActivitiesChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncChatDiffPanels();
    }

    private void OnActivitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (JulesClient.Services.AppSettings.AutoScrollChat && ChatListView.Items.Count > 0)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                ChatListView.ScrollIntoView(ChatListView.Items[^1]);
            });
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModel.AggregatePatch) or nameof(ViewModel.SelectedSession))
        {
            SyncChatDiffPanels();
        }
    }

    private void OnChatDiffSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        SyncChatDiffPanels();
    }

    // Shows/hides the Chat vs Diff panels from the SelectorBar, and hides the
    // Diff tab entirely when the session has no changeset.
    private void SyncChatDiffPanels()
    {
        bool diffAvailable = ViewModel.AggregatePatch != null;
        DiffTabItem.Visibility = diffAvailable ? Visibility.Visible : Visibility.Collapsed;

        bool diffSelected = ChatDiffSelector.SelectedItem == DiffTabItem;
        if (diffSelected && !diffAvailable)
        {
            ChatDiffSelector.SelectedItem = ChatTabItem;
            diffSelected = false;
        }

        ChatListView.Visibility = diffSelected ? Visibility.Collapsed : Visibility.Visible;
        DiffPanel.Visibility = diffSelected ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Sessions.Count == 0)
        {
            ViewModel.LoadSessionsCommand.Execute(null);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Cleanup();
        ViewModel.Activities.CollectionChanged -= OnActivitiesChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
