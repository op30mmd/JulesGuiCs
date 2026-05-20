using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using JulesClient.ViewModels;
using System.Collections.Specialized;

namespace JulesClient.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; } = new();

    public SessionsPage()
    {
        this.InitializeComponent();
        ViewModel.Activities.CollectionChanged += OnActivitiesChanged;
    }

    private void OnActivitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ChatListView.Items.Count > 0)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                ChatListView.ScrollIntoView(ChatListView.Items[^1]);
            });
        }
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
    }

    private void DiffFileExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.DataContext is DiffFileViewModel vm)
        {
            vm.LoadHunks();
        }
    }
}
