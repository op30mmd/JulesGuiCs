using CommunityToolkit.Mvvm.ComponentModel;
using JulesClient.Models;
using System.Diagnostics;
using System.Reactive.Linq;

namespace JulesClient.Services;

public interface IPollingService
{
    IDisposable StartPolling(string sessionId, Action<ActivityListResponse> onRecv, TimeSpan? interval = null);
    void StopPolling(string sessionId);
    bool IsPolling(string sessionId);
}

public class PollingService : ObservableObject, IPollingService, IDisposable
{
    private readonly ICachedJulesApiClient _api;
    private readonly Dictionary<string, IDisposable> _pollers = new();
    private readonly Dictionary<string, DateTime> _lastTimestamps = new();
    private readonly TimeSpan _def = TimeSpan.FromSeconds(10);

    public PollingService(ICachedJulesApiClient api) => _api = api;

    public IDisposable StartPolling(string sid, Action<ActivityListResponse> onRecv, TimeSpan? iv = null)
    {
        StopPolling(sid);
        var i = iv ?? _def;

        var p = Observable.Interval(i)
            .SelectMany(_ => Observable.FromAsync(async ct =>
            {
                try
                {
                    DateTime last = DateTime.MinValue;
                    bool hasLast = _lastTimestamps.TryGetValue(sid, out last);

                    ActivityListResponse? firstResp = null;
                    string? pageToken = null;
                    var allActivities = new List<JulesClient.Models.Activity>();

                    do
                    {
                        string? filter = hasLast ? $"create_time > \"{last:yyyy-MM-ddTHH:mm:ss.fffZ}\"" : null;
                        var resp = await _api.ListActivitiesAsync(sid, 10, pageToken: pageToken, filter: filter, ct: ct);
                        if (firstResp == null) firstResp = resp;

                        if (resp?.Activities != null)
                        {
                            allActivities.AddRange(resp.Activities);
                        }
                        pageToken = resp?.NextPageToken;
                    } while (pageToken != null && !ct.IsCancellationRequested);

                    if (allActivities.Any())
                    {
                        var maxTime = allActivities.Where(a => a.CreateTime.HasValue).Max(a => a.CreateTime);
                        if (maxTime.HasValue)
                        {
                            _lastTimestamps[sid] = maxTime.Value;
                        }
                    }

                    return firstResp != null ? firstResp with { Activities = allActivities } : null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"[POLLING] Error for {sid}: {ex}");
                    return null;
                }
            }))
            .Where(resp => resp != null)
            .Subscribe(
                onNext: resp => onRecv(resp!),
                onError: e => Debug.WriteLine($"[POLLING] Fatal error: {e}")
            );

        _pollers[sid] = p;
        OnPropertyChanged(nameof(IsPolling));
        return p;
    }

    public void StopPolling(string sid)
    {
        _lastTimestamps.Remove(sid);
        if (_pollers.Remove(sid, out var p))
        {
            p.Dispose();
            OnPropertyChanged(nameof(IsPolling));
        }
    }

    public bool IsPolling(string sid) => _pollers.ContainsKey(sid);

    public void StopAll()
    {
        foreach (var p in _pollers.Values) p.Dispose();
        _pollers.Clear();
        _lastTimestamps.Clear();
        OnPropertyChanged(nameof(IsPolling));
    }

    public void Dispose() => StopAll();
}
