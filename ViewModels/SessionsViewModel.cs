using System.Collections.ObjectModel;
using JulesClient.Models;
using JulesClient.Services;
using System.Diagnostics;
using Microsoft.UI.Dispatching;

namespace JulesClient.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    private readonly ICachedJulesApiClient _api;
    private readonly IPollingService _polling;
    private IDisposable? _pollingSubscription;
    private readonly DispatcherQueue _dispatcher;
    private readonly HashSet<string> _loadedActivityIds = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private Session? _selectedSession;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private ParsedPatch? _aggregatePatch;

    public ObservableCollection<Session> Sessions { get; } = new();
    public ObservableCollection<JulesClient.Models.Activity> Activities { get; } = new();
    public ObservableCollection<DiffFileViewModel> DiffFiles { get; } = new();
    public ObservableCollection<DiffDisplayItem> FlattenedDiff { get; } = new();

    public SessionsViewModel()
    {
        _api = App.Current.Services.GetRequiredService<ICachedJulesApiClient>();
        _polling = App.Current.Services.GetRequiredService<IPollingService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    public async Task RefreshAllDataAsync()
    {
        await _api.InvalidateAllAsync();
        _loadedActivityIds.Clear();
        await LoadSessionsAsync();
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            Debug.WriteLine("[VM] Loading sessions...");
            var allSessions = new List<Session>();
            string? pageToken = null;
            do
            {
                var response = await _api.ListSessionsAsync(pageToken: pageToken);
                if (response.Sessions != null) allSessions.AddRange(response.Sessions);
                pageToken = response.NextPageToken;
            } while (pageToken != null);

            _dispatcher.TryEnqueue(() =>
            {
                SyncSessions(allSessions);
            });
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

    private void SyncSessions(List<Session> freshSessions)
    {
        var existingIds = Sessions.Select(s => s.Name).ToHashSet();
        var freshIds = freshSessions.Select(s => s.Name).ToHashSet();

        foreach (var session in Sessions.Where(s => !freshIds.Contains(s.Name)).ToList())
        {
            Sessions.Remove(session);
        }

        foreach (var fresh in freshSessions)
        {
            var existing = Sessions.FirstOrDefault(s => s.Name == fresh.Name);
            if (existing == null)
            {
                Sessions.Add(fresh);
            }
            else
            {
                var idx = Sessions.IndexOf(existing);
                Sessions[idx] = fresh;
                if (SelectedSession?.Name == fresh.Name)
                {
                    SelectedSession = fresh;
                }
            }
        }
    }

    partial void OnSelectedSessionChanged(Session? value)
    {
        _pollingSubscription?.Dispose();
        _loadedActivityIds.Clear();
        _dispatcher.TryEnqueue(() =>
        {
            Activities.Clear();
            DiffFiles.Clear();
            FlattenedDiff.Clear();
            AggregatePatch = null;
            _lastPatchSignature = string.Empty;
        });

        if (value != null)
        {
            Debug.WriteLine($"[VM] Session selected: {value.Name}");
            _ = LoadActivitiesAsync(value.Name);
            _pollingSubscription = _polling.StartPolling(value.Name, resp =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    bool changed = false;
                    if (resp.Activities != null)
                    {
                        foreach (var activity in resp.Activities.OrderBy(a => a.CreateTime ?? string.Empty))
                        {
                            if (_loadedActivityIds.Add(activity.Name))
                            {
                                if (activity.Originator == "user" && !string.IsNullOrEmpty(activity.UserMessage?.Prompt))
                                {
                                    var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == activity.UserMessage.Prompt);
                                    if (local != null)
                                    {
                                        Activities.Remove(local);
                                        _loadedActivityIds.Remove(local.Name);
                                    }
                                }

                                Debug.WriteLine($"[VM] New activity: {activity.Name} from {activity.Originator}");
                                Activities.Add(activity);
                                changed = true;
                            }
                        }
                        if (changed) UpdateAggregatePatch();
                    }
                });
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

            _dispatcher.TryEnqueue(() =>
            {
                foreach (var activity in allActivities.OrderBy(a => a.CreateTime ?? string.Empty))
                {
                    if (_loadedActivityIds.Add(activity.Name))
                    {
                        if (activity.Originator == "user" && !string.IsNullOrEmpty(activity.UserMessage?.Prompt))
                        {
                            var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == activity.UserMessage.Prompt);
                            if (local != null)
                            {
                                Activities.Remove(local);
                                _loadedActivityIds.Remove(local.Name);
                            }
                        }
                        Activities.Add(activity);
                    }
                }
                UpdateAggregatePatch();
            });
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
            UserMessaged: null,
            Review: null,
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
            _dispatcher.TryEnqueue(() => SelectedSession = updated);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to approve plan: {ex.Message}");
        }
    }

    private string _lastPatchSignature = string.Empty;

    private void UpdateAggregatePatch()
    {
        var allPatches = Activities
            .SelectMany(a => (a.Artifacts ?? new()).Concat(new List<Artifact> { new(a.BashOutput, a.ChangeSet, a.Media, a.PullRequest) }))
            .Select(art => art?.ChangeSet?.GitPatch?.UnidiffPatch)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (allPatches.Count == 0) return;

        var signature = allPatches.Count + ":" + allPatches[^1].Length;
        if (signature == _lastPatchSignature) return;
        _lastPatchSignature = signature;

        var merged = DiffParser.Merge(allPatches);
        var fileTree = DiffParser.BuildFileTree(merged);
        var flattened = DiffParser.Flatten(merged);

        Debug.WriteLine($"[VM] Diff: {allPatches.Count} sources -> {merged.Files.Count} unique files");

        AggregatePatch = merged;
        DiffFiles.Clear();
        foreach (var fileNode in fileTree)
        {
            DiffFiles.Add(new DiffFileViewModel(fileNode));
        }

        FlattenedDiff.Clear();
        foreach (var item in flattened)
        {
            FlattenedDiff.Add(item);
        }
    }

    [RelayCommand]
    public void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    public void Cleanup()
    {
        _pollingSubscription?.Dispose();
    }
}
