using System.Collections.ObjectModel;
using JulesClient.Models;
using JulesClient.Services;
using System.Diagnostics;

namespace JulesClient.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    private readonly IJulesApiClient _api;
    private readonly IPollingService _polling;
    private IDisposable? _pollingSubscription;
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private Session? _selectedSession;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    public ObservableCollection<Session> Sessions { get; } = new();
    public ObservableCollection<JulesClient.Models.Activity> Activities { get; } = new();

    public SessionsViewModel()
    {
        _api = App.Current.Services.GetRequiredService<IJulesApiClient>();
        _polling = App.Current.Services.GetRequiredService<IPollingService>();
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            Debug.WriteLine("[VM] Loading sessions...");
            var sessions = new List<Session>();
            string? pageToken = null;
            do
            {
                var response = await _api.ListSessionsAsync(pageToken: pageToken);
                if (response.Sessions != null) sessions.AddRange(response.Sessions);
                pageToken = response.NextPageToken;
            } while (pageToken != null);

            _syncContext?.Post(_ =>
            {
                Sessions.Clear();
                foreach (var session in sessions)
                {
                    Sessions.Add(session);
                }
            }, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to load sessions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSessionChanged(Session? value)
    {
        _pollingSubscription?.Dispose();
        Activities.Clear();
        if (value != null)
        {
            Debug.WriteLine($"[VM] Session selected: {value.Name}");
            _ = LoadActivitiesAsync(value.Name);
            _pollingSubscription = _polling.StartPolling(value.Name, resp =>
            {
                _syncContext?.Post(_ =>
                {
                    if (resp.Activities != null)
                    {
                        foreach (var activity in resp.Activities.OrderBy(a => a.CreateTime ?? string.Empty))
                        {
                            if (!Activities.Any(a => a.Name == activity.Name))
                            {
                                Debug.WriteLine($"[VM] New activity: {activity.Name} from {activity.Originator}");
                                Activities.Add(activity);
                            }
                        }
                    }
                }, null);
            });
        }
    }

    private async Task LoadActivitiesAsync(string sessionId)
    {
        try
        {
            Debug.WriteLine($"[VM] Loading activities for {sessionId}...");
            var allActivities = new List<JulesClient.Models.Activity>();
            string? pageToken = null;
            do
            {
                var response = await _api.ListActivitiesAsync(sessionId, pageToken: pageToken);
                if (response.Activities != null) allActivities.AddRange(response.Activities);
                pageToken = response.NextPageToken;
            } while (pageToken != null);

            _syncContext?.Post(_ =>
            {
                foreach (var activity in allActivities.OrderBy(a => a.CreateTime ?? string.Empty))
                {
                    if (!Activities.Any(a => a.Name == activity.Name))
                        Activities.Add(activity);
                }
            }, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to load activities: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(ChatInput)) return;
        var msg = ChatInput;
        ChatInput = string.Empty;
        try
        {
            await _api.SendMessageAsync(SelectedSession.Name, msg);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to send message: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApprovePlanAsync()
    {
        if (SelectedSession == null) return;
        try
        {
            await _api.ApprovePlanAsync(SelectedSession.Name);
            var updated = await _api.GetSessionAsync(SelectedSession.Name);
            _syncContext?.Post(_ => SelectedSession = updated, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to approve plan: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        _pollingSubscription?.Dispose();
    }
}
