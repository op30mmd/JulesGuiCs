using System.Diagnostics;
using JulesClient.Models;

namespace JulesClient.Services;

public interface ICachedJulesApiClient : IJulesApiClient
{
    Task InvalidateAllAsync(CancellationToken ct = default);
}

public class CachedJulesApiClient : ICachedJulesApiClient, IDisposable
{
    private readonly IJulesApiClient _inner;
    private readonly ICacheService _cache;
    private readonly TimeSpan _sourcesTtl = TimeSpan.FromHours(24);
    private readonly TimeSpan _sessionsTtl = TimeSpan.FromHours(24);
    private readonly TimeSpan _activitiesTtl = TimeSpan.FromHours(24);
    private readonly TimeSpan _sessionDetailTtl = TimeSpan.FromHours(24);

    public CachedJulesApiClient(IJulesApiClient inner, ICacheService cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<SourceListResponse> ListSourcesAsync(string? pageToken = null, CancellationToken ct = default)
    {
        if (pageToken != null)
        {
            return await _inner.ListSourcesAsync(pageToken, ct);
        }

        var cached = await _cache.GetAsync<SourceListResponse>("sources:all", ct);
        if (cached?.Sources != null)
        {
            return cached;
        }

        var fresh = await _inner.ListSourcesAsync(ct: ct);
        if (fresh.Sources != null)
        {
            await _cache.SetAsync("sources:all", fresh, _sourcesTtl, ct);
        }
        return fresh;
    }

    public async Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default)
    {
        var result = await _inner.CreateSessionAsync(req, ct);
        await _cache.RemoveByPrefixAsync("sessions", ct);
        await _cache.RemoveAsync("sources:all", ct);
        Debug.WriteLine($"[CACHE] Invalidated sessions/sources cache after CreateSession");
        return result;
    }

    public async Task<SessionListResponse> ListSessionsAsync(int pageSize = 5, string? pageToken = null, CancellationToken ct = default)
    {
        if (pageToken != null)
        {
            return await _inner.ListSessionsAsync(pageSize, pageToken, ct);
        }

        var cached = await _cache.GetAsync<SessionListResponse>("sessions:all", ct);
        if (cached?.Sessions != null)
        {
            return cached;
        }

        var allSessions = new List<Session>();
        string? token = null;
        do
        {
            var response = await _inner.ListSessionsAsync(pageSize, token, ct);
            if (response.Sessions != null) allSessions.AddRange(response.Sessions);
            token = response.NextPageToken;
        } while (token != null);

        var merged = new SessionListResponse { Sessions = allSessions, NextPageToken = null };
        await _cache.SetAsync("sessions:all", merged, _sessionsTtl, ct);
        return merged;
    }

    public async Task<Session> GetSessionAsync(string id, CancellationToken ct = default)
    {
        var cacheKey = $"session:{id}";
        var cached = await _cache.GetAsync<Session>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }

        var fresh = await _inner.GetSessionAsync(id, ct);
        await _cache.SetAsync(cacheKey, fresh, _sessionDetailTtl, ct);
        return fresh;
    }

    public async Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default)
    {
        var result = await _inner.ApprovePlanAsync(id, ct);
        await _cache.RemoveAsync($"session:{id}", ct);
        await _cache.RemoveAsync("sessions:all", ct);
        Debug.WriteLine($"[CACHE] Invalidated session cache after ApprovePlan: {id}");
        return result;
    }

    public async Task<ActivityListResponse> ListActivitiesAsync(string sid, int pageSize = 10, string? pageToken = null, string? filter = null, CancellationToken ct = default)
    {
        if (pageToken != null || filter != null)
        {
            return await _inner.ListActivitiesAsync(sid, pageSize, pageToken, filter, ct);
        }

        var cacheKey = $"activities:{sid}";
        var cached = await _cache.GetAsync<ActivityListResponse>(cacheKey, ct);
        if (cached?.Activities != null)
        {
            return cached;
        }

        var allActivities = new List<JulesClient.Models.Activity>();
        string? token = null;
        do
        {
            var response = await _inner.ListActivitiesAsync(sid, pageSize, token, ct: ct);
            if (response.Activities != null) allActivities.AddRange(response.Activities);
            token = response.NextPageToken;
        } while (token != null);

        var merged = new ActivityListResponse { Activities = allActivities, NextPageToken = null };
        if (allActivities.Any())
        {
            await _cache.SetAsync(cacheKey, merged, _activitiesTtl, ct);
        }
        return merged;
    }

    public async Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default)
    {
        var result = await _inner.SendMessageAsync(sid, prompt, ct);
        await _cache.RemoveAsync($"activities:{sid}", ct);
        await _cache.RemoveAsync($"session:{sid}", ct);
        Debug.WriteLine($"[CACHE] Invalidated activities cache after SendMessage: {sid}");
        return result;
    }

    public IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default)
    {
        return _inner.PollActivitiesAsync(sid, interval, ct);
    }

    public async Task InvalidateAllAsync(CancellationToken ct = default)
    {
        await _cache.ClearAsync(ct);
        Debug.WriteLine("[CACHE] All caches invalidated");
    }

    public void Dispose()
    {
    }
}
