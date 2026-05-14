using System.Net.Http.Headers;
using System.Text.Json;
using JulesClient.Models;
using System.Reactive.Linq;

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
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private const string Base = "https://jules.googleapis.com/v1alpha/";
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JulesApiClient(ISettingsService settings, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri(Base), Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private void ApplyKey()
    {
        _http.DefaultRequestHeaders.Remove("X-Goog-Api-Key");
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Goog-Api-Key", _settings.ApiKey);
        }
    }

    private async Task HandleErrorResponse(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        string content = await response.Content.ReadAsStringAsync(ct);
        string message = $"API Request failed with status {response.StatusCode} for {response.RequestMessage?.RequestUri}.";

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var errorObj = JsonSerializer.Deserialize<GoogleErrorResponse>(content, _json);
                if (errorObj?.Error != null)
                {
                    message = $"API Error ({errorObj.Error.Code}): {errorObj.Error.Message} (Status: {errorObj.Error.Status})";
                }
                else
                {
                    message += $" Response: {content}";
                }
            }
            catch
            {
                message += $" Response: {content}";
            }
        }
        else
        {
            message += " (Empty response body)";
        }

        throw new Exception(message);
    }

    public async Task<SourceListResponse> ListSourcesAsync(string? pt = null, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.GetAsync("sources" + (pt != null ? $"?pageToken={Uri.EscapeDataString(pt)}" : ""), ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<SourceListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default)
    {
        ApplyKey();
        var c = new StringContent(JsonSerializer.Serialize(req, _json), System.Text.Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("sessions", c, ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<Session>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<SessionListResponse> ListSessionsAsync(int ps = 10, string? pt = null, CancellationToken ct = default)
    {
        ApplyKey();
        var q = new List<string> { $"pageSize={ps}" }; if (pt != null) q.Add($"pageToken={Uri.EscapeDataString(pt)}");
        var r = await _http.GetAsync($"sessions?{string.Join("&", q)}", ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<SessionListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<Session> GetSessionAsync(string id, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.GetAsync($"sessions/{id}", ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<Session>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.PostAsync($"sessions/{id}:approvePlan", null, ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<ApprovePlanResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? new ApprovePlanResponse();
    }
    public async Task<ActivityListResponse> ListActivitiesAsync(string sid, int ps = 30, string? pt = null, CancellationToken ct = default)
    {
        ApplyKey();
        var q = new List<string> { $"pageSize={ps}" }; if (pt != null) q.Add($"pageToken={Uri.EscapeDataString(pt)}");
        var r = await _http.GetAsync($"sessions/{sid}/activities?{string.Join("&", q)}", ct);
        await HandleErrorResponse(r, ct);
        return JsonSerializer.Deserialize<ActivityListResponse>(await r.Content.ReadAsStringAsync(ct), _json) ?? throw new Exception("Parse failed");
    }
    public async Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default)
    {
        ApplyKey();
        var req = new { prompt }; var c = new StringContent(JsonSerializer.Serialize(req, _json), System.Text.Encoding.UTF8, "application/json");
        var r = await _http.PostAsync($"sessions/{sid}:sendMessage", c, ct);
        await HandleErrorResponse(r, ct);
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

public class GoogleErrorResponse
{
    public GoogleError? Error { get; set; }
}

public class GoogleError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
