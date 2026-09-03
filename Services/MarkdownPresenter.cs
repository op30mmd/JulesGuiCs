using System;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace JulesClient.Services;

/// <summary>
/// Hosts rendered Markdown. Plain content goes through <see cref="MarkdownParser"/>
/// into a <see cref="TextBlock"/>; a search/replace edit block is lifted into its
/// own collapsed <see cref="Expander"/> with the two sides shown as a red/green
/// diff. Replaces a bare <c>TextBlock</c> + <c>MarkdownHelper.Text</c> binding.
/// </summary>
public sealed class MarkdownPresenter : StackPanel
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownPresenter),
        new PropertyMetadata(null, (d, _) =>
        {
            var p = (MarkdownPresenter)d;
            p._expanded = false; // a recycled container just got new content
            p.Rebuild();
        }));

    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    // When true, content longer than <see cref="CollapseThreshold"/> characters
    // renders folded to its first line with a "Show more" toggle. Used for the
    // agent's long "thinking out loud" chat messages.
    public static readonly DependencyProperty CollapsibleProperty = DependencyProperty.Register(
        nameof(Collapsible), typeof(bool), typeof(MarkdownPresenter),
        new PropertyMetadata(false, (d, _) => ((MarkdownPresenter)d).Rebuild()));

    public bool Collapsible
    {
        get => (bool)GetValue(CollapsibleProperty);
        set => SetValue(CollapsibleProperty, value);
    }

    private const int CollapseThreshold = 500;
    private bool _expanded;

    public static readonly DependencyProperty BaseFontSizeProperty = DependencyProperty.Register(
        nameof(BaseFontSize), typeof(double), typeof(MarkdownPresenter),
        new PropertyMetadata(14.0, (d, _) => ((MarkdownPresenter)d).Rebuild()));

    public double BaseFontSize
    {
        get => (double)GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    public MarkdownPresenter()
    {
        Spacing = 6;
        Loaded += (_, _) => AppSettings.Changed += OnSettingsChanged;
        Unloaded += (_, _) => AppSettings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        if (DispatcherQueue is { } dq) dq.TryEnqueue(Rebuild);
    }

    private void Rebuild()
    {
        if (DispatcherQueue is { HasThreadAccess: false } dq)
        {
            dq.TryEnqueue(Rebuild);
            return;
        }

        Children.Clear();
        var text = Markdown;
        if (string.IsNullOrEmpty(text)) return;

        // Collapsed: show only the first line plus a toggle.
        if (Collapsible && !_expanded && text.Length > CollapseThreshold)
        {
            Children.Add(PlainTextBlock(FirstLine(text)));
            Children.Add(BuildToggle("Show more", expand: true));
            return;
        }

        // Plain-text mode: skip the markdown parser entirely.
        if (!AppSettings.MarkdownEnabled)
        {
            Children.Add(PlainTextBlock(text));
        }
        else
        {
            try
            {
                foreach (var segment in MarkdownConflictParser.Split(text))
                {
                    if (segment.Conflict is { } conflict)
                    {
                        Children.Add(BuildConflict(conflict));
                        continue;
                    }

                    // Lift fenced code blocks into their own collapsible cards;
                    // the prose between them still renders as a TextBlock.
                    foreach (var piece in FencedCode.Split(segment.Text))
                    {
                        Children.Add(piece.Code is { } code
                            ? BuildCodeBlock(code)
                            : BuildMarkdown(piece.Text));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MARKDOWN] Presenter rebuild failed: {ex.Message}");
                Children.Clear();
                Children.Add(PlainTextBlock(text));
            }
        }

        if (Collapsible && _expanded && text.Length > CollapseThreshold)
        {
            Children.Add(BuildToggle("Show less", expand: false));
        }
    }

    // The first non-blank line of the source, stripped of leading markdown
    // markers and trimmed, used as the collapsed preview.
    private static string FirstLine(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('#', '-', '*', '>', ' ', '\t').Trim();
            if (line.Length == 0) continue;
            return line.Length > 160 ? line[..160].TrimEnd() + " …" : line + " …";
        }
        return "…";
    }

    private FrameworkElement BuildToggle(string label, bool expand)
    {
        var btn = new HyperlinkButton
        {
            Content = label,
            Padding = new Thickness(0),
            MinWidth = 0,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };
        btn.Click += (_, _) =>
        {
            _expanded = expand;
            Rebuild();
        };
        return btn;
    }

    private TextBlock PlainTextBlock(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily(AppSettings.ChatFontFamily),
        TextWrapping = TextWrapping.Wrap,
        FontSize = BaseFontSize + (AppSettings.ChatFontSize - 15),
        LineHeight = System.Math.Round((BaseFontSize + 1) * AppSettings.ChatLineHeight),
        LineStackingStrategy = LineStackingStrategy.MaxHeight,
        IsTextSelectionEnabled = true
    };

    private TextBlock BuildMarkdown(string markdown)
    {
        // Base + the user's delta from the default 15pt, so the smaller call
        // sites (plan steps, comments) keep their relative size.
        var size = BaseFontSize + 1 + (AppSettings.ChatFontSize - 15);
        var tb = new TextBlock
        {
            FontFamily = new FontFamily(AppSettings.ChatFontFamily),
            // Wrap (not WrapWholeWords) so long paths / URLs / hashes break
            // instead of clipping; a roomy line height keeps the
            // one-sentence-per-line agent output readable; MaxHeight lets a
            // heading or code block still grow taller.
            TextWrapping = TextWrapping.Wrap,
            FontSize = size,
            LineHeight = System.Math.Round(size * AppSettings.ChatLineHeight),
            LineStackingStrategy = LineStackingStrategy.MaxHeight,
            IsTextSelectionEnabled = true
        };
        MarkdownParser.ParseInto(tb, markdown);
        return tb;
    }

    // The collapsed header and the expanded panes are both pinned to this width,
    // so toggling the Expander changes the bubble's height only, never its width
    // (which was jumping and, worse, made the bubble jump between alignments).
    private const double ConflictWidth = 600;
    private const double ConflictPaneMaxWidth = 540;

    // A fenced code block longer than this starts collapsed (when the setting
    // is on); shorter ones start expanded but stay collapsible.
    private const int CodeCollapseLines = 14;

    private static FrameworkElement BuildConflict(ConflictBlock conflict)
    {
        int lineCount = CountLines(conflict.Search) + CountLines(conflict.Replace);
        var caption = string.IsNullOrEmpty(conflict.Language)
            ? $"Proposed edit  ·  {lineCount} lines"
            : $"Proposed edit  ·  {conflict.Language}  ·  {lineCount} lines";

        var body = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        body.Children.Add(BuildPane("SEARCH", conflict.Search, conflict.Language, removed: true));
        body.Children.Add(BuildPane("REPLACE", conflict.Replace, conflict.Language, removed: false));

        return new Expander
        {
            Header = new TextBlock { Text = caption, FontWeight = FontWeights.SemiBold, FontSize = 13 },
            Content = body,
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinWidth = ConflictWidth,
            MaxWidth = ConflictWidth,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private static int CountLines(string s) => string.IsNullOrEmpty(s) ? 0 : s.Split('\n').Length;

    // A fenced code block, rendered as a collapsible card with the language and
    // line count in the header and the highlighted, side-scrolling code inside.
    private static FrameworkElement BuildCodeBlock(FencedCodeBlock block)
    {
        var code = block.Code ?? string.Empty;
        int lineCount = CountLines(code);
        var unit = lineCount == 1 ? "line" : "lines";
        var caption = string.IsNullOrEmpty(block.Language)
            ? $"Code  ·  {lineCount} {unit}"
            : $"{block.Language}  ·  {lineCount} {unit}";

        var codeText = new TextBlock
        {
            FontFamily = MarkdownParser.CodeFont,
            FontSize = MarkdownParser.CodeFontSize,
            TextWrapping = TextWrapping.NoWrap,
            IsTextSelectionEnabled = true
        };
        foreach (var token in CodeHighlighter.Highlight(code, block.Language))
        {
            var run = new Run { Text = token.Text };
            var brush = MarkdownParser.CodeTokenBrush(token.Kind);
            if (brush != null) run.Foreground = brush;

            if (token.Kind == CodeTokenKind.Comment)
                codeText.Inlines.Add(new Italic { Inlines = { run } });
            else
                codeText.Inlines.Add(run);
        }

        var body = new ScrollViewer
        {
            Content = codeText,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = ConflictPaneMaxWidth,
            Margin = new Thickness(0, 4, 0, 0)
        };

        bool startCollapsed = AppSettings.CollapseLongCodeBlocks && lineCount > CodeCollapseLines;

        return new Expander
        {
            Header = new TextBlock
            {
                Text = caption,
                FontFamily = MarkdownParser.CodeFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.75
            },
            Content = body,
            IsExpanded = !startCollapsed,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinWidth = ConflictWidth,
            MaxWidth = ConflictWidth,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private static Border BuildPane(string label, string code, string? language, bool removed)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = MarkdownParser.CodeFont,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Opacity = 0.6
        });

        // Code must not wrap - it scrolls sideways inside the pane instead. If it
        // wrapped, the longest line would set the pane's desired width, which
        // drives the whole chat bubble wider and makes it jump on expand/collapse.
        var codeText = new TextBlock
        {
            FontFamily = MarkdownParser.CodeFont,
            FontSize = MarkdownParser.CodeFontSize,
            TextWrapping = TextWrapping.NoWrap,
            IsTextSelectionEnabled = true
        };

        foreach (var token in CodeHighlighter.Highlight(code, language))
        {
            var run = new Run { Text = token.Text };
            var brush = MarkdownParser.CodeTokenBrush(token.Kind);
            if (brush != null) run.Foreground = brush;

            if (token.Kind == CodeTokenKind.Comment)
                codeText.Inlines.Add(new Italic { Inlines = { run } });
            else
                codeText.Inlines.Add(run);
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = codeText,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = ConflictPaneMaxWidth
        });

        byte r = removed ? (byte)0xF8 : (byte)0x3F;
        byte g = removed ? (byte)0x51 : (byte)0xB9;
        byte b = removed ? (byte)0x49 : (byte)0x50;

        return new Border
        {
            Child = panel,
            Background = new SolidColorBrush(ColorHelper.FromArgb(0x2A, r, g, b)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0x66, r, g, b)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8)
        };
    }
}
