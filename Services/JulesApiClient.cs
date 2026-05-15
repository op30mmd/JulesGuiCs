using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    Task<ActivityListResponse> ListActivitiesAsync(string sid, int pageSize = 30, string? pageToken = null, string? filter = null, CancellationToken ct = default);
    Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default);
    IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default);
}

public class JulesApiClient : IJulesApiClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private const string Base = "https://jules.googleapis.com/v1alpha/";
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions _debugJson = new(_json);

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

    public async Task<SourceListResponse> ListSourcesAsync(string? pageToken = null, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.GetAsync("sources" + (pageToken != null ? $"?pageToken={Uri.EscapeDataString(pageToken)}" : ""), ct);
        await HandleErrorResponse(r, ct);
        return await r.Content.ReadFromJsonAsync<SourceListResponse>(_json, ct) ?? throw new Exception("Parse failed");
    }
    public async Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.PostAsJsonAsync("sessions", req, _json, ct);
        await HandleErrorResponse(r, ct);
        return await r.Content.ReadFromJsonAsync<Session>(_json, ct) ?? throw new Exception("Parse failed");
    }
    public async Task<SessionListResponse> ListSessionsAsync(int pageSize = 10, string? pageToken = null, CancellationToken ct = default)
    {
        ApplyKey();
        var q = new List<string> { $"pageSize={pageSize}" }; if (pageToken != null) q.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        try
        {
            var r = await _http.GetAsync($"sessions?{string.Join("&", q)}", ct);
            await HandleErrorResponse(r, ct);
            return await r.Content.ReadFromJsonAsync<SessionListResponse>(_json, ct) ?? throw new Exception("Parse failed");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to list sessions: {ex.Message}", ex);
        }
    }
    public async Task<Session> GetSessionAsync(string id, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.GetAsync(id, ct);
        await HandleErrorResponse(r, ct);
        return await r.Content.ReadFromJsonAsync<Session>(_json, ct) ?? throw new Exception("Parse failed");
    }
    public async Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default)
    {
        ApplyKey();
        var r = await _http.PostAsync($"{id}:approvePlan", null, ct);
        await HandleErrorResponse(r, ct);
        return await r.Content.ReadFromJsonAsync<ApprovePlanResponse>(_json, ct) ?? new ApprovePlanResponse();
    }
    public async Task<ActivityListResponse> ListActivitiesAsync(string sid, int pageSize = 30, string? pageToken = null, string? filter = null, CancellationToken ct = default)
    {
        ApplyKey();
        var q = new List<string> { $"pageSize={pageSize}" };
        if (pageToken != null) q.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        if (filter != null) q.Add($"filter={Uri.EscapeDataString(filter)}");
        try
        {
            var r = await _http.GetAsync($"{sid}/activities?{string.Join("&", q)}", ct);
            await HandleErrorResponse(r, ct);

            var content = await r.Content.ReadAsStringAsync(ct);
            var resp = JsonSerializer.Deserialize<ActivityListResponse>(content, _json) ?? throw new Exception("Parse failed");

            // Debug: Capture raw JSON for each activity
            if (resp.Activities != null)
            {
                var nodes = System.Text.Json.Nodes.JsonNode.Parse(content)?["activities"]?.AsArray();
                for (int i = 0; i < resp.Activities.Count; i++)
                {
                    if (nodes != null && i < nodes.Count)
                        resp.Activities[i].RawInfo = nodes[i]?.ToJsonString(_debugJson);
                }
            }
            return resp;
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to list activities for session {sid}: {ex.Message}", ex);
        }
    }
    public async Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default)
    {
        ApplyKey();
        var req = new { prompt };
        var r = await _http.PostAsJsonAsync($"{sid}:sendMessage", req, _json, ct);
        await HandleErrorResponse(r, ct);
        return new SendMessageResponse { Success = true };
    }
    public IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default) =>
        Observable.Create<ActivityListResponse>(async (obs, token) =>
        {
            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(ct, token); string? npt = null;
            while (!lcts.IsCancellationRequested)
            {
                try { var a = await ListActivitiesAsync(sid, 30, npt, null, lcts.Token); obs.OnNext(a); npt = a.NextPageToken; }
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
