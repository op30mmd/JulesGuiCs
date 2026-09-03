using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using JulesClient.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;

namespace JulesClient.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; } = new();

    // The chat ListView's inner ScrollViewer, resolved once it is templated.
    private ScrollViewer? _chatScroller;
    private int _hookAttempts;

    // How far (px) from the bottom still counts as "at the bottom".
    private const double JumpThreshold = 160;

    public SessionsPage()
    {
        this.InitializeComponent();
        ViewModel.Activities.CollectionChanged += OnActivitiesChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ChatListView.Loaded += (_, _) => HookChatScroller();
        ChatListView.SizeChanged += (_, _) => UpdateJumpToBottom();
        Loaded += (_, _) => HookChatScroller();

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

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            HookChatScroller();
            UpdateJumpToBottom();
        });
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
        UpdateJumpToBottom();
    }

    private void OnChatListLoaded(object sender, RoutedEventArgs e) => HookChatScroller();

    // Finds (once) the ListView's built-in ScrollViewer and subscribes to its
    // scroll changes. The ScrollViewer part is often not in the visual tree yet
    // when Loaded fires, so this re-queues itself until the template is realized.
    private void HookChatScroller()
    {
        if (_chatScroller != null)
        {
            return;
        }

        _chatScroller = FindDescendant<ScrollViewer>(ChatListView);
        if (_chatScroller == null)
        {
            if (_hookAttempts++ < 20)
            {
                _ = DispatcherQueue.TryEnqueue(HookChatScroller);
            }
            return;
        }

        _chatScroller.ViewChanged += (_, _) => UpdateJumpToBottom();
        UpdateJumpToBottom();
    }

    // The button is visible only on the Chat tab while the list is scrolled
    // more than JumpThreshold px above its bottom.
    private void UpdateJumpToBottom()
    {
        if (_chatScroller == null)
        {
            HookChatScroller();
        }

        bool show = ChatListView.Visibility == Visibility.Visible
                    && _chatScroller != null
                    && _chatScroller.ScrollableHeight - _chatScroller.VerticalOffset > JumpThreshold;

        JumpToBottomButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnJumpToBottomClick(object sender, RoutedEventArgs e)
    {
        if (_chatScroller != null)
        {
            _chatScroller.ChangeView(null, _chatScroller.ScrollableHeight, null);
        }
        else if (ChatListView.Items.Count > 0)
        {
            ChatListView.ScrollIntoView(ChatListView.Items[^1]);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var deeper = FindDescendant<T>(child);
            if (deeper != null)
            {
                return deeper;
            }
        }
        return null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        HookChatScroller();
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
