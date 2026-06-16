using System.Diagnostics;
using System.Text.Json;

#pragma warning disable CS1998 // Async method lacks 'await' in non-Windows builds

namespace JulesClient.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
    Task<long> GetCacheSizeAsync(CancellationToken ct = default);
    Task CleanupExpiredAsync(CancellationToken ct = default);
}

public class CacheEntry<T>
{
    public T? Data { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CacheService : ICacheService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const long MaxCacheSizeBytes = 500 * 1024 * 1024;

#if WINDOWS
    private readonly Windows.Storage.StorageFolder _localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
    private const string CacheSubfolder = "cache";

    private async Task<string> GetFilePath(string key)
    {
        var safeKey = key.Replace("/", "_").Replace(":", "_").Replace("\\", "_");
        var folder = await _localFolder.CreateFolderAsync(CacheSubfolder, Windows.Storage.CreationCollisionOption.OpenIfExists);
        return System.IO.Path.Combine(folder.Path, $"{safeKey}.json");
    }

    private async Task<Windows.Storage.StorageFolder?> GetCacheFolder()
    {
        return await _localFolder.CreateFolderAsync(CacheSubfolder, Windows.Storage.CreationCollisionOption.OpenIfExists);
    }
#endif

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var path = await GetFilePath(key);
            if (!System.IO.File.Exists(path)) return default;

            var json = await System.IO.File.ReadAllTextAsync(path, ct);
            var entry = JsonSerializer.Deserialize<CacheEntry<T>>(json, _json);

            if (entry == null) return default;
            if (DateTime.UtcNow > entry.ExpiresAt)
            {
                await RemoveAsync(key, ct);
                return default;
            }

            return entry.Data;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Read error for {key}: {ex.Message}");
            return default;
        }
#else
        return default;
#endif
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var folder = await GetCacheFolder();
            if (folder != null)
            {
                var props = await folder.GetBasicPropertiesAsync();
                if (props.Size > MaxCacheSizeBytes)
                {
                    await CleanupExpiredAsync(ct);
                }
            }

            var entry = new CacheEntry<T>
            {
                Data = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(30))
            };

            var json = JsonSerializer.Serialize(entry, _json);
            var path = await GetFilePath(key);
            await System.IO.File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Write error for {key}: {ex.Message}");
        }
#endif
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var path = await GetFilePath(key);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Remove error for {key}: {ex.Message}");
        }
#endif
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var folder = await GetCacheFolder();
            if (folder == null) return;
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                await file.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Clear error: {ex.Message}");
        }
#endif
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var folder = await GetCacheFolder();
            if (folder == null) return;
            var files = await folder.GetFilesAsync();
            var safePrefix = prefix.Replace("/", "_").Replace(":", "_").Replace("\\", "_");

            foreach (var file in files)
            {
                if (file.Name.StartsWith(safePrefix))
                {
                    await file.DeleteAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] RemoveByPrefix error for {prefix}: {ex.Message}");
        }
#endif
    }

    public async Task<long> GetCacheSizeAsync(CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var folder = await GetCacheFolder();
            if (folder == null) return 0;
            var props = await folder.GetBasicPropertiesAsync();
            return (long)props.Size;
        }
        catch { return 0; }
#else
        return 0;
#endif
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
#if WINDOWS
        try
        {
            var folder = await GetCacheFolder();
            if (folder == null) return;
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(folder.Path, file.Name), ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("expiresAt", out var expProp))
                    {
                        var expiresAt = expProp.GetDateTime();
                        if (DateTime.UtcNow > expiresAt)
                        {
                            await file.DeleteAsync();
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Cleanup error: {ex.Message}");
        }
#endif
    }
}
