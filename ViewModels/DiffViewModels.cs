using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class DiffFileViewModel : ObservableObject
{
    public DiffFileNode Node { get; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private ObservableCollection<DiffHunkViewModel> _hunks = new();

    public DiffFileViewModel(DiffFileNode node)
    {
        Node = node;
        _isExpanded = false;
    }

    public string DisplayName => Node.DisplayName;
    public string Stats => Node.Stats;
    public int TotalLines => Node.TotalLines;

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
        if (IsExpanded && Hunks.Count == 0)
        {
            foreach (var hunk in Node.File.Hunks)
            {
                Hunks.Add(new DiffHunkViewModel(hunk));
            }
        }
    }
}

public partial class DiffHunkViewModel : ObservableObject
{
    public string Header { get; }
    public ObservableCollection<DiffLineViewModel> Lines { get; } = new();

    public DiffHunkViewModel(ParsedHunk hunk)
    {
        Header = hunk.Header;
        foreach (var line in hunk.Lines)
        {
            Lines.Add(new DiffLineViewModel(line));
        }
    }
}

public partial class DiffLineViewModel : ObservableObject
{
    public DiffLineType Type { get; }
    public string Content { get; }
    public int? OldLineNumber { get; }
    public int? NewLineNumber { get; }

    public DiffLineViewModel(ParsedLine line)
    {
        Type = line.Type;
        Content = line.Content;
        OldLineNumber = line.OldLineNumber;
        NewLineNumber = line.NewLineNumber;
    }
}
