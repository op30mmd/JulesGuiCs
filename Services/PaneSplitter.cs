using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace JulesClient.Services;

// A bare drag strip for resizing a Grid column. WinUI 3 ships no GridSplitter,
// and the only way to give an element a resize cursor is UIElement.ProtectedCursor,
// which is protected - hence this subclass. Panel is the base because Border and
// the concrete panels are sealed, and because a Panel paints (and so hit-tests)
// its own Background without needing a template or any children. The drag
// handling itself lives with the pane that owns the splitter.
public sealed class PaneSplitter : Panel
{
    public PaneSplitter()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
