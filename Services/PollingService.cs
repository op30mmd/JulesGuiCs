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
    private readonly IJulesApiClient _api;
    private readonly Dictionary<string, IDisposable> _pollers = new();
    private readonly Dictionary<string, string> _lastTimestamps = new();
    private readonly TimeSpan _def = TimeSpan.FromSeconds(3);

    public PollingService(IJulesApiClient api) => _api = api;

    public IDisposable StartPolling(string sid, Action<ActivityListResponse> onRecv, TimeSpan? iv = null)
    {
        StopPolling(sid);
        var i = iv ?? _def;

        var p = Observable.Create<ActivityListResponse>(async (observer, ct) =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        string? last = null;
                        _lastTimestamps.TryGetValue(sid, out last);

                        ActivityListResponse? firstResp = null;
                        string? pageToken = null;
                        var allActivities = new List<JulesClient.Models.Activity>();

                        do
                        {
                            var resp = await _api.ListActivitiesAsync(sid, 30, pageToken: pageToken, createTime: last, ct: ct);
                            if (firstResp == null) firstResp = resp;

                            if (resp?.Activities != null)
                            {
                                allActivities.AddRange(resp.Activities);
                            }
                            pageToken = resp?.NextPageToken;
                        } while (pageToken != null && !ct.IsCancellationRequested);

                        if (allActivities.Any())
                        {
                            var maxTime = allActivities.Max(a => a.CreateTime ?? string.Empty);
                            if (!string.IsNullOrEmpty(maxTime))
                            {
                                _lastTimestamps[sid] = maxTime;
                            }
                        }

                        if (firstResp != null)
                        {
                            observer.OnNext(firstResp with { Activities = allActivities });
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Debug.WriteLine($"[POLLING] Error for {sid}: {ex}");
                    }

                    try
                    {
                        await Task.Delay(i, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            })
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(
                onNext: onRecv!,
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
