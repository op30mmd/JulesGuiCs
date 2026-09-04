using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Input;
using JulesClient.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Foundation;
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

    // Whether the jump-to-bottom button is currently shown, so the transition to
    // hidden can be detected (see ResetJumpToBottomHoverState).
    private bool _jumpToBottomShown;

    // Session list pane sizing. _sessionPaneWidth is the width the user picked
    // with the splitter, and is kept even while the window is too narrow to
    // honour it so that widening the window brings it back.
    private const double SessionPaneMinWidth = 200;
    private const double SessionPaneMaxWidth = 560;
    private const double SessionPaneDefaultWidth = 300;
    private const double SessionPaneRailWidth = 48;

    private const int PaneAnimationMs = 160;

    private double _sessionPaneWidth = SessionPaneDefaultWidth;
    // The width actually written to the column, so the slide knows where it is
    // starting from and a no-op commit can be skipped.
    private double _appliedPaneWidth = SessionPaneDefaultWidth;
    private bool _sessionPaneCollapsed;
    private bool _splitterDragging;
    private double _splitterStartWidth;
    private double _splitterStartX;
    private EventHandler<object>? _paneAnimation;

    public SessionsPage()
    {
        this.InitializeComponent();
        ViewModel.Activities.CollectionChanged += OnActivitiesChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ChatListView.Loaded += (_, _) => HookChatScroller();
        ChatListView.SizeChanged += (_, _) => UpdateJumpToBottom();
        Loaded += (_, _) => HookChatScroller();
        Loaded += (_, _) => UpdatePaneClip();
        SessionPane.SizeChanged += (_, _) => UpdatePaneClip();

        // A window resize can squeeze the pane, but not while the collapse slide
        // already owns the column width and both panes' sizes.
        SizeChanged += (_, _) =>
        {
            if (_paneAnimation == null)
            {
                CommitPaneWidth(TargetPaneWidth());
            }
        };

        // Render-thread fades, so collapsing the pane costs a layout pass per
        // frame for the width and nothing at all for the fade.
        SessionsHeading.OpacityTransition = NewPaneFade();
        SessionsRefreshButton.OpacityTransition = NewPaneFade();
        SessionListArea.OpacityTransition = NewPaneFade();

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

        if (_jumpToBottomShown && !show)
        {
            ResetJumpToBottomHoverState();
        }
        _jumpToBottomShown = show;
    }

    // Hiding the button while the pointer is still on it - which is exactly what
    // clicking it does, since the chat jumps to the bottom and the button fades
    // out from under the cursor - leaves it stuck in its PointerOver (or
    // Pressed) state: IsHitTestVisible="False" stops the matching PointerExited
    // from ever arriving, so the button keeps the hover look and is still
    // wearing it the next time it fades back in.
    //
    // Toggling IsEnabled is the public way to clear the button's cached
    // pointer/pressed flags; both writes happen in the same pass, so no frame is
    // ever drawn showing the disabled look.
    private void ResetJumpToBottomHoverState()
    {
        // Disabling the focused element makes the framework hand focus to
        // whatever it picks next, so park it on the message box deliberately -
        // the button is on its way out either way.
        if (JumpToBottomButton.FocusState != FocusState.Unfocused)
        {
            ChatInputBox.Focus(FocusState.Programmatic);
        }

        JumpToBottomButton.IsEnabled = false;
        JumpToBottomButton.IsEnabled = true;
    }

    private static ScalarTransition NewPaneFade() =>
        new() { Duration = TimeSpan.FromMilliseconds(110) };

    private void UpdatePaneClip() =>
        SessionPaneClip.Rect = new Rect(0, 0, SessionPane.ActualWidth, SessionPane.ActualHeight);

    // The widest the pane can be dragged to right now - capped so the chat keeps
    // a usable amount of room in a narrow window.
    private double MaxPaneWidth()
    {
        double roomFor = ActualWidth > 0
            ? Math.Max(SessionPaneMinWidth, ActualWidth - 420)
            : SessionPaneMaxWidth;
        return Math.Min(SessionPaneMaxWidth, roomFor);
    }

    private double ClampPaneWidth(double width) =>
        Math.Clamp(width, SessionPaneMinWidth, MaxPaneWidth());

    // The width the pane should occupy right now, given the collapse state, the
    // width the user picked and how much room the window can spare.
    private double TargetPaneWidth() =>
        _sessionPaneCollapsed ? SessionPaneRailWidth : ClampPaneWidth(_sessionPaneWidth);

    private void CommitPaneWidth(double width)
    {
        if (Math.Abs(width - _appliedPaneWidth) < 0.5)
        {
            return;
        }

        _appliedPaneWidth = width;
        SessionListColumn.Width = new GridLength(width);
    }

    // Runs a layout-changing pane update and the layout pass it forces with
    // anchoring held off. Resizing the pane re-wraps every expander in the chat
    // at once, and ScrollAnchor reads each of those height deltas as a reason to
    // scroll - which lands the reader somewhere arbitrary. Only safe to call from
    // outside a layout pass (a click or a pointer event, not SizeChanged).
    private void WithoutAnchoring(string phase, Action change)
    {
        JulesClient.Services.ScrollAnchor.IsSuspended = true;
        var clock = Stopwatch.StartNew();
        try
        {
            change();
            UpdateLayout();
        }
        finally
        {
            JulesClient.Services.ScrollAnchor.IsSuspended = false;
        }
        ReportLayoutCost(phase, clock.Elapsed.TotalMilliseconds);
    }

    // What one layout pass at a new width costs, and how much of the chat the
    // list is actually holding realised. "realized" against "items" is the
    // number that matters: a virtualising list should hold a screenful whatever
    // the history length, and if instead it tracks the item count then every
    // resize is paying for the whole conversation.
    private void ReportLayoutCost(string phase, double milliseconds)
    {
        if (!JulesClient.Services.AppSettings.VerboseLogging)
        {
            ResizeDiagnostics.Visibility = Visibility.Collapsed;
            return;
        }

        ResizeDiagnosticsText.Text =
            $"{phase} {milliseconds:0} ms"
            + $" | sessions {DescribeList(SessionsListView)}"
            + $" | chat {DescribeList(ChatListView)}";
        ResizeDiagnostics.Visibility = Visibility.Visible;
        Debug.WriteLine("[RESIZE] " + ResizeDiagnosticsText.Text);
    }

    private static string DescribeList(ListView list) =>
        list.ItemsPanelRoot is ItemsStackPanel panel
            ? $"{list.Items.Count} items, {panel.LastCacheIndex - panel.FirstCacheIndex + 1} realised,"
              + $" {panel.LastVisibleIndex - panel.FirstVisibleIndex + 1} visible"
            : $"{list.Items.Count} items, panel {list.ItemsPanelRoot?.GetType().Name ?? "none"}";

    // An element given an explicit Width while its HorizontalAlignment is
    // Stretch gets *centred* in a slot narrower than that width, not
    // left-aligned. That is what put a pinned pane in the middle of its column,
    // offset by half the overflow, instead of filling from its left edge - so
    // the alignment has to be pinned along with the width, and handed back with
    // it.
    private static void PinWidth(FrameworkElement element, double width)
    {
        element.HorizontalAlignment = HorizontalAlignment.Left;
        element.Width = width;
    }

    private static void UnpinWidth(FrameworkElement element)
    {
        element.ClearValue(WidthProperty);
        element.ClearValue(HorizontalAlignmentProperty);
    }

    // Collapse/expand the session list. Everything but the toggle button fades
    // out and the column animates down to a rail, so the button - and with it the
    // way back - stays on screen whether or not a session is open.
    private void OnToggleSessionPane(object sender, RoutedEventArgs e)
    {
        _sessionPaneCollapsed = !_sessionPaneCollapsed;

        // ChevronRight when collapsed (opens), ChevronLeft when open (closes).
        SessionPaneToggleIcon.Glyph = _sessionPaneCollapsed ? "\uE76C" : "\uE76B";
        ToolTipService.SetToolTip(
            SessionPaneToggle, _sessionPaneCollapsed ? "Show session list" : "Hide session list");

        SessionPaneSplitter.Visibility =
            _sessionPaneCollapsed ? Visibility.Collapsed : Visibility.Visible;

        if (!_sessionPaneCollapsed)
        {
            // Live again before the column grows, so it fades in as it widens.
            SetPaneContentActive(true);
        }

        // OpacityTransition animates this on the render thread - no layout.
        SetPaneContentOpacity(_sessionPaneCollapsed ? 0 : 1);

        AnimatePaneWidth(TargetPaneWidth(), () =>
        {
            if (_sessionPaneCollapsed)
            {
                // Only at the end: a faded-out list is still clickable.
                SetPaneContentActive(false);
            }
        });
    }

    // Nothing in WinUI animates a GridLength, so the column is stepped a frame at
    // a time. That means a real layout pass per frame, which is why the session
    // list is frozen for the duration exactly as it is during a drag - its rows
    // are the expensive part of that pass - and why the animation is short.
    //
    // An earlier version slid the chat pane with a render-thread transform
    // instead, for zero layout. A transform can only ever be right for content
    // aligned to the pane's leading edge: anything centred in the pane (the
    // "Select a session to start" placeholder, the loading spinner) has to
    // re-centre as the pane resizes, and a transform cannot do that. It jumped by
    // half the width delta - at the end of the animation when the pane's width
    // was pinned, at the start when it wasn't. Only really resizing the column
    // puts that content where it belongs on every frame.
    private void AnimatePaneWidth(double to, Action? onCompleted = null)
    {
        StopPaneAnimation();

        double from = _appliedPaneWidth;
        if (Math.Abs(to - from) < 0.5 || ActualWidth <= 0)
        {
            WithoutAnchoring("toggle", () => CommitPaneWidth(to));
            onCompleted?.Invoke();
            return;
        }

        JulesClient.Services.ScrollAnchor.IsSuspended = true;
        PinWidth(SessionListArea, MaxPaneWidth());

        var clock = Stopwatch.StartNew();
        _paneAnimation = (_, _) =>
        {
            double t = Math.Clamp(clock.Elapsed.TotalMilliseconds / PaneAnimationMs, 0, 1);
            double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            CommitPaneWidth(from + ((to - from) * eased));

            if (t >= 1)
            {
                StopPaneAnimation();
                onCompleted?.Invoke();
            }
        };
        CompositionTarget.Rendering += _paneAnimation;
    }

    private void StopPaneAnimation()
    {
        if (_paneAnimation == null)
        {
            return;
        }

        CompositionTarget.Rendering -= _paneAnimation;
        _paneAnimation = null;

        var clock = Stopwatch.StartNew();
        CommitPaneWidth(TargetPaneWidth());
        UnpinWidth(SessionListArea);
        UpdateLayout();
        ReportLayoutCost("toggle-end", clock.Elapsed.TotalMilliseconds);

        // A drag that interrupted the animation still needs anchoring off.
        JulesClient.Services.ScrollAnchor.IsSuspended = _splitterDragging;
    }

    // The list is genuinely taken out of layout - it is large and would still be
    // clickable if it were merely transparent. The heading and the refresh button
    // are only taken out of the input path: collapsing them changes the header
    // row's height, because the heading is the tallest thing in that Auto row,
    // and the toggle button is centred in it - which made the button hop upwards
    // every time the pane closed. They stay measured, so the row height is fixed,
    // and the opacity fade is what actually hides them. In the rail they end up
    // pushed past its right edge and clipped away.
    private void SetPaneContentActive(bool active)
    {
        SessionsHeading.IsHitTestVisible = active;
        SessionsRefreshButton.IsHitTestVisible = active;
        SessionsRefreshButton.IsTabStop = active;
        SessionListArea.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetPaneContentOpacity(double opacity)
    {
        SessionsHeading.Opacity = opacity;
        SessionsRefreshButton.Opacity = opacity;
        SessionListArea.Opacity = opacity;
    }

    private void OnSessionSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        StopPaneAnimation();

        _splitterStartX = e.GetCurrentPoint(this).Position.X;
        _splitterStartWidth = _appliedPaneWidth;
        _splitterDragging = SessionPaneSplitter.CapturePointer(e.Pointer);

        if (_splitterDragging)
        {
            // Anchoring off for the whole drag, so a resize doesn't scroll the
            // chat as the items in it re-wrap.
            JulesClient.Services.ScrollAnchor.IsSuspended = true;

            // One full layout of this page measures 29-41 ms, which is fine as a
            // one-off but far too slow to repeat on every frame of a drag. So the
            // list's measure width is frozen at the widest the pane can reach:
            // its rows are laid out once here and then only re-clipped as the
            // pane moves. This is the same pin that used to strand the list in
            // the middle of the pane - it behaves now that PinWidth sets the
            // alignment along with the width.
            var clock = Stopwatch.StartNew();
            PinWidth(SessionListArea, MaxPaneWidth());
            UpdateLayout();
            ReportLayoutCost("drag-start", clock.Elapsed.TotalMilliseconds);
        }

        // Deliberately not marking the pointer events handled: doing so stops the
        // gesture recogniser, and with it the double-click that resets the width.
    }

    private void OnSessionSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_splitterDragging)
        {
            return;
        }

        double delta = e.GetCurrentPoint(this).Position.X - _splitterStartX;
        _sessionPaneWidth = ClampPaneWidth(_splitterStartWidth + delta);
        CommitPaneWidth(TargetPaneWidth());
    }

    private void OnSessionSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_splitterDragging)
        {
            return;
        }

        SessionPaneSplitter.ReleasePointerCapture(e.Pointer);
        EndSplitterDrag();
    }

    private void OnSessionSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e) =>
        EndSplitterDrag();

    private void EndSplitterDrag()
    {
        if (!_splitterDragging)
        {
            return;
        }

        _splitterDragging = false;
        // Hands the list back to layout and lets it settle at the real width
        // while anchoring is still off, so the re-wrap doesn't scroll the chat.
        var clock = Stopwatch.StartNew();
        UnpinWidth(SessionListArea);
        UpdateLayout();
        JulesClient.Services.ScrollAnchor.IsSuspended = false;
        ReportLayoutCost("drag-end", clock.Elapsed.TotalMilliseconds);
    }

    // Double-click the splitter to go back to the default width.
    private void OnSessionSplitterDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _sessionPaneWidth = SessionPaneDefaultWidth;
        AnimatePaneWidth(TargetPaneWidth());
        e.Handled = true;
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
