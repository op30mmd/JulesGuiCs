using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ICacheService _cache;

    // Connection
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private double _requestTimeoutSeconds;
    [ObservableProperty] private double _maxRetries;

    // Appearance
    [ObservableProperty] private int _themeIndex; // 0 Default, 1 Light, 2 Dark

    // Chat / text
    [ObservableProperty] private string _chatFontFamily = string.Empty;
    [ObservableProperty] private double _chatFontSize;
    [ObservableProperty] private double _chatLineHeight;
    [ObservableProperty] private string _codeFontFamily = string.Empty;
    [ObservableProperty] private double _codeFontSize;
    [ObservableProperty] private bool _syntaxHighlighting;
    [ObservableProperty] private bool _markdownEnabled;
    [ObservableProperty] private bool _showTimestamps;
    [ObservableProperty] private bool _showOriginatorLabels;
    [ObservableProperty] private bool _showCodeLanguageLabel;
    [ObservableProperty] private bool _showProgressUpdates;
    [ObservableProperty] private bool _collapseAgentMessages;
    [ObservableProperty] private bool _collapseUserMessages;
    [ObservableProperty] private bool _collapseLongCodeBlocks;
    [ObservableProperty] private bool _autoScrollChat;
    [ObservableProperty] private bool _sendOnEnter;

    // Sessions / behaviour
    [ObservableProperty] private double _pollingIntervalSeconds;
    [ObservableProperty] private double _maxSessionsShown;
    [ObservableProperty] private bool _defaultRequirePlanApproval;
    [ObservableProperty] private bool _defaultAutoCreatePR;
    [ObservableProperty] private bool _confirmBeforeSend;

    // Diff viewer
    [ObservableProperty] private double _diffFontSize;
    [ObservableProperty] private bool _diffAutoExpandSingleFile;
    [ObservableProperty] private double _diffAutoExpandMaxLines;
    [ObservableProperty] private bool _diffWrapLines;

    // Cache
    [ObservableProperty] private bool _cachingEnabled;
    [ObservableProperty] private double _cacheMaxSizeMB;
    [ObservableProperty] private string _cacheSizeText = "…";

    // Proxy
    [ObservableProperty] private ProxyMode _proxyMode;
    [ObservableProperty] private bool _proxyBypassLocal;
    [ObservableProperty] private string _proxyHost = string.Empty;
    [ObservableProperty] private double _proxyPort;
    [ObservableProperty] private string _proxyUsername = string.Empty;
    [ObservableProperty] private string _proxyPassword = string.Empty;

    // Diagnostics
    [ObservableProperty] private bool _isDemoMode;
    [ObservableProperty] private bool _verboseLogging;

    public SettingsViewModel()
    {
        _settings = App.Current.Services.GetRequiredService<ISettingsService>();
        _cache = App.Current.Services.GetRequiredService<ICacheService>();

        _apiKey = _settings.ApiKey;
        _requestTimeoutSeconds = _settings.RequestTimeoutSeconds;
        _maxRetries = _settings.MaxRetries;

        _themeIndex = _settings.AppTheme switch { "Light" => 1, "Dark" => 2, _ => 0 };

        _chatFontFamily = _settings.ChatFontFamily;
        _chatFontSize = _settings.ChatFontSize;
        _chatLineHeight = _settings.ChatLineHeight;
        _codeFontFamily = _settings.CodeFontFamily;
        _codeFontSize = _settings.CodeFontSize;
        _syntaxHighlighting = _settings.SyntaxHighlighting;
        _markdownEnabled = _settings.MarkdownEnabled;
        _showTimestamps = _settings.ShowTimestamps;
        _showOriginatorLabels = _settings.ShowOriginatorLabels;
        _showCodeLanguageLabel = _settings.ShowCodeLanguageLabel;
        _showProgressUpdates = _settings.ShowProgressUpdates;
        _collapseAgentMessages = _settings.CollapseAgentMessages;
        _collapseUserMessages = _settings.CollapseUserMessages;
        _collapseLongCodeBlocks = _settings.CollapseLongCodeBlocks;
        _autoScrollChat = _settings.AutoScrollChat;
        _sendOnEnter = _settings.SendOnEnter;

        _pollingIntervalSeconds = _settings.PollingIntervalSeconds;
        _maxSessionsShown = _settings.MaxSessionsShown;
        _defaultRequirePlanApproval = _settings.DefaultRequirePlanApproval;
        _defaultAutoCreatePR = _settings.DefaultAutoCreatePR;
        _confirmBeforeSend = _settings.ConfirmBeforeSend;

        _diffFontSize = _settings.DiffFontSize;
        _diffAutoExpandSingleFile = _settings.DiffAutoExpandSingleFile;
        _diffAutoExpandMaxLines = _settings.DiffAutoExpandMaxLines;
        _diffWrapLines = _settings.DiffWrapLines;

        _cachingEnabled = _settings.CachingEnabled;
        _cacheMaxSizeMB = _settings.CacheMaxSizeMB;

        _proxyMode = _settings.ProxyMode;
        _proxyBypassLocal = _settings.ProxyBypassLocal;
        _proxyHost = _settings.ProxyHost;
        _proxyPort = _settings.ProxyPort;
        _proxyUsername = _settings.ProxyUsername;
        _proxyPassword = _settings.ProxyPassword;

        _isDemoMode = _settings.IsDemoMode;
        _verboseLogging = _settings.VerboseLogging;

        _ = RefreshCacheSizeAsync();
    }

    public void Save()
    {
        _settings.ApiKey = ApiKey;
        _settings.RequestTimeoutSeconds = (int)RequestTimeoutSeconds;
        _settings.MaxRetries = (int)MaxRetries;

        _settings.AppTheme = ThemeIndex switch { 1 => "Light", 2 => "Dark", _ => "Default" };

        _settings.ChatFontFamily = ChatFontFamily;
        _settings.ChatFontSize = ChatFontSize;
        _settings.ChatLineHeight = ChatLineHeight;
        _settings.CodeFontFamily = CodeFontFamily;
        _settings.CodeFontSize = CodeFontSize;
        _settings.SyntaxHighlighting = SyntaxHighlighting;
        _settings.MarkdownEnabled = MarkdownEnabled;
        _settings.ShowTimestamps = ShowTimestamps;
        _settings.ShowOriginatorLabels = ShowOriginatorLabels;
        _settings.ShowCodeLanguageLabel = ShowCodeLanguageLabel;
        _settings.ShowProgressUpdates = ShowProgressUpdates;
        _settings.CollapseAgentMessages = CollapseAgentMessages;
        _settings.CollapseUserMessages = CollapseUserMessages;
        _settings.CollapseLongCodeBlocks = CollapseLongCodeBlocks;
        _settings.AutoScrollChat = AutoScrollChat;
        _settings.SendOnEnter = SendOnEnter;

        _settings.PollingIntervalSeconds = (int)PollingIntervalSeconds;
        _settings.MaxSessionsShown = (int)MaxSessionsShown;
        _settings.DefaultRequirePlanApproval = DefaultRequirePlanApproval;
        _settings.DefaultAutoCreatePR = DefaultAutoCreatePR;
        _settings.ConfirmBeforeSend = ConfirmBeforeSend;

        _settings.DiffFontSize = DiffFontSize;
        _settings.DiffAutoExpandSingleFile = DiffAutoExpandSingleFile;
        _settings.DiffAutoExpandMaxLines = (int)DiffAutoExpandMaxLines;
        _settings.DiffWrapLines = DiffWrapLines;

        _settings.CachingEnabled = CachingEnabled;
        _settings.CacheMaxSizeMB = (int)CacheMaxSizeMB;

        _settings.ProxyMode = ProxyMode;
        _settings.ProxyBypassLocal = ProxyBypassLocal;
        _settings.ProxyHost = ProxyHost;
        _settings.ProxyPort = (int)ProxyPort;
        _settings.ProxyUsername = ProxyUsername;
        _settings.ProxyPassword = ProxyPassword;

        _settings.IsDemoMode = IsDemoMode;
        _settings.VerboseLogging = VerboseLogging;

        AppSettings.Apply(_settings);
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await _cache.ClearAsync();
        await RefreshCacheSizeAsync();
    }

    [RelayCommand]
    private async Task RefreshCacheSizeAsync()
    {
        try
        {
            var bytes = await _cache.GetCacheSizeAsync();
            CacheSizeText = bytes <= 0
                ? "empty"
                : bytes < 1024 * 1024
                    ? $"{bytes / 1024.0:0.0} KB"
                    : $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }
        catch { CacheSizeText = "unknown"; }
    }
}
