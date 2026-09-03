using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Input;
using JulesClient.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.System;
using Windows.UI.Core;

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
        // Only a real new message should move the view. The Remove/Add pair used
        // to swap a local echo for the confirmed activity must not, and - more
        // importantly - an incoming message must not yank the viewport while the
        // user has scrolled up to read earlier history. Auto-scroll only when the
        // chat is already parked at (or near) the newest message; otherwise the
        // floating "jump to latest" button is the affordance.
        bool added = e.Action is NotifyCollectionChangedAction.Add
                               or NotifyCollectionChangedAction.Reset;

        if (added && JulesClient.Services.AppSettings.AutoScrollChat
            && ChatListView.Items.Count > 0 && IsChatNearBottom())
        {
            _ = DispatcherQueue.TryEnqueue(ScrollChatToBottom);
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            HookChatScroller();
            UpdateJumpToBottom();
        });
    }

    // True while the chat is scrolled to (or within JumpThreshold of) its newest
    // message, or before its ScrollViewer has been realized - the states in
    // which appending a message should keep the newest one in view.
    private bool IsChatNearBottom()
    {
        if (_chatScroller == null)
        {
            HookChatScroller();
        }

        return _chatScroller == null
               || _chatScroller.ScrollableHeight - _chatScroller.VerticalOffset <= JumpThreshold;
    }

    private void ScrollChatToBottom()
    {
        if (ChatListView.Items.Count == 0)
        {
            return;
        }

        // ScrollIntoView reliably reveals the just-added item even before the
        // ListView has finished measuring it. ChangeView to the current
        // ScrollableHeight would stop one message short each time (the new item
        // is not in the extent yet), so the view slowly drifts off the bottom
        // and auto-scroll appears to stop working.
        ChatListView.ScrollIntoView(ChatListView.Items[^1]);
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

        // Drive opacity (animated by the button's OpacityTransition) rather than
        // Visibility so the button fades out instead of popping.
        JumpToBottomButton.Opacity = show ? 1 : 0;
        JumpToBottomButton.IsHitTestVisible = show;
    }

    // Enter behaviour in the message box:
    //  - "Press Enter to send" on:  Enter sends, Shift+Enter inserts a newline.
    //  - "Press Enter to send" off: Enter inserts a newline, Ctrl+Enter sends.
    private void OnChatInputPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        bool shift = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        bool ctrl = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

        bool send = ctrl
                    || (JulesClient.Services.AppSettings.SendOnEnter && !shift);
        if (!send)
        {
            return; // let the TextBox insert the newline
        }

        e.Handled = true;
        if (ViewModel.SendMessageCommand.CanExecute(null))
        {
            ViewModel.SendMessageCommand.Execute(null);
        }
    }

    // Opens a code review in a modal dialog instead of an inline dropdown, so a
    // long review never reflows the chat.
    private async void OnOpenReviewDialog(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe
            || fe.Tag is not JulesClient.Models.Activity activity
            || XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = activity.ReviewDisplayTitle,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Content = new ContentControl
            {
                ContentTemplate = (DataTemplate)Resources["ReviewDialogTemplate"],
                Content = activity,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            }
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch
        {
            // ShowAsync throws if another ContentDialog is already open.
        }
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
