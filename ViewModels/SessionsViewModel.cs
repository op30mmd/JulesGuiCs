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
    private readonly HashSet<string> _seenArtifactIds = new();
    private readonly List<string> _allSessionPatches = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePullRequest))]
    private Session? _selectedSession;

    public PullRequest? ActivePullRequest
    {
        get
        {
            var pr = SelectedSession?.Outputs?.FirstOrDefault(o => o.PullRequest != null)?.PullRequest;
            if (pr != null) return pr;

            foreach (var a in Activities)
            {
                if (a.PullRequest != null) return a.PullRequest;
                if (a.Artifacts != null)
                {
                    var artPr = a.Artifacts.FirstOrDefault(art => art.PullRequest != null)?.PullRequest;
                    if (artPr != null) return artPr;
                }
            }
            return null;
        }
    }

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private bool _isPollingEnabled = true;

    [ObservableProperty]
    private ParsedPatch? _aggregatePatch;

    [ObservableProperty]
    private bool _isSlowConnection;

    public ObservableCollection<Session> Sessions { get; } = new();
    public ObservableCollection<JulesClient.Models.Activity> Activities { get; } = new();
    public ObservableCollection<DiffFileViewModel> DiffFiles { get; } = new();
    public ObservableCollection<DiffDisplayItem> FlattenedDiff { get; } = new();

    public SessionsViewModel()
    {
        _api = App.Current.Services.GetRequiredService<ICachedJulesApiClient>();
        _polling = App.Current.Services.GetRequiredService<IPollingService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Simple polling to update the slow connection indicator
        _dispatcher.TryEnqueue(() =>
        {
            var timer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => IsSlowConnection = _api.IsSlowConnection;
            timer.Start();
        });
    }

    [RelayCommand]
    public void TogglePolling()
    {
        IsPollingEnabled = !IsPollingEnabled;
        if (SelectedSession != null)
        {
            if (IsPollingEnabled)
            {
                StartPolling(SelectedSession.Name);
            }
            else
            {
                _pollingSubscription?.Dispose();
                _pollingSubscription = null;
            }
        }
    }

    [RelayCommand]
    public async Task RefreshAllDataAsync()
    {
        await _api.InvalidateAllAsync();
        _loadedActivityIds.Clear();
        _seenArtifactIds.Clear();
        _allSessionPatches.Clear();
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
                if (response.Sessions != null)
                {
                    allSessions.AddRange(response.Sessions);
                }
                pageToken = response.NextPageToken;
            }
            while (pageToken != null);

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

    private string? _lastSelectedSessionName;

    partial void OnSelectedSessionChanged(Session? value)
    {
        if (value?.Name == _lastSelectedSessionName) return;
        _lastSelectedSessionName = value?.Name;

        _pollingSubscription?.Dispose();
        _loadedActivityIds.Clear();
        _seenArtifactIds.Clear();
        _allSessionPatches.Clear();
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
            IsPollingEnabled = true;
            StartPolling(value.Name);
        }
    }

    private void StartPolling(string sessionId)
    {
        _pollingSubscription?.Dispose();
        _pollingSubscription = _polling.StartPolling(sessionId, resp =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                bool changed = false;
                if (resp.Activities != null)
                {
                    foreach (var activity in resp.Activities.OrderBy(a => a.CreateTime ?? DateTime.MinValue))
                    {
                        if (_loadedActivityIds.Add(activity.Name))
                        {
                            var processed = ProcessActivity(activity);
                            if (processed != null)
                            {
                                if (processed.Originator == "user" && !string.IsNullOrEmpty(processed.UserMessage?.Prompt))
                                {
                                    var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == processed.UserMessage.Prompt);
                                    if (local != null)
                                    {
                                        Activities.Remove(local);
                                        _loadedActivityIds.Remove(local.Name);
                                    }
                                }

                                Debug.WriteLine($"[VM] New activity: {processed.Name} from {processed.Originator}");
                                Activities.Add(processed);
                                changed = true;

                                if (processed.PullRequest != null || processed.Artifacts?.Any(art => art.PullRequest != null) == true)
                                {
                                    OnPropertyChanged(nameof(ActivePullRequest));
                                }
                            }
                        }
                    }
                    if (changed)
                    {
                        UpdateAggregatePatch();
                    }
                }
            });
        });
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
                if (response.Activities != null)
                {
                    allActivities.AddRange(response.Activities);
                }
                pageToken = response.NextPageToken;
            }
            while (pageToken != null);

            _dispatcher.TryEnqueue(() =>
            {
                foreach (var activity in allActivities.OrderBy(a => a.CreateTime ?? DateTime.MinValue))
                {
                    if (_loadedActivityIds.Add(activity.Name))
                    {
                        var processed = ProcessActivity(activity);
                        if (processed != null)
                        {
                            if (processed.Originator == "user" && !string.IsNullOrEmpty(processed.UserMessage?.Prompt))
                            {
                                var local = Activities.FirstOrDefault(a => a.Name.StartsWith("local_") && a.UserMessage?.Prompt == processed.UserMessage.Prompt);
                                if (local != null)
                                {
                                    Activities.Remove(local);
                                    _loadedActivityIds.Remove(local.Name);
                                }
                            }
                            Activities.Add(processed);
                        }
                    }
                }
                OnPropertyChanged(nameof(ActivePullRequest));
                UpdateAggregatePatch();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to load activities: {ex.Message}");
        }
    }

    private JulesClient.Models.Activity? ProcessActivity(JulesClient.Models.Activity a)
    {
        // Always extract patches for the Diff tab, even if the activity or artifact is filtered out of the chat.
        ExtractPatches(a);

        var flatArts = new List<Artifact>();

        void AddIfUnique(Artifact art)
        {
            bool isUnique = false;

            if (art.PullRequest?.HasData == true && !string.IsNullOrEmpty(art.PullRequest.Url))
            {
                if (_seenArtifactIds.Add(art.PullRequest.Url))
                {
                    isUnique = true;
                }
            }
            else if (art.BashOutput?.HasData == true)
            {
                var sig = $"bash_{art.BashOutput.Command}_{art.BashOutput.Output}";
                if (_seenArtifactIds.Add(sig))
                {
                    isUnique = true;
                }
            }
            else if (art.Media?.HasData == true)
            {
                var sig = $"media_{art.Media.Data?.GetHashCode()}";
                if (_seenArtifactIds.Add(sig))
                {
                    isUnique = true;
                }
            }

            if (isUnique)
            {
                flatArts.Add(art);
            }
        }

        // Unpack root artifacts
        if (a.BashOutput != null) AddIfUnique(new Artifact(BashOutput: a.BashOutput));
        if (a.Media != null) AddIfUnique(new Artifact(Media: a.Media));
        if (a.PullRequest != null) AddIfUnique(new Artifact(PullRequest: a.PullRequest));

        // Unpack nested artifacts
        if (a.Artifacts != null)
        {
            foreach (var art in a.Artifacts)
            {
                if (art.BashOutput != null) AddIfUnique(new Artifact(BashOutput: art.BashOutput));
                if (art.Media != null) AddIfUnique(new Artifact(Media: art.Media));
                if (art.PullRequest != null) AddIfUnique(new Artifact(PullRequest: art.PullRequest));
            }
        }

        bool hasUniqueContent = !string.IsNullOrWhiteSpace(a.DisplayText) ||
                                 a.ProgressUpdated?.HasData == true ||
                                 a.PlanGenerated?.HasData == true ||
                                 a.SessionFailed != null ||
                                 a.SessionCompleted != null ||
                                 a.PlanApproved != null ||
                                 flatArts.Count > 0;

        if (!hasUniqueContent)
        {
            return null;
        }

        return a with
        {
            Artifacts = flatArts.Count > 0 ? flatArts : null,
            BashOutput = null,
            ChangeSet = null,
            Media = null,
            PullRequest = null
        };
    }

    private void ExtractPatches(JulesClient.Models.Activity a)
    {
        var patch = a.ChangeSet?.GitPatch?.UnidiffPatch;
        if (!string.IsNullOrEmpty(patch)) _allSessionPatches.Add(patch);

        if (a.Artifacts != null)
        {
            foreach (var art in a.Artifacts)
            {
                var p = art.ChangeSet?.GitPatch?.UnidiffPatch;
                if (!string.IsNullOrEmpty(p)) _allSessionPatches.Add(p);
            }
        }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(ChatInput)) return;
        var msg = ChatInput;
        ChatInput = string.Empty;

        var localMsg = new JulesClient.Models.Activity(
            Name: $"local_{Guid.NewGuid()}", Id: null, CreateTime: DateTime.UtcNow, Originator: "user",
            ProgressUpdated: null, PlanGenerated: null, PlanApproved: null, SessionCompleted: null, SessionFailed: null,
            BashOutput: null, ChangeSet: null, Media: null, PullRequest: null, Artifacts: null,
            UserMessage: new UserMessage(Prompt: msg, Text: null), AgentMessage: null, UserMessaged: null, Review: null,
            Text: null, Prompt: null, Description: null
        );
        Activities.Add(localMsg);

        try { await _api.SendMessageAsync(SelectedSession.Name, msg); }
        catch (Exception ex) { Debug.WriteLine($"[VM] Failed to send message: {ex.Message}"); }
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
        catch (Exception ex) { Debug.WriteLine($"[VM] Failed to approve plan: {ex.Message}"); }
    }

    [RelayCommand]
    public void CopySessionJson()
    {
        if (SelectedSession?.RawInfo != null) CopyToClipboard(SelectedSession.RawInfo);
    }

    private string _lastPatchSignature = string.Empty;

    private void UpdateAggregatePatch()
    {
        if (_allSessionPatches.Count == 0) return;

        var signature = _allSessionPatches.Count + ":" + _allSessionPatches[^1].Length;
        if (signature == _lastPatchSignature) return;
        _lastPatchSignature = signature;

        var merged = DiffParser.Merge(_allSessionPatches);
        var fileTree = DiffParser.BuildFileTree(merged);
        var flattened = DiffParser.Flatten(merged);

        AggregatePatch = merged;
        DiffFiles.Clear();
        foreach (var fileNode in fileTree)
        {
            var fileVm = new DiffFileViewModel(fileNode);
            fileVm.LoadHunks();
            DiffFiles.Add(fileVm);
        }

        FlattenedDiff.Clear();
        foreach (var item in flattened) FlattenedDiff.Add(item);
    }

    [RelayCommand]
    public void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    public void Cleanup() => _pollingSubscription?.Dispose();
}
