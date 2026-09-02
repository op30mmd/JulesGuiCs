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
        new PropertyMetadata(null, (d, _) => ((MarkdownPresenter)d).Rebuild()));

    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

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

        // Plain-text mode: skip the markdown parser entirely.
        if (!AppSettings.MarkdownEnabled)
        {
            Children.Add(PlainTextBlock(text));
            return;
        }

        try
        {
            foreach (var segment in MarkdownConflictParser.Split(text))
            {
                Children.Add(segment.Conflict is { } conflict
                    ? BuildConflict(conflict)
                    : BuildMarkdown(segment.Text));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MARKDOWN] Presenter rebuild failed: {ex.Message}");
            Children.Clear();
            Children.Add(PlainTextBlock(text));
        }
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
