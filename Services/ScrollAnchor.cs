using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace JulesClient.Services;

/// <summary>
/// Keeps the viewport visually stable when an <see cref="Expander"/> whose top
/// is at or above the top of the scrollable area is expanded or collapsed.
///
/// WinUI's own anchoring (<c>ScrollViewer.VerticalAnchorRatio</c> /
/// <c>UIElement.CanBeScrollAnchor</c>) doesn't help here: it isn't honoured
/// inside a <see cref="ListView"/> (the chat), and even in a plain
/// <c>ScrollViewer</c> it anchors on item add/remove, not on an item resizing
/// in place. So instead this watches the expander's <see cref="FrameworkElement.SizeChanged"/>
/// and, when the growth/shrink happened above the fold, shifts the scroll
/// offset by the same delta so the content the user is reading doesn't jump.
/// </summary>
public static class ScrollAnchor
{
    public static readonly DependencyProperty PreserveOnResizeProperty =
        DependencyProperty.RegisterAttached(
            "PreserveOnResize", typeof(bool), typeof(ScrollAnchor),
            new PropertyMetadata(false, OnPreserveOnResizeChanged));

    public static void SetPreserveOnResize(DependencyObject o, bool value) => o.SetValue(PreserveOnResizeProperty, value);
    public static bool GetPreserveOnResize(DependencyObject o) => (bool)o.GetValue(PreserveOnResizeProperty);

    /// <summary>
    /// Turns the height compensation below off while a layout change that is not
    /// an expand/collapse is under way - resizing the session list pane, say.
    /// Such a change re-wraps every realised item at once, so each watched
    /// expander reports a height delta, and compensating for all of them scrolls
    /// the chat to a meaningless place. Worse, every <c>ChangeView</c> re-scrolls
    /// the list and realises different items, which report their own deltas: on a
    /// pane drag that cascade runs on every frame and is what makes dragging feel
    /// laggy. Set by <c>SessionsPage</c> around the drag and the collapse.
    /// </summary>
    public static bool IsSuspended { get; set; }

    private static void OnPreserveOnResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        fe.SizeChanged -= OnSizeChanged;
        if (e.NewValue is true)
        {
            fe.SizeChanged += OnSizeChanged;
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsSuspended)
        {
            return;
        }

        // Ignore first layout and teardown - only genuine in-place resizes.
        if (e.PreviousSize.Height <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        double delta = e.NewSize.Height - e.PreviousSize.Height;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        if (sender is not FrameworkElement fe)
        {
            return;
        }

        var scroller = FindScrollViewer(fe);
        if (scroller == null)
        {
            return;
        }

        double top;
        try
        {
            top = fe.TransformToVisual(scroller).TransformPoint(new Point(0, 0)).Y;
        }
        catch
        {
            return;
        }

        // Only a change whose top edge is at/above the viewport top pushes the
        // visible content; a resize fully inside the viewport just grows
        // downward, which is the intended behaviour.
        if (top > 1)
        {
            return;
        }

        scroller.ChangeView(null, scroller.VerticalOffset + delta, null, disableAnimation: true);
    }

    // How close to the end still counts as "pinned to the bottom".
    private const double BottomAnchorTolerance = 24;

    /// <summary>
    /// Runs <paramref name="mutate"/> (a layout-changing action such as
    /// expanding a "Show more" block) and then keeps the reader's place:
    /// <list type="bullet">
    /// <item>if the view was pinned to the bottom, re-pins it to the bottom so
    /// the newly revealed content is followed;</item>
    /// <item>otherwise, if <paramref name="anchor"/> starts at or above the top
    /// of the viewport, compensates the scroll offset by the height delta so the
    /// content above the fold doesn't jump.</item>
    /// </list>
    /// When the anchor is fully in view and the view wasn't at the bottom, the
    /// content just reflows downward, which is what clicking to reveal expects.
    /// </summary>
    public static void PreserveDuring(FrameworkElement anchor, Action mutate)
    {
        var scroller = FindScrollViewer(anchor);
        if (scroller == null)
        {
            mutate();
            return;
        }

        double top;
        try
        {
            top = anchor.TransformToVisual(scroller).TransformPoint(new Point(0, 0)).Y;
        }
        catch
        {
            mutate();
            return;
        }

        double heightBefore = anchor.ActualHeight;
        double offsetBefore = scroller.VerticalOffset;
        bool wasAtBottom = scroller.ScrollableHeight - offsetBefore <= BottomAnchorTolerance;
        bool anchorTop = top <= 1;

        mutate();

        if (!wasAtBottom && !anchorTop)
        {
            return; // in view and not pinned to the bottom - let it reflow down
        }

        // The block's height is animated open/closed over ~200ms, so re-apply
        // the correction every rendered frame for a short window. Driving the
        // scroll from CompositionTarget.Rendering works where doing it from a
        // layout callback does not (ChangeView is dropped mid-layout).
        int frames = 0;
        EventHandler<object>? onRender = null;
        onRender = (_, _) =>
        {
            if (wasAtBottom)
            {
                scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
            }
            else
            {
                double delta = anchor.ActualHeight - heightBefore;
                scroller.ChangeView(null, Math.Max(0, offsetBefore + delta), null, disableAnimation: true);
            }

            if (++frames > FollowFrames)
            {
                CompositionTarget.Rendering -= onRender;
            }
        };
        CompositionTarget.Rendering += onRender;
    }

    // ~0.7s at 60fps - comfortably past the height animation plus settling.
    private const int FollowFrames = 42;

    private static ScrollViewer? FindScrollViewer(DependencyObject start)
    {
        var node = VisualTreeHelper.GetParent(start);
        while (node != null)
        {
            if (node is ScrollViewer sv)
            {
                return sv;
            }
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
