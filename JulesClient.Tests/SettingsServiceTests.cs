using JulesClient.Services;

namespace JulesClient.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedValues()
    {
        var settings = new SettingsService();

        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal(30, settings.RequestTimeoutSeconds);
        Assert.Equal(3, settings.MaxRetries);
        Assert.Equal("Default", settings.AppTheme);
        Assert.Equal(15.0, settings.ChatFontSize);
        Assert.Equal(1.5, settings.ChatLineHeight);
        Assert.True(settings.SyntaxHighlighting);
        Assert.True(settings.MarkdownEnabled);
        Assert.Equal(10, settings.PollingIntervalSeconds);
        Assert.Equal(0, settings.MaxSessionsShown);
        Assert.True(settings.DefaultRequirePlanApproval);
        Assert.False(settings.DefaultAutoCreatePR);
        Assert.Equal(11.5, settings.DiffFontSize);
        Assert.True(settings.CachingEnabled);
        Assert.Equal(500, settings.CacheMaxSizeMB);
        Assert.Equal(ProxyMode.None, settings.ProxyMode);
        Assert.False(settings.IsDemoMode);
        Assert.False(settings.VerboseLogging);
    }

    [Fact]
    public void SetAndGetProperties_PersistInMemory()
    {
        var settings = new SettingsService
        {
            ApiKey = "test-api-key",
            RequestTimeoutSeconds = 45,
            AppTheme = "Dark",
            ChatFontSize = 18.0,
            PollingIntervalSeconds = 15,
            ProxyMode = ProxyMode.Manual,
            ProxyHost = "127.0.0.1",
            ProxyPort = 1080,
            IsDemoMode = true,
            VerboseLogging = true
        };

        Assert.Equal("test-api-key", settings.ApiKey);
        Assert.Equal(45, settings.RequestTimeoutSeconds);
        Assert.Equal("Dark", settings.AppTheme);
        Assert.Equal(18.0, settings.ChatFontSize);
        Assert.Equal(15, settings.PollingIntervalSeconds);
        Assert.Equal(ProxyMode.Manual, settings.ProxyMode);
        Assert.Equal("127.0.0.1", settings.ProxyHost);
        Assert.Equal(1080, settings.ProxyPort);
        Assert.True(settings.IsDemoMode);
        Assert.True(settings.VerboseLogging);
    }

    [Fact]
    public void ProxyMode_LegacyFallback_UsesProxyEnabled()
    {
        var settings = new SettingsService
        {
            ProxyEnabled = true
        };

        // When ProxyMode value is not set in raw store, fallback checks ProxyEnabled
        Assert.Equal(ProxyMode.Manual, settings.ProxyMode);
    }
}
