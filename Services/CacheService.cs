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

    private static long MaxCacheSizeBytes => AppSettings.CacheMaxSizeBytes;

#if WINDOWS
    private readonly ISettingsService _settings;
    private readonly Windows.Storage.StorageFolder _localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
    private const string CacheSubfolder = "cache";

    public CacheService(ISettingsService settings) => _settings = settings;

    // A short, stable id for the current account + mode. Cached data lives in a
    // per-partition subfolder so entries written for one Jules account (or for
    // demo mode) are never served after switching to a different key/mode.
    private string GetPartition()
    {
        var raw = (_settings.IsDemoMode ? "demo" : "live") + "|" + _settings.ApiKey;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    // Root folder holding every partition. Get/Set/Remove/Clear stay scoped to the
    // current partition; size and expiry maintenance sweep the whole root so cache
    // left behind by other accounts still gets bounded and cleaned.
    private string RootCachePath => System.IO.Path.Combine(_localFolder.Path, CacheSubfolder);

    private async Task<Windows.Storage.StorageFolder> GetCacheFolder()
    {
        var root = await _localFolder.CreateFolderAsync(CacheSubfolder, Windows.Storage.CreationCollisionOption.OpenIfExists);
        return await root.CreateFolderAsync(GetPartition(), Windows.Storage.CreationCollisionOption.OpenIfExists);
    }

    private async Task<string> GetFilePath(string key)
    {
        var safeKey = key.Replace("/", "_").Replace(":", "_").Replace("\\", "_");
        var folder = await GetCacheFolder();
        return System.IO.Path.Combine(folder.Path, $"{safeKey}.json");
    }
#else
    public CacheService(ISettingsService settings) { }
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
            await EnforceSizeLimitAsync(ct);

            var entry = new CacheEntry<T>
            {
                Data = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(30))
            };

            var json = JsonSerializer.Serialize(entry, _json);
            var path = await GetFilePath(key);

            // Write to a temp file and swap it in, so a concurrent reader never
            // sees a half-written cache file.
            var tmp = path + ".tmp";
            await System.IO.File.WriteAllTextAsync(tmp, json, ct);
            System.IO.File.Move(tmp, path, overwrite: true);
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
            var dir = new System.IO.DirectoryInfo(RootCachePath);
            if (!dir.Exists) return 0;

            long total = 0;
            foreach (var f in dir.GetFiles("*.json", System.IO.SearchOption.AllDirectories)) total += f.Length;
            return total;
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
            var dir = new System.IO.DirectoryInfo(RootCachePath);
            if (!dir.Exists) return;

            foreach (var file in dir.GetFiles("*.json", System.IO.SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(file.FullName, ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("expiresAt", out var expProp))
                    {
                        if (DateTime.UtcNow > expProp.GetDateTime())
                        {
                            file.Delete();
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

#if WINDOWS
    // Keeps the on-disk cache under MaxCacheSizeBytes: first drop expired entries,
    // then, if still over, evict the oldest entries down to 80% of the cap.
    private async Task EnforceSizeLimitAsync(CancellationToken ct)
    {
        try
        {
            var dir = new System.IO.DirectoryInfo(RootCachePath);
            if (!dir.Exists) return;

            long Total(System.IO.FileInfo[] fs)
            {
                long t = 0;
                foreach (var f in fs) t += f.Length;
                return t;
            }

            var files = dir.GetFiles("*.json", System.IO.SearchOption.AllDirectories);
            if (Total(files) <= MaxCacheSizeBytes) return;

            await CleanupExpiredAsync(ct);

            files = dir.GetFiles("*.json", System.IO.SearchOption.AllDirectories);
            long total = Total(files);
            if (total <= MaxCacheSizeBytes) return;

            long target = MaxCacheSizeBytes * 8 / 10;
            foreach (var f in files.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total <= target) break;
                try
                {
                    var len = f.Length;
                    f.Delete();
                    total -= len;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE] Size-limit enforcement error: {ex.Message}");
        }
    }
#endif
}
