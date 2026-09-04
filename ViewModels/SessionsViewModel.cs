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
    // Polls session.State for the open session. The activity feed never carries
    // the session state and does not reliably emit a "session completed" event,
    // so without this refresh the header can sit on "Working…" indefinitely
    // after Jules has actually stopped. The freshest state is kept in
    // _liveSessionState rather than written back onto SelectedSession / the
    // Sessions collection: replacing the selected item churns the ListView's
    // two-way SelectedItem binding, which transiently deselects the session,
    // tears down its bindings and blanks the chat.
    private CancellationTokenSource? _sessionStateCts;
    private string? _liveSessionState;
    private readonly DispatcherQueue _dispatcher;
    private readonly HashSet<string> _loadedActivityIds = new();
    private readonly HashSet<string> _seenArtifactIds = new();
    // Lifecycle lines ("Plan approved", "Session completed") Jules has emitted
    // for this session, so a repeated one isn't shown twice.
    private readonly HashSet<string> _seenSystemEvents = new();
    // Jules re-sends the (cumulative) changeset many times per run. _seenChangePatches
    // skips byte-identical re-sends; _prevFilePatchBodies holds the previous
    // snapshot's per-file diff so each note names only the files that actually
    // changed since then.
    private readonly HashSet<string> _seenChangePatches = new(StringComparer.Ordinal);
    private Dictionary<string, string> _prevFilePatchBodies = new(StringComparer.Ordinal);
    private readonly List<string> _allSessionPatches = new();

    [ObservableProperty]
    private bool _isLoading;

    // True while the open session's history is being paged in, so the chat can
    // show a spinner instead of an empty pane.
    [ObservableProperty]
    private bool _isLoadingActivities;

    [ObservableProperty]
    private Session? _selectedSession;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private ParsedPatch? _aggregatePatch;

    [ObservableProperty]
    private string _diffSummary = string.Empty;

    [ObservableProperty]
    private string _diffAddedLabel = string.Empty;

    [ObservableProperty]
    private string _diffRemovedLabel = string.Empty;

    // Session status indicator (header): a short label plus a kind that the
    // StatusKindToBrushConverter maps to a colour. StatusIsBusy drives the
    // spinner shown while Jules is working.
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _statusKind = "idle";

    [ObservableProperty]
    private bool _statusIsBusy;

    public ObservableCollection<Session> Sessions { get; } = new();
    public ObservableCollection<JulesClient.Models.Activity> Activities { get; } = new();
    public ObservableCollection<DiffFileViewModel> DiffFiles { get; } = new();

    // The centred spinner in the session list is only for the first load: once
    // rows are on screen a refresh is signalled by the header button's ring
    // instead of covering the list the user is already reading.
    public bool ShowSessionsLoading => IsLoading && Sessions.Count == 0;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowSessionsLoading));

    public SessionsViewModel()
    {
        _api = App.Current.Services.GetRequiredService<ICachedJulesApiClient>();
        _polling = App.Current.Services.GetRequiredService<IPollingService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Sessions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowSessionsLoading));
        AppSettings.Changed += OnAppSettingsChanged;
    }

    // A settings change (e.g. "Show progress updates") re-applies the chat
    // filters by rebuilding the open session's activity feed from scratch.
    private void OnAppSettingsChanged()
    {
        var current = SelectedSession;
        if (current != null)
        {
            _dispatcher.TryEnqueue(() => BindSession(current));
        }
    }

    [RelayCommand]
    public async Task RefreshAllDataAsync()
    {
        await _api.InvalidateAllAsync();
        _loadedActivityIds.Clear();
        _seenArtifactIds.Clear();
        _seenSystemEvents.Clear();
        _seenChangePatches.Clear();
        _prevFilePatchBodies = new(StringComparer.Ordinal);
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

            var cap = AppSettings.MaxSessionsShown;
            if (cap > 0 && allSessions.Count > cap)
            {
                allSessions = allSessions.Take(cap).ToList();
            }

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
        var freshIds = freshSessions.Select(s => s.Name).ToHashSet();
        var byName = Sessions.ToDictionary(s => s.Name);

        foreach (var session in Sessions.Where(s => !freshIds.Contains(s.Name)).ToList())
        {
            Sessions.Remove(session);
        }

        foreach (var fresh in freshSessions)
        {
            if (!byName.TryGetValue(fresh.Name, out var existing))
            {
                Sessions.Add(fresh);
            }
            else if (!existing.Equals(fresh))
            {
                // Only swap the row (and force a UI rebuild) when data actually changed.
                Sessions[Sessions.IndexOf(existing)] = fresh;
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
        if (value?.Name == _lastSelectedSessionName)
        {
            // Same session, but its row may have been refreshed with a newer State.
            RecomputeStatus();
            return;
        }
        _lastSelectedSessionName = value?.Name;
        BindSession(value);
    }

    // Tears down the current session's polling/state and (re)loads the given
    // session's activities. Also called when settings change so filters like
    // "Show progress updates" take effect on the open session immediately.
    private void BindSession(Session? value)
    {
        _pollingSubscription?.Dispose();
        _sessionStateCts?.Cancel();
        _sessionStateCts?.Dispose();
        _sessionStateCts = null;
        _liveSessionState = value?.State;
        _loadedActivityIds.Clear();
        _seenArtifactIds.Clear();
        _seenSystemEvents.Clear();
        _seenChangePatches.Clear();
        _prevFilePatchBodies = new(StringComparer.Ordinal);
        _allSessionPatches.Clear();
        _dispatcher.TryEnqueue(() =>
        {
            IsLoadingActivities = value != null;
            Activities.Clear();
            DiffFiles.Clear();
            AggregatePatch = null;
            DiffSummary = string.Empty;
            DiffAddedLabel = string.Empty;
            DiffRemovedLabel = string.Empty;
            _lastPatchSignature = string.Empty;
            RecomputeStatus();
        });

        if (value != null)
        {
            Debug.WriteLine($"[VM] Session selected: {value.Name}");
            _ = LoadActivitiesAsync(value.Name);

            _sessionStateCts = new CancellationTokenSource();
            _ = PollSessionStateAsync(value.Name, _sessionStateCts.Token);

            _pollingSubscription = _polling.StartPolling(value.Name, resp =>
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
                                    ReplaceLocalEcho(processed);
                                    Debug.WriteLine($"[VM] New activity: {processed.Name} from {processed.Originator}");
                                    Activities.Add(processed);
                                    changed = true;
                                }
                            }
                        }
                        if (changed)
                        {
                            UpdateAggregatePatch();
                        }
                    }
                    RecomputeStatus();
                });
            });
        }
    }

    // Re-fetches the open session (once immediately, then on the polling
    // interval) so the header reflects server-side state transitions - most
    // importantly Jules finishing. Uses the uncached client: the cached one
    // holds sessions for minutes, which is exactly the staleness this defeats.
    // Only _liveSessionState is updated; SelectedSession and the Sessions
    // collection are left untouched (see the field comment).
    private async Task PollSessionStateAsync(string sessionId, CancellationToken ct)
    {
        var raw = App.Current.Services.GetRequiredService<IJulesApiClient>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var fresh = await raw.GetSessionAsync(sessionId, ct);
                _dispatcher.TryEnqueue(() =>
                {
                    if (SelectedSession?.Name != sessionId)
                    {
                        return; // selection moved on while the request was in flight
                    }
                    if (!string.Equals(_liveSessionState, fresh.State, StringComparison.Ordinal))
                    {
                        _liveSessionState = fresh.State;
                        RecomputeStatus();
                    }
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VM] Session state poll failed: {ex.Message}");
            }

            try
            {
                var interval = TimeSpan.FromSeconds(Math.Clamp(AppSettings.PollingIntervalSeconds, 3, 120));
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException) { break; }
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
                            ReplaceLocalEcho(processed);
                            Activities.Add(processed);
                        }
                    }
                }
                UpdateAggregatePatch();
                RecomputeStatus();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VM] Failed to load activities: {ex.Message}");
        }
        finally
        {
            // Queued behind the enqueue above, so the spinner only clears once the
            // loaded activities are actually in the list. If the selection moved on
            // while this request was in flight, the newer load owns the spinner.
            _dispatcher.TryEnqueue(() =>
            {
                if (SelectedSession?.Name == sessionId)
                {
                    IsLoadingActivities = false;
                }
            });
        }
    }

    // Derives the header status indicator from the activity feed and, only as a
    // fallback, the session's State string.
    private void RecomputeStatus()
    {
        var s = SelectedSession;
        if (s == null)
        {
            StatusText = string.Empty;
            StatusKind = "idle";
            StatusIsBusy = false;
            return;
        }

        // One pass over the feed to locate the newest terminal event and the
        // newest turn by each side. The indices let us tell a session that has
        // really finished from one that finished and was then resumed by a
        // follow-up message: session.State is only refreshed by an explicit
        // reload, so it can keep reading COMPLETED long after the conversation
        // has moved on, and the previous "walk back and trust State" logic then
        // showed "Completed" while Jules was already working on the follow-up.
        int lastFailedIdx = -1, lastCompletedIdx = -1, lastUserIdx = -1, lastAgentIdx = -1;
        for (int i = 0; i < Activities.Count; i++)
        {
            var act = Activities[i];
            if (act.SessionFailed != null) lastFailedIdx = i;
            if (act.SessionCompleted != null) lastCompletedIdx = i;

            var who = act.EffectiveOriginator;
            if (string.Equals(who, "user", StringComparison.OrdinalIgnoreCase))
            {
                lastUserIdx = i;
            }
            else if (string.Equals(who, "agent", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(who, "review", StringComparison.OrdinalIgnoreCase))
            {
                lastAgentIdx = i;
            }
        }

        // A terminal lifecycle event in the feed is authoritative, but only
        // while it is still the last word. A user message after it means the
        // session has been resumed and Jules is (about to be) working again.
        // Trailing agent messages (a wrap-up summary) do not cancel completion.
        if (lastFailedIdx >= 0 && lastFailedIdx >= lastCompletedIdx && lastFailedIdx > lastUserIdx)
        {
            SetStatus("Failed", "failed");
            return;
        }
        if (lastCompletedIdx >= 0 && lastCompletedIdx > lastUserIdx)
        {
            SetStatus("Completed", "done");
            return;
        }

        // The user has spoken more recently than Jules: Jules is up next.
        if (lastUserIdx >= 0 && lastUserIdx > lastAgentIdx)
        {
            SetStatus("Working…", "working", busy: true);
            return;
        }

        // Detect a resume that session.State has not caught up with: a user turn
        // that lands after every terminal event in the feed *and* after the
        // session row's own UpdateTime. In that case State still describes the
        // world before the follow-up, so its "finished" meanings are stale and
        // Jules is really working. Without a usable UpdateTime we cannot tell,
        // so we trust State (this only needs to hold until the next reload).
        bool sessionResumed =
            lastUserIdx >= 0
            && lastUserIdx > lastCompletedIdx
            && lastUserIdx > lastFailedIdx
            && s.UpdateTime is { } rowTime
            && Activities[lastUserIdx].CreateTime is { } msgTime
            && msgTime > rowTime;

        if (sessionResumed)
        {
            SetStatus("Working…", "working", busy: true);
            return;
        }

        // _liveSessionState is the value from the background session poll (see
        // PollSessionStateAsync); it is fresher than SelectedSession.State, which
        // is only refreshed by a full session-list reload.
        var rawState = _liveSessionState ?? s.State;
        var state = (rawState ?? string.Empty).Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');

        switch (state)
        {
            case "COMPLETED":
            case "COMPLETE":
            case "SUCCEEDED":
            case "SUCCESS":
            case "DONE":
            case "FINISHED":
                SetStatus("Completed", "done");
                return;
            case "FAILED":
            case "FAILURE":
            case "ERROR":
            case "ERRORED":
                SetStatus("Failed", "failed");
                return;
            case "CANCELLED":
            case "CANCELED":
                SetStatus("Cancelled", "idle");
                return;
            case "PAUSED":
            case "SUSPENDED":
                SetStatus("Paused", "idle");
                return;
        }

        if (s.PendingPlan != null)
        {
            SetStatus("Waiting for plan approval", "attention");
            return;
        }

        if (state.Contains("AWAIT") || state.Contains("FEEDBACK")
            || state.Contains("INPUT") || state.Contains("USER"))
        {
            SetStatus("Waiting for your reply", "attention");
            return;
        }

        // ACTIVE / IN_PROGRESS / RUNNING / PLANNING / QUEUED / unknown-but-open.
        SetStatus("Working…", "working", busy: true);
    }

    // When the real user activity arrives from the API, drop the local echo of
    // the same text so the message isn't shown twice. Matches on the rendered
    // text and effective originator, not a specific field, because the API may
    // deliver a user message as either userMessage.prompt or userMessaged.
    private void ReplaceLocalEcho(JulesClient.Models.Activity processed)
    {
        if (!string.Equals(processed.EffectiveOriginator, "user", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var text = NormalizeMessage(processed.DisplayText);
        if (text.Length == 0) return;

        var local = Activities.FirstOrDefault(x =>
            x.Name.StartsWith("local_", StringComparison.Ordinal)
            && NormalizeMessage(x.DisplayText) == text);

        if (local != null)
        {
            Activities.Remove(local);
            _loadedActivityIds.Remove(local.Name);
        }
    }

    private static string NormalizeMessage(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private void SetStatus(string text, string kind, bool busy = false)
    {
        StatusText = text;
        StatusKind = kind;
        StatusIsBusy = busy;
    }

    private JulesClient.Models.Activity? ProcessActivity(JulesClient.Models.Activity a)
    {
        // Always extract patches for the Diff tab, even if the activity or artifact is filtered out of the chat.
        ExtractPatches(a);

        // A compact "**Updated** `foo.cs`" line for the chat. Jules re-sends the
        // cumulative changeset many times; each note names only the files whose
        // diff changed since the previous snapshot, so it reads as a per-step
        // delta rather than the whole growing changeset. A byte-identical
        // re-send, or one where nothing changed, produces no note.
        string? changeSummary = null;
        var changePatch = CollectPatchText(a);
        if (!string.IsNullOrEmpty(changePatch)
            && _seenChangePatches.Add(changePatch.Length + ":" + changePatch.GetHashCode()))
        {
            var bodies = DiffParser.FilePatchBodies(changePatch);
            var changed = bodies
                .Where(x => DiffParser.BodyHasHunks(x.Body)) // skip binary / mode-only entries
                .Where(x => !_prevFilePatchBodies.TryGetValue(x.Path, out var prev) || prev != x.Body)
                .Select(x => x.Path)
                .Distinct()
                .ToList();

            if (changed.Count > 0)
            {
                changeSummary = DiffParser.SummarizeFiles(changed);
            }
            _prevFilePatchBodies = bodies
                .GroupBy(x => x.Path)
                .ToDictionary(g => g.Key, g => g.Last().Body, StringComparer.Ordinal);
        }

        // "Show progress updates" (Settings) off: drop activities whose only
        // payload is Jules' step-by-step narration. Patches were harvested just
        // above, so the Diff tab is unaffected. Progress events carrying a
        // "Code Review" heading are kept - that is how review cards arrive.
        // Root-level Text/Description are treated as part of the narration here
        // (they only feed DisplayText as a last resort); a genuine agent or
        // user *message* keeps the activity visible.
        bool hasRealMessage = !string.IsNullOrWhiteSpace(a.AgentMessage?.Message)
                              || !string.IsNullOrWhiteSpace(a.AgentMessage?.Text)
                              || !string.IsNullOrWhiteSpace(a.AgentMessaged?.AgentMessage)
                              || !string.IsNullOrWhiteSpace(a.UserMessage?.Prompt)
                              || !string.IsNullOrWhiteSpace(a.UserMessage?.Text)
                              || !string.IsNullOrWhiteSpace(a.UserMessaged?.UserMessage);

        if (!AppSettings.ShowProgressUpdates
            && a.ProgressUpdated?.HasData == true
            && !a.IsReview
            && !hasRealMessage
            && a.PlanGenerated?.HasData != true
            && a.SessionFailed == null
            && a.SessionCompleted == null
            && a.PlanApproved == null
            && a.BashOutput == null
            && a.Media == null
            && a.PullRequest == null
            && a.ChangeSet == null
            && (a.Artifacts == null || a.Artifacts.Count == 0))
        {
            return null;
        }

        // Jules can emit the same lifecycle line more than once (e.g. two
        // "Plan approved" activities with different ids). Render it only once
        // per plan / per session.
        if (a.IsSystemEvent)
        {
            var key = a.SystemEventText + "|" + (a.PlanApproved?.PlanId ?? string.Empty);
            if (!_seenSystemEvents.Add(key))
            {
                return null;
            }
        }

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
                                 !string.IsNullOrEmpty(changeSummary) ||
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

        var result = a with
        {
            Artifacts = flatArts.Count > 0 ? flatArts : null,
            BashOutput = null,
            ChangeSet = null,
            Media = null,
            PullRequest = null
        };
        result.ChangeSummary = changeSummary;
        return result;
    }

    private void ExtractPatches(JulesClient.Models.Activity a)
    {
        foreach (var p in ActivityPatches(a))
        {
            _allSessionPatches.Add(p);
        }
    }

    private static IEnumerable<string> ActivityPatches(JulesClient.Models.Activity a)
    {
        var patch = a.ChangeSet?.GitPatch?.UnidiffPatch;
        if (!string.IsNullOrEmpty(patch)) yield return patch;

        if (a.Artifacts != null)
        {
            foreach (var art in a.Artifacts)
            {
                var p = art.ChangeSet?.GitPatch?.UnidiffPatch;
                if (!string.IsNullOrEmpty(p)) yield return p;
            }
        }
    }

    private static string CollectPatchText(JulesClient.Models.Activity a) =>
        string.Join("\n", ActivityPatches(a));

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(ChatInput)) return;

        if (AppSettings.ConfirmBeforeSend && !await ConfirmSendAsync()) return;

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
        RecomputeStatus();

        try { await _api.SendMessageAsync(SelectedSession.Name, msg); }
        catch (Exception ex) { Debug.WriteLine($"[VM] Failed to send message: {ex.Message}"); }
    }

    private static async Task<bool> ConfirmSendAsync()
    {
        try
        {
            var root = (App.MainWindow?.Content as Microsoft.UI.Xaml.FrameworkElement)?.XamlRoot;
            if (root == null) return true;

            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Send message?",
                Content = "Send this message to Jules?",
                PrimaryButtonText = "Send",
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                XamlRoot = root
            };
            return await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary;
        }
        catch { return true; }
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

    private static readonly System.Text.Json.JsonSerializerOptions _prettyJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [RelayCommand]
    public void CopySessionJson()
    {
        var s = SelectedSession;
        if (s == null) return;

        // Prefer the raw payload captured from the API; fall back to serialising
        // the parsed model so the button still does something if RawInfo was
        // never populated.
        var text = !string.IsNullOrWhiteSpace(s.RawInfo)
            ? s.RawInfo
            : System.Text.Json.JsonSerializer.Serialize(s, _prettyJson);
        CopyToClipboard(text);
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

        AggregatePatch = merged;

        DiffFiles.Clear();
        int added = 0, removed = 0;
        // Auto-open a diff that is a single, reasonably small file; otherwise
        // start collapsed and let the per-file badges show the shape.
        bool autoExpand = AppSettings.DiffAutoExpandSingleFile
                          && fileTree.Count == 1
                          && fileTree[0].TotalLines <= AppSettings.DiffAutoExpandMaxLines;
        foreach (var fileNode in fileTree)
        {
            added += fileNode.AddedLines;
            removed += fileNode.RemovedLines;
            DiffFiles.Add(new DiffFileViewModel(fileNode) { IsExpanded = autoExpand });
        }

        DiffSummary = fileTree.Count == 1 ? "1 file changed" : $"{fileTree.Count} files changed";
        DiffAddedLabel = $"+{added}";
        DiffRemovedLabel = $"−{removed}"; // U+2212
    }

    [RelayCommand]
    public void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        // Flush so the text stays on the clipboard after the app exits.
        try { Windows.ApplicationModel.DataTransfer.Clipboard.Flush(); } catch { }
    }

    public void Cleanup()
    {
        _pollingSubscription?.Dispose();
        _sessionStateCts?.Cancel();
        _sessionStateCts?.Dispose();
        _sessionStateCts = null;
        AppSettings.Changed -= OnAppSettingsChanged;
    }
}
