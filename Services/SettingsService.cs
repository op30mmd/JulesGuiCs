using System;
using System.Collections.Generic;

namespace JulesClient.Services;

public enum ProxyMode
{
    None,
    Manual,
    System
}

public interface ISettingsService
{
    // Connection
    string ApiKey { get; set; }
    int RequestTimeoutSeconds { get; set; }
    int MaxRetries { get; set; }

    // Appearance
    string AppTheme { get; set; }   // "Default" | "Light" | "Dark"

    // Chat / text rendering
    string ChatFontFamily { get; set; }
    double ChatFontSize { get; set; }
    double ChatLineHeight { get; set; }
    string CodeFontFamily { get; set; }
    double CodeFontSize { get; set; }
    bool SyntaxHighlighting { get; set; }
    bool MarkdownEnabled { get; set; }
    bool ShowTimestamps { get; set; }
    bool ShowOriginatorLabels { get; set; }
    bool ShowCodeLanguageLabel { get; set; }
    bool AutoScrollChat { get; set; }

    // Sessions / behaviour
    int PollingIntervalSeconds { get; set; }
    int MaxSessionsShown { get; set; }
    bool DefaultRequirePlanApproval { get; set; }
    bool DefaultAutoCreatePR { get; set; }
    bool ConfirmBeforeSend { get; set; }

    // Diff viewer
    double DiffFontSize { get; set; }
    bool DiffAutoExpandSingleFile { get; set; }
    int DiffAutoExpandMaxLines { get; set; }
    bool DiffWrapLines { get; set; }

    // Cache
    bool CachingEnabled { get; set; }
    int CacheMaxSizeMB { get; set; }

    // Proxy
    ProxyMode ProxyMode { get; set; }
    bool ProxyEnabled { get; set; } // legacy, kept for transition logic
    string ProxyHost { get; set; }
    int ProxyPort { get; set; }
    string ProxyUsername { get; set; }
    string ProxyPassword { get; set; }
    bool ProxyBypassLocal { get; set; }

    // Diagnostics
    bool IsDemoMode { get; set; }
    bool VerboseLogging { get; set; }
}

public class SettingsService : ISettingsService
{
#if WINDOWS
    private readonly Windows.Storage.ApplicationDataContainer _store =
        Windows.Storage.ApplicationData.Current.LocalSettings;

    private T Get<T>(string key, T fallback)
    {
        try
        {
            if (_store.Values.TryGetValue(key, out var v) && v is not null)
            {
                if (v is T typed) return typed;
                return (T)Convert.ChangeType(v, typeof(T));
            }
        }
        catch { }
        return fallback;
    }

    private void Set<T>(string key, T value)
    {
        try { _store.Values[key] = value; } catch { }
    }
#else
    private readonly Dictionary<string, object?> _store = new();

    private T Get<T>(string key, T fallback)
    {
        if (_store.TryGetValue(key, out var v) && v is T typed) return typed;
        return fallback;
    }

    private void Set<T>(string key, T value) => _store[key] = value;
#endif

    // Connection
    public string ApiKey { get => Get("ApiKey", string.Empty); set => Set("ApiKey", value); }
    public int RequestTimeoutSeconds { get => Get("RequestTimeoutSeconds", 30); set => Set("RequestTimeoutSeconds", value); }
    public int MaxRetries { get => Get("MaxRetries", 3); set => Set("MaxRetries", value); }

    // Appearance
    public string AppTheme { get => Get("AppTheme", "Default"); set => Set("AppTheme", value); }

