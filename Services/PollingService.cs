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
    private readonly TimeSpan _def = TimeSpan.FromSeconds(10);

    public PollingService(IJulesApiClient api) => _api = api;

    public IDisposable StartPolling(string sid, Action<ActivityListResponse> onRecv, TimeSpan? iv = null)
    {
        StopPolling(sid);
        var i = iv ?? _def;
        Debug.WriteLine($"[POLLING] Starting poll for {sid} every {i.TotalSeconds}s");

        var p = Observable.Create<ActivityListResponse>(obs =>
        {
            var cts = new CancellationTokenSource();
            DateTime lastTimestamp = DateTime.MinValue;

            var loop = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        string? pageToken = null;
                        var allActivities = new List<JulesClient.Models.Activity>();
                        ActivityListResponse? firstResp = null;

                        do
                        {
                            string? filter = lastTimestamp != DateTime.MinValue
                                ? $"create_time > \"{lastTimestamp:yyyy-MM-ddTHH:mm:ss.fffZ}\""
                                : null;

                            Debug.WriteLine($"[POLLING] Requesting activities for {sid} (filter: {filter ?? "none"})");
                            var resp = await _api.ListActivitiesAsync(sid, 20, pageToken: pageToken, filter: filter, ct: cts.Token);

                            if (firstResp == null) firstResp = resp;
                            if (resp?.Activities != null)
                            {
                                allActivities.AddRange(resp.Activities);
                            }
                            pageToken = resp?.NextPageToken;
                        } while (pageToken != null && !cts.Token.IsCancellationRequested);

                        if (allActivities.Any())
                        {
                            var maxTime = allActivities.Where(a => a.CreateTime.HasValue).Max(a => a.CreateTime);
                            if (maxTime.HasValue)
                            {
                                lastTimestamp = maxTime.Value;
                            }

                            Debug.WriteLine($"[POLLING] Received {allActivities.Count} new activities for {sid}. New lastTimestamp: {lastTimestamp:HH:mm:ss.fff}");
                            obs.OnNext(firstResp! with { Activities = allActivities });
                        }
                        else if (firstResp != null)
                        {
                            // Also emit if we got a response even if no new activities (important for first poll)
                            obs.OnNext(firstResp);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[POLLING] Error for {sid}: {ex.Message}");
                    }

                    try { await Task.Delay(i, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
                Debug.WriteLine($"[POLLING] Polling loop ended for {sid}");
            });

            return () =>
            {
                Debug.WriteLine($"[POLLING] Stopping poll for {sid}");
                cts.Cancel();
                cts.Dispose();
            };
        }).Subscribe(
            onNext: resp => onRecv(resp),
            onError: e => Debug.WriteLine($"[POLLING] Fatal error: {e}")
        );

        _pollers[sid] = p;
        OnPropertyChanged(nameof(IsPolling));
        return p;
    }

    public void StopPolling(string sid)
    {
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
        OnPropertyChanged(nameof(IsPolling));
    }

    public void Dispose() => StopAll();
}
