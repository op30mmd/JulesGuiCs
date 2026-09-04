using JulesClient.Services;

namespace JulesClient.Tests;

public class CacheServiceTests
{
    [Fact]
    public async Task NonWindows_CacheServiceMethods_ExecuteWithoutThrowing()
    {
        var settings = new SettingsService();
        var cache = new CacheService(settings);

        // Under non-Windows builds (like net8.0 test runner on Linux), CacheService uses fallback implementations that don't throw exceptions.
        await cache.SetAsync("key1", "value1");
        var val = await cache.GetAsync<string>("key1");
        Assert.Null(val); // default return in non-Windows build

        await cache.RemoveAsync("key1");
        await cache.RemoveByPrefixAsync("key");
        await cache.ClearAsync();
        await cache.CleanupExpiredAsync();

        var size = await cache.GetCacheSizeAsync();
        Assert.Equal(0, size);
    }
}
