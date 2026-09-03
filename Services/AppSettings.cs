using System;

namespace JulesClient.Services;

/// <summary>
/// A plain, UI-free snapshot of the user's preferences that any code can read
/// without going through DI. <see cref="Apply"/> refreshes it from the persisted
/// <see cref="ISettingsService"/> (at startup and after Save) and raises
/// <see cref="Changed"/> so live views can re-render.
/// </summary>
public static class AppSettings
{
    // Chat / text
    public static string ChatFontFamily { get; private set; } = "Segoe UI Variable Text, Segoe UI";
    public static double ChatFontSize { get; private set; } = 15;
    public static double ChatLineHeight { get; private set; } = 1.5;
    public static string CodeFontFamily { get; private set; } = "Consolas, Cascadia Mono, Courier New";
    public static double CodeFontSize { get; private set; } = 12.5;
    public static bool SyntaxHighlighting { get; private set; } = true;
    public static bool MarkdownEnabled { get; private set; } = true;
    public static bool ShowTimestamps { get; private set; } = true;
    public static bool ShowOriginatorLabels { get; private set; } = true;
    public static bool ShowCodeLanguageLabel { get; private set; } = true;
    public static bool ShowProgressUpdates { get; private set; } = true;
    public static bool CollapseAgentMessages { get; private set; } = true;
    public static bool CollapseUserMessages { get; private set; } = true;
    public static bool CollapseLongCodeBlocks { get; private set; } = true;
    public static bool AutoScrollChat { get; private set; } = true;
    // Enter sends the message; Shift+Enter inserts a newline. When off, Enter
    // inserts a newline and the Send button (or Ctrl+Enter) sends.
    public static bool SendOnEnter { get; private set; } = true;

    // Sessions / behaviour
    public static int PollingIntervalSeconds { get; private set; } = 10;
    public static int MaxSessionsShown { get; private set; }          // 0 = no limit
    public static bool DefaultRequirePlanApproval { get; private set; } = true;
    public static bool DefaultAutoCreatePR { get; private set; }
    public static bool ConfirmBeforeSend { get; private set; }

    // Diff viewer
    public static double DiffFontSize { get; private set; } = 11.5;
    public static bool DiffAutoExpandSingleFile { get; private set; } = true;
    public static int DiffAutoExpandMaxLines { get; private set; } = 400;
    public static bool DiffWrapLines { get; private set; }

    // Cache
    public static bool CachingEnabled { get; private set; } = true;
    public static long CacheMaxSizeBytes { get; private set; } = 500L * 1024 * 1024;

    // Diagnostics
    public static bool VerboseLogging { get; private set; }

    public static event Action? Changed;

    public static void Apply(ISettingsService s)
    {
        ChatFontFamily = Fallback(s.ChatFontFamily, ChatFontFamily);
        ChatFontSize = Clamp(s.ChatFontSize, 10, 24, 15);
        ChatLineHeight = Clamp(s.ChatLineHeight, 1.0, 2.5, 1.5);
        CodeFontFamily = Fallback(s.CodeFontFamily, CodeFontFamily);
        CodeFontSize = Clamp(s.CodeFontSize, 9, 20, 12.5);
        SyntaxHighlighting = s.SyntaxHighlighting;
        MarkdownEnabled = s.MarkdownEnabled;
        ShowTimestamps = s.ShowTimestamps;
        ShowOriginatorLabels = s.ShowOriginatorLabels;
        ShowCodeLanguageLabel = s.ShowCodeLanguageLabel;
        ShowProgressUpdates = s.ShowProgressUpdates;
        CollapseAgentMessages = s.CollapseAgentMessages;
        CollapseUserMessages = s.CollapseUserMessages;
        CollapseLongCodeBlocks = s.CollapseLongCodeBlocks;
        AutoScrollChat = s.AutoScrollChat;
        SendOnEnter = s.SendOnEnter;

        PollingIntervalSeconds = (int)Clamp(s.PollingIntervalSeconds, 3, 120, 10);
        MaxSessionsShown = Math.Max(0, s.MaxSessionsShown);
        DefaultRequirePlanApproval = s.DefaultRequirePlanApproval;
        DefaultAutoCreatePR = s.DefaultAutoCreatePR;
        ConfirmBeforeSend = s.ConfirmBeforeSend;

        DiffFontSize = Clamp(s.DiffFontSize, 9, 18, 11.5);
        DiffAutoExpandSingleFile = s.DiffAutoExpandSingleFile;
        DiffAutoExpandMaxLines = Math.Max(0, s.DiffAutoExpandMaxLines);
        DiffWrapLines = s.DiffWrapLines;

        CachingEnabled = s.CachingEnabled;
        CacheMaxSizeBytes = Math.Max(16, s.CacheMaxSizeMB) * 1024L * 1024L;

        VerboseLogging = s.VerboseLogging;

        try { Changed?.Invoke(); } catch { }
    }

    private static string Fallback(string? value, string def) =>
        string.IsNullOrWhiteSpace(value) ? def : value.Trim();

    private static double Clamp(double value, double min, double max, double def)
    {
        if (double.IsNaN(value) || value <= 0) return def;
        return Math.Min(max, Math.Max(min, value));
    }
}
