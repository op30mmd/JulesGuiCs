using CommunityToolkit.Mvvm.ComponentModel;
using JulesClient.Models;
namespace JulesClient.Services;

public interface IPollingService {
    IDisposable StartPolling(string sessionId, Action<ActivityListResponse> onRecv, TimeSpan? interval=null);
    void StopPolling(string sessionId);
    bool IsPolling(string sessionId);
}
public class PollingService : ObservableObject, IPollingService, IDisposable {
    private readonly IJulesApiClient _api;
    private readonly Dictionary<string,IDisposable> _pollers=new();
    private readonly TimeSpan _def=TimeSpan.FromSeconds(3);
    public PollingService(IJulesApiClient api)=>_api=api;
    public IDisposable StartPolling(string sid, Action<ActivityListResponse> onRecv, TimeSpan? iv=null) {
        StopPolling(sid); var i=iv??_def;
        var p=Observable.Interval(i).SelectMany(_=>Observable.FromAsync(ct=>_api.ListActivitiesAsync(sid,30,ct:ct)))
          .ObserveOn(SynchronizationContext.Current??SynchronizationContext.Current!).Subscribe(onNext:onRecv,onError:e=>System.Diagnostics.Debug.WriteLine(e));
        _pollers[sid]=p; OnPropertyChanged(nameof(IsPolling)); return p;
    }
    public void StopPolling(string sid){if(_pollers.Remove(sid,out var p)){p.Dispose();OnPropertyChanged(nameof(IsPolling));}}
    public bool IsPolling(string sid)=>_pollers.ContainsKey(sid);
    public void StopAll(){foreach(var p in _pollers.Values)p.Dispose();_pollers.Clear();OnPropertyChanged(nameof(IsPolling));}
    public void Dispose()=>StopAll();
}
