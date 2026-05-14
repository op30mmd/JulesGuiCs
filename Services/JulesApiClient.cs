using System.Net.Http.Headers;
using System.Text.Json;
using JulesClient.Models;
namespace JulesClient.Services;

public interface IJulesApiClient
{
    Task<SourceListResponse> ListSourcesAsync(string? pageToken = null, CancellationToken ct = default);
    Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default);
    Task<SessionListResponse> ListSessionsAsync(int pageSize = 10, string? pageToken = null, CancellationToken ct = default);
    Task<Session> GetSessionAsync(string id, CancellationToken ct = default);
    Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default);
    Task<ActivityListResponse> ListActivitiesAsync(string sid, int pageSize = 30, string? pageToken = null, CancellationToken ct = default);
    Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default);
    IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default);
}

public class JulesApiClient : IJulesApiClient, IDisposable
{
    private readonly HttpClient _http; private readonly string _key;
    private const string Base = "https://jules.googleapis.com/v1alpha";
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JulesApiClient(string key, HttpMessageHandler? proxy = null)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _http = new HttpClient(proxy ?? new HttpClientHandler()) { BaseAddress = new Uri(Base), Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Add("X-Goog-Api-Key", _key);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    public async Task<SourceListResponse> ListSourcesAsync(string? pt = null, CancellationToken ct = default)
    {
        var r = await _http.GetAsync("/sources" + (pt != null ? $"?pageToken={Uri.EscapeDataString(pt)}" : ""), ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<SourceListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default)
    {
        var c = new StringContent(JsonSerializer.Serialize(req, _json), System.Text.Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("/sessions", c, ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<Session>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<SessionListResponse> ListSessionsAsync(int ps = 10, string? pt = null, CancellationToken ct = default)
    {
        var q = new List<string> { $"pageSize={ps}" }; if (pt != null) q.Add($"pageToken={Uri.EscapeDataString(pt)}");
        var r = await _http.GetAsync($"/sessions?{string.Join("&", q)}", ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<SessionListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<Session> GetSessionAsync(string id, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"/sessions/{id}", ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<Session>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"/sessions/{id}:approvePlan", null, ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<ApprovePlanResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? new ApprovePlanResponse();
    }
    public async Task<ActivityListResponse> ListActivitiesAsync(string sid, int ps = 30, string? pt = null, CancellationToken ct = default)
    {
        var q = new List<string> { $"pageSize={ps}" }; if (pt != null) q.Add($"pageToken={Uri.EscapeDataString(pt)}");
        var r = await _http.GetAsync($"/sessions/{sid}/activities?{string.Join("&", q)}", ct); r.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<ActivityListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default)
    {
        var req = new { prompt }; var c = new StringContent(JsonSerializer.Serialize(req, _json), System.Text.Encoding.UTF8, "application/json");
        var r = await _http.PostAsync($"/sessions/{sid}:sendMessage", c, ct); r.EnsureSuccessStatusCode();
        return new SendMessageResponse { Success = true };
    }
    public IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default) =>
        Observable.Create<ActivityListResponse>(async (obs, token) =>
        {
            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(ct, token); string? npt = null;
            while (!lcts.IsCancellationRequested)
            {
                try { var a = await ListActivitiesAsync(sid, 30, npt, lcts.Token); obs.OnNext(a); npt = a.NextPageToken; }
                catch (Exception e) when (e is not OperationCanceledException) { obs.OnError(e); return; }
                await Task.Delay(interval, lcts.Token);
            }
        });
    public void Dispose() => _http.Dispose();
}
