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
    private static readonly Dictionary<string, List<JulesClient.Models.Activity>> _activitiesCache = new();
    private static List<Session>? _sessionsCache;

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
    public async Task RefreshAllCommand()
    {
        _sessionsCache = null;
        _activitiesCache.Clear();
        await LoadSessionsAsync();
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        if (_sessionsCache != null)
        {
            Sessions.Clear();
            foreach (var s in _sessionsCache) Sessions.Add(s);
        }

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

            _sessionsCache = sessions;
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

            if (_activitiesCache.TryGetValue(value.Name, out var cached))
            {
                foreach (var a in cached.OrderBy(a => a.CreateTime ?? string.Empty)) Activities.Add(a);
            }

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
                                // Remove optimistic message if this activity is its server-side counterpart
                                if (activity.Originator == "user" && !string.IsNullOrEmpty(activity.UserMessage?.Prompt))
                                {
                                    var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == activity.UserMessage.Prompt);
                                    if (local != null) Activities.Remove(local);
                                }

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

            _activitiesCache[sessionId] = allActivities;

            _syncContext?.Post(_ =>
            {
                foreach (var activity in allActivities.OrderBy(a => a.CreateTime ?? string.Empty))
                {
                    if (!Activities.Any(a => a.Name == activity.Name))
                    {
                         // Remove optimistic message
                        if (activity.Originator == "user" && !string.IsNullOrEmpty(activity.UserMessage?.Prompt))
                        {
                            var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == activity.UserMessage.Prompt);
                            if (local != null) Activities.Remove(local);
                        }
                        Activities.Add(activity);
                    }
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

        // Optimistic UI update
        var localMsg = new JulesClient.Models.Activity(
            Name: $"local_{Guid.NewGuid()}",
            Id: null,
            CreateTime: DateTime.UtcNow.ToString("O"),
            Originator: "user",
            ProgressUpdated: null,
            PlanGenerated: null,
            PlanApproved: null,
            SessionCompleted: null,
            SessionFailed: null,
            BashOutput: null,
            ChangeSet: null,
            Media: null,
            PullRequest: null,
            Artifacts: null,
            UserMessage: new UserMessage(Prompt: msg, Text: null),
            AgentMessage: null,
            Text: null,
            Prompt: null,
            Description: null
        );
        Activities.Add(localMsg);

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