    // Chat / text
    public string ChatFontFamily
    {
        get { var v = Get("ChatFontFamily", string.Empty); return string.IsNullOrWhiteSpace(v) ? "Segoe UI Variable Text, Segoe UI" : v; }
        set => Set("ChatFontFamily", value);
    }
    public double ChatFontSize { get => Get("ChatFontSize", 15.0); set => Set("ChatFontSize", value); }
    public double ChatLineHeight { get => Get("ChatLineHeight", 1.5); set => Set("ChatLineHeight", value); }
    public string CodeFontFamily
    {
        get { var v = Get("CodeFontFamily", string.Empty); return string.IsNullOrWhiteSpace(v) ? "Consolas, Cascadia Mono, Courier New" : v; }
        set => Set("CodeFontFamily", value);
    }
    public double CodeFontSize { get => Get("CodeFontSize", 12.5); set => Set("CodeFontSize", value); }
    public bool SyntaxHighlighting { get => Get("SyntaxHighlighting", true); set => Set("SyntaxHighlighting", value); }
    public bool MarkdownEnabled { get => Get("MarkdownEnabled", true); set => Set("MarkdownEnabled", value); }
    public bool ShowTimestamps { get => Get("ShowTimestamps", true); set => Set("ShowTimestamps", value); }
    public bool ShowOriginatorLabels { get => Get("ShowOriginatorLabels", true); set => Set("ShowOriginatorLabels", value); }
    public bool ShowCodeLanguageLabel { get => Get("ShowCodeLanguageLabel", true); set => Set("ShowCodeLanguageLabel", value); }
    public bool AutoScrollChat { get => Get("AutoScrollChat", true); set => Set("AutoScrollChat", value); }

    // Sessions / behaviour
    public int PollingIntervalSeconds { get => Get("PollingIntervalSeconds", 10); set => Set("PollingIntervalSeconds", value); }
    public int MaxSessionsShown { get => Get("MaxSessionsShown", 0); set => Set("MaxSessionsShown", value); }
    public bool DefaultRequirePlanApproval { get => Get("DefaultRequirePlanApproval", true); set => Set("DefaultRequirePlanApproval", value); }
    public bool DefaultAutoCreatePR { get => Get("DefaultAutoCreatePR", false); set => Set("DefaultAutoCreatePR", value); }
    public bool ConfirmBeforeSend { get => Get("ConfirmBeforeSend", false); set => Set("ConfirmBeforeSend", value); }

    // Diff viewer
    public double DiffFontSize { get => Get("DiffFontSize", 11.5); set => Set("DiffFontSize", value); }
    public bool DiffAutoExpandSingleFile { get => Get("DiffAutoExpandSingleFile", true); set => Set("DiffAutoExpandSingleFile", value); }
    public int DiffAutoExpandMaxLines { get => Get("DiffAutoExpandMaxLines", 400); set => Set("DiffAutoExpandMaxLines", value); }
    public bool DiffWrapLines { get => Get("DiffWrapLines", false); set => Set("DiffWrapLines", value); }

    // Cache
    public bool CachingEnabled { get => Get("CachingEnabled", true); set => Set("CachingEnabled", value); }
    public int CacheMaxSizeMB { get => Get("CacheMaxSizeMB", 500); set => Set("CacheMaxSizeMB", value); }

    // Proxy
    public ProxyMode ProxyMode
    {
        get
        {
            var raw = Get<string?>("ProxyMode", null);
            if (raw is not null && Enum.TryParse<ProxyMode>(raw, out var parsed)) return parsed;
            return ProxyEnabled ? ProxyMode.Manual : ProxyMode.None; // legacy fallback
        }
        set => Set("ProxyMode", value.ToString());
    }

    public bool ProxyEnabled { get => Get("ProxyEnabled", false); set => Set("ProxyEnabled", value); }
    public string ProxyHost { get => Get("ProxyHost", string.Empty); set => Set("ProxyHost", value); }
    public int ProxyPort { get => Get("ProxyPort", 1080); set => Set("ProxyPort", value); }
    public string ProxyUsername { get => Get("ProxyUsername", string.Empty); set => Set("ProxyUsername", value); }
    public string ProxyPassword { get => Get("ProxyPassword", string.Empty); set => Set("ProxyPassword", value); }
    public bool ProxyBypassLocal { get => Get("ProxyBypassLocal", true); set => Set("ProxyBypassLocal", value); }

    // Diagnostics
    public bool IsDemoMode { get => Get("IsDemoMode", false); set => Set("IsDemoMode", value); }
    public bool VerboseLogging { get => Get("VerboseLogging", false); set => Set("VerboseLogging", value); }
}
