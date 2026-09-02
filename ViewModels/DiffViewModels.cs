using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class DiffFileViewModel : ObservableObject
{
    public DiffFileNode Node { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<DiffHunkViewModel> Hunks { get; } = new();

    public DiffFileViewModel(DiffFileNode node)
    {
        Node = node;
    }

    public string DisplayName => Node.DisplayName;
    public int TotalLines => Node.TotalLines;
    public int AddedLines => Node.AddedLines;
    public int RemovedLines => Node.RemovedLines;

    public string AddedBadge => $"+{Node.AddedLines}";
    public string RemovedBadge => $"−{Node.RemovedLines}"; // U+2212 MINUS SIGN
    public string HunkCountLabel => Node.File.Hunks.Count == 1 ? "1 hunk" : $"{Node.File.Hunks.Count} hunks";

    // Hunks are materialised on first expand to keep large diffs cheap to open.
    partial void OnIsExpandedChanged(bool value)
    {
        if (value) LoadHunks();
    }

    public void LoadHunks()
    {
        if (Hunks.Count > 0) return;
        foreach (var hunk in Node.File.Hunks)
        {
            Hunks.Add(new DiffHunkViewModel(hunk));
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

    public string OldGutter => OldLineNumber?.ToString() ?? string.Empty;
    public string NewGutter => NewLineNumber?.ToString() ?? string.Empty;

    public double FontSize => AppSettings.DiffFontSize;
    public TextWrapping Wrapping => AppSettings.DiffWrapLines ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public FontFamily CodeFontFamily => new(AppSettings.CodeFontFamily);

    public string Sign => Type switch
    {
        DiffLineType.Added => "+",
        DiffLineType.Removed => "−",
        _ => string.Empty
    };
}
