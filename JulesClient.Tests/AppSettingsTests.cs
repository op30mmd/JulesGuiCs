using JulesClient.Services;

namespace JulesClient.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Apply_UpdatesPropertiesAndClampsValues()
    {
        var settings = new SettingsService
        {
            ChatFontSize = 5.0, // should clamp to min 10
            ChatLineHeight = 3.0, // should clamp to max 2.5
            PollingIntervalSeconds = 1, // should clamp to min 3
            DiffFontSize = 100.0, // should clamp to max 18
            CacheMaxSizeMB = 10, // should use max(16, 10) = 16 MB -> 16 * 1024 * 1024
            ChatFontFamily = "   " // fallback
        };

        AppSettings.Apply(settings);

        Assert.Equal(10.0, AppSettings.ChatFontSize);
        Assert.Equal(2.5, AppSettings.ChatLineHeight);
        Assert.Equal(3, AppSettings.PollingIntervalSeconds);
        Assert.Equal(18.0, AppSettings.DiffFontSize);
        Assert.Equal(16L * 1024 * 1024, AppSettings.CacheMaxSizeBytes);
        Assert.Equal("Segoe UI Variable Text, Segoe UI", AppSettings.ChatFontFamily);
    }

    [Fact]
    public void Apply_RaisesChangedEvent()
    {
        bool changedRaised = false;
        Action handler = () => changedRaised = true;

        AppSettings.Changed += handler;
        try
        {
            var settings = new SettingsService();
            AppSettings.Apply(settings);
            Assert.True(changedRaised);
        }
        finally
        {
            AppSettings.Changed -= handler;
        }
    }
}
