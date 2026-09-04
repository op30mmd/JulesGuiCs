using System.Text.Json.Serialization;

namespace JulesClient.Models;

public record SourceListResponse(
    [property: JsonPropertyName("sources")] List<Source>? Sources = null,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken = null
);

public record Source(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("githubRepo")] GitHubRepo? GitHubRepo = null,
    [property: JsonPropertyName("createTime")] DateTime? CreateTime = null,
    [property: JsonPropertyName("updateTime")] DateTime? UpdateTime = null
);

public record GitHubRepo(
    [property: JsonPropertyName("owner")] string? Owner = null,
    [property: JsonPropertyName("repo")] string? Repo = null,
    [property: JsonPropertyName("isPrivate")] bool? IsPrivate = null,
    [property: JsonPropertyName("defaultBranch")] GitHubBranch? DefaultBranch = null,
    [property: JsonPropertyName("branches")] List<GitHubBranch>? Branches = null
);

public record GitHubBranch(
    [property: JsonPropertyName("displayName")] string? DisplayName = null
);

public record SessionListResponse(
    [property: JsonPropertyName("sessions")] List<Session>? Sessions = null,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken = null
);

public record Session(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("sourceContext")] SourceContext? SourceContext = null,
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("createTime")] DateTime? CreateTime = null,
    [property: JsonPropertyName("updateTime")] DateTime? UpdateTime = null,
    [property: JsonPropertyName("state")] string? State = null,
    [property: JsonPropertyName("plan")] Plan? Plan = null,
    [property: JsonPropertyName("pendingPlan")] Plan? PendingPlan = null,
    [property: JsonPropertyName("outputs")] List<SessionOutput>? Outputs = null,
    [property: JsonPropertyName("requirePlanApproval")] bool? RequirePlanApproval = null
)
{
    public string ShortId => Name?.Replace("sessions/", "") ?? string.Empty;

    // What the session list rows show. Binding the raw Title/Prompt is what made
    // that list so expensive to lay out: the rows trim with
    // TextTrimming="CharacterEllipsis" and don't wrap, so the text engine has to
    // measure the whole string to work out where the ellipsis goes - and a Jules
    // prompt routinely runs to thousands of characters, of which a row shows a
    // few dozen. Cutting to one line first makes a row's measure cost the length
    // of that line instead of the length of the prompt, which is what a resize of
    // the pane was paying for on every visible row.
    //
    // Computed rather than cached on purpose: a cache field would join the
    // record's generated equality, and SyncSessions compares rows with Equals to
    // decide whether to swap them.
    [JsonIgnore]
    public string ListTitle => FirstLine(Title);

    [JsonIgnore]
    public string ListSubtitle => FirstLine(Prompt);

    // Generously longer than a row can ever display, so the TextBlock still gets
    // to place the ellipsis itself.
    private const int ListLineMaxChars = 160;

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var line = text.AsSpan().TrimStart();
        int lineBreak = line.IndexOfAny('\r', '\n');
        if (lineBreak >= 0)
        {
            line = line[..lineBreak];
        }

        line = line.TrimEnd();
        return line.Length <= ListLineMaxChars
            ? line.ToString()
            : line[..ListLineMaxChars].ToString();
    }

    // Raw JSON of this session as received from the API. Deliberately NOT
    // [JsonIgnore]: it has to survive a round-trip through the disk cache so the
    // "Copy session JSON" button works on a cache hit, not just a fresh fetch.
    public string? RawInfo { get; set; }

    // The starting branch for the session header. Jules often leaves
    // sourceContext.githubRepoContext.startingBranch empty (it used the repo
    // default); the PR's base ref is that same starting branch.
    [JsonIgnore]
    public string? DisplayBranch
    {
        get
        {
            var starting = SourceContext?.StartingBranch;
            if (!string.IsNullOrWhiteSpace(starting)) return starting;

            var baseRef = Outputs?
                .Select(o => o.PullRequest?.BaseRef)
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            return string.IsNullOrWhiteSpace(baseRef) ? null : baseRef;
        }
    }

    // The pull request Jules opened for this session (from session outputs).
    [JsonIgnore]
    public PullRequest? PrimaryPullRequest =>
        Outputs?.Select(o => o.PullRequest).FirstOrDefault(pr => pr?.HasData == true);

    // "PR #6" from the PR url, or a generic label.
    [JsonIgnore]
    public string? PullRequestLabel
    {
        get
        {
            var url = PrimaryPullRequest?.Url;
            if (string.IsNullOrWhiteSpace(url)) return null;
            var last = url.TrimEnd('/').Split('/').LastOrDefault();
            return int.TryParse(last, out var n) ? $"PR #{n}" : "Pull request";
        }
    }
}

public record SourceContext(
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("githubRepoContext")] GitHubRepoContext? GitHubRepoContext = null,
    [property: JsonPropertyName("environmentVariablesEnabled")] bool? EnvironmentVariablesEnabled = null
)
{
    [JsonIgnore]
    public string? StartingBranch => GitHubRepoContext?.StartingBranch;
}

public record GitHubRepoContext(
    [property: JsonPropertyName("startingBranch")] string? StartingBranch = null
);

public record SessionOutput(
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest = null,
    [property: JsonPropertyName("changeSet")] ChangeSet? ChangeSet = null
);

public record PullRequest(
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("baseRef")] string? BaseRef = null,
    [property: JsonPropertyName("headRef")] string? HeadRef = null
)
{
    [JsonIgnore]
    public bool HasData => !string.IsNullOrWhiteSpace(Url);
}

public record CreateSessionRequest(
    [property: JsonPropertyName("sourceContext")] SourceContext SourceContext,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("requirePlanApproval")] bool RequirePlanApproval = false,
    [property: JsonPropertyName("automationMode")] string? AutomationMode = null,
    [property: JsonPropertyName("title")] string? Title = null
);

public record Plan(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("steps")] List<PlanStep>? Steps = null
);

public record PlanStep(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("index")] int? Index = null
);

public record ApprovePlanResponse();

public record ActivityListResponse(
    [property: JsonPropertyName("activities")] List<Activity>? Activities = null,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken = null
);

public record Activity(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("createTime")] DateTime? CreateTime = null,
    [property: JsonPropertyName("originator")] string? Originator = null,
    [property: JsonPropertyName("progressUpdated")] ProgressUpdated? ProgressUpdated = null,
    [property: JsonPropertyName("planGenerated")] PlanGenerated? PlanGenerated = null,
    [property: JsonPropertyName("planApproved")] PlanApproved? PlanApproved = null,
    [property: JsonPropertyName("sessionCompleted")] object? SessionCompleted = null,
    [property: JsonPropertyName("sessionFailed")] SessionFailed? SessionFailed = null,
    [property: JsonPropertyName("bashOutput")] BashOutput? BashOutput = null,
    [property: JsonPropertyName("changeSet")] ChangeSet? ChangeSet = null,
    [property: JsonPropertyName("media")] Media? Media = null,
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest = null,
    [property: JsonPropertyName("artifacts")] List<Artifact>? Artifacts = null,
    [property: JsonPropertyName("userMessage")] UserMessage? UserMessage = null,
    [property: JsonPropertyName("agentMessage")] AgentMessage? AgentMessage = null,
    [property: JsonPropertyName("userMessaged")] UserMessaged? UserMessaged = null,
    [property: JsonPropertyName("agentMessaged")] AgentMessaged? AgentMessaged = null,
    [property: JsonPropertyName("review")] Review? Review = null,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("updateTime")] DateTime? UpdateTime = null,
    [property: JsonPropertyName("title")] string? Title = null
)
{
    [JsonIgnore]
    private string? _rawInfo;

    [JsonIgnore]
    private string? _cachedOriginator;

    [JsonIgnore]
    private string? _cachedDisplayText;

    [JsonIgnore]
    private bool? _cachedHasContent;

    [JsonIgnore]
    private bool? _cachedIsReview;

    // Raw JSON of this activity as received from the API. Deliberately NOT
    // [JsonIgnore]: it must survive the disk cache so the per-message "Raw JSON"
    // panel (Verbose logging) works on a cache hit, not just a fresh fetch.
    public string? RawInfo
    {
        get => _rawInfo;
        set
        {
            _rawInfo = value;
            _cachedOriginator = null;
            _cachedDisplayText = null;
            _cachedHasContent = null;
            _cachedIsReview = null;
        }
    }

    [JsonIgnore]
    public string? EffectiveOriginator
    {
        get
        {
            if (_cachedOriginator != null)
            {
                return _cachedOriginator;
            }

            if (IsReview)
            {
                return _cachedOriginator = "review";
            }

            bool hasAgentContent = !string.IsNullOrWhiteSpace(AgentMessage?.Message) ||
                                   !string.IsNullOrWhiteSpace(AgentMessage?.Text) ||
                                   !string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage) ||
                                   !string.IsNullOrWhiteSpace(Review?.Summary) ||
                                   !string.IsNullOrWhiteSpace(SessionFailed?.Reason) ||
                                   PlanApproved != null ||
                                   SessionCompleted != null ||
                                   ProgressUpdated?.HasData == true ||
                                   PlanGenerated?.HasData == true ||
                                   BashOutput?.HasData == true ||
                                   ChangeSet?.HasData == true ||
                                   Media?.HasData == true ||
                                   PullRequest?.HasData == true ||
                                   Artifacts?.Any(a => a.HasData) == true;

            if (hasAgentContent)
            {
                return _cachedOriginator = "agent";
            }

            if (!string.IsNullOrWhiteSpace(UserMessage?.Prompt) ||
                !string.IsNullOrWhiteSpace(UserMessage?.Text) ||
                !string.IsNullOrWhiteSpace(UserMessaged?.UserMessage))
            {
                return _cachedOriginator = "user";
            }

            return _cachedOriginator = Originator;
        }
    }

    [JsonIgnore]
    public bool IsDuplicateUserMessage
    {
        get
        {
            bool hasUserContent = !string.IsNullOrWhiteSpace(UserMessage?.Prompt) ||
                                  !string.IsNullOrWhiteSpace(UserMessage?.Text) ||
                                  !string.IsNullOrWhiteSpace(UserMessaged?.UserMessage);

            bool hasAgentContent = !string.IsNullOrWhiteSpace(AgentMessage?.Message) ||
                                   !string.IsNullOrWhiteSpace(AgentMessage?.Text) ||
                                   !string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage) ||
                                   !string.IsNullOrWhiteSpace(Review?.Summary) ||
                                   !string.IsNullOrWhiteSpace(SessionFailed?.Reason) ||
                                   PlanApproved != null ||
                                   SessionCompleted != null ||
                                   ProgressUpdated?.HasData == true ||
                                   PlanGenerated?.HasData == true ||
                                   BashOutput?.HasData == true ||
                                   ChangeSet?.HasData == true ||
                                   Media?.HasData == true ||
                                   PullRequest?.HasData == true ||
                                   Artifacts?.Any(a => a.HasData) == true;

            return hasUserContent && !hasAgentContent && !string.Equals(Originator, "user", StringComparison.OrdinalIgnoreCase);
        }
    }

    [JsonIgnore]
    public string? DisplayText
    {
        get
        {
            if (_cachedDisplayText != null)
            {
                return _cachedDisplayText;
            }

            bool hasAgentContent = !string.IsNullOrWhiteSpace(AgentMessage?.Message) ||
                                   !string.IsNullOrWhiteSpace(AgentMessage?.Text) ||
                                   !string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage) ||
                                   !string.IsNullOrWhiteSpace(Review?.Summary) ||
                                   !string.IsNullOrWhiteSpace(SessionFailed?.Reason) ||
                                   !string.IsNullOrWhiteSpace(Text) ||
                                   !string.IsNullOrWhiteSpace(Description) ||
                                   PlanApproved != null ||
                                   SessionCompleted != null;

            if (hasAgentContent)
            {
                if (!string.IsNullOrWhiteSpace(AgentMessage?.Message))
                {
                    return _cachedDisplayText = AgentMessage.Message;
                }

                if (!string.IsNullOrWhiteSpace(AgentMessage?.Text))
                {
                    return _cachedDisplayText = AgentMessage.Text;
                }

                if (!string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage))
                {
                    return _cachedDisplayText = AgentMessaged.AgentMessage;
                }

                if (!string.IsNullOrWhiteSpace(Review?.Summary))
                {
                    return _cachedDisplayText = Review.Summary;
                }

                if (!string.IsNullOrWhiteSpace(SessionFailed?.Reason))
                {
                    return _cachedDisplayText = SessionFailed.Reason;
                }

                if (!string.IsNullOrWhiteSpace(Text))
                {
                    return _cachedDisplayText = Text;
                }

                if (!string.IsNullOrWhiteSpace(Description))
                {
                    return _cachedDisplayText = Description;
                }

                if (PlanApproved != null)
                {
                    return _cachedDisplayText = "Plan Approved";
                }

                if (SessionCompleted != null)
                {
                    return _cachedDisplayText = "Session Completed";
                }
            }

            bool isUser = string.Equals(Originator, "user", StringComparison.OrdinalIgnoreCase);
            if (isUser)
            {
                if (!string.IsNullOrWhiteSpace(UserMessage?.Prompt))
                {
                    return _cachedDisplayText = UserMessage.Prompt;
                }

                if (!string.IsNullOrWhiteSpace(UserMessage?.Text))
                {
                    return _cachedDisplayText = UserMessage.Text;
                }

                if (!string.IsNullOrWhiteSpace(UserMessaged?.UserMessage))
                {
                    return _cachedDisplayText = UserMessaged.UserMessage;
                }
            }

            if (!string.IsNullOrWhiteSpace(Text))
            {
                return _cachedDisplayText = Text;
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                return _cachedDisplayText = Description;
            }

            return _cachedDisplayText = null;
        }
    }

    [JsonIgnore]
    public bool HasContent
    {
        get
        {
            if (_cachedHasContent.HasValue)
            {
                return _cachedHasContent.Value;
            }

            bool result;
            if (IsDuplicateUserMessage)
            {
                result = false;
            }
            else if (!string.IsNullOrWhiteSpace(DisplayText))
            {
                result = true;
            }
            else if (ProgressUpdated?.HasData == true)
            {
                result = true;
            }
            else if (PlanGenerated?.HasData == true)
            {
                result = true;
            }
            else if (Artifacts?.Any(a => a.HasData) == true)
            {
                result = true;
            }
            else if (PlanApproved != null || SessionCompleted != null || SessionFailed != null)
            {
                result = true;
            }
            else if (BashOutput?.HasData == true || ChangeSet?.HasData == true || Media?.HasData == true || PullRequest?.HasData == true)
            {
                result = true;
            }
            else
            {
                result = false;
            }

            _cachedHasContent = result;
            return result;
        }
    }

    [JsonIgnore]
    public bool HasDebugInfo => !string.IsNullOrWhiteSpace(RawInfo);

    // The chat's DisplayText for this activity is one of Jules' own chat
    // messages (not a plan, review or progress block). When "Collapse long
    // agent messages" is on, the presenter folds it if it is long enough.
    [JsonIgnore]
    public bool CollapseAgentMessage =>
        JulesClient.Services.AppSettings.CollapseAgentMessages
        && string.Equals(EffectiveOriginator, "agent", StringComparison.OrdinalIgnoreCase)
        && !IsReview
        && ProgressUpdated?.HasData != true
        && PlanGenerated?.HasData != true;

    // The same for the user's own messages (e.g. a pasted log), gated on the
    // "Collapse long user messages" setting.
    [JsonIgnore]
    public bool CollapseUserMessage =>
        JulesClient.Services.AppSettings.CollapseUserMessages
        && string.Equals(EffectiveOriginator, "user", StringComparison.OrdinalIgnoreCase);

    // Whether the chat presenter should offer a "Show more" fold for this
    // message's body. The length check itself lives in the presenter.
    [JsonIgnore]
    public bool CollapseMessage => CollapseAgentMessage || CollapseUserMessage;

    // Chat shows a per-message "Raw JSON" expander only when Verbose logging
    // (Settings > Diagnostics) is on and the raw payload was captured.
    [JsonIgnore]
    public bool ShowRawJson => JulesClient.Services.AppSettings.VerboseLogging && HasDebugInfo;

    private static readonly char[] _headingTrimChars =
        { ' ', '\t', '\r', '\n', ':', '.', '!', '?', '-', '–', '—', '#', '_', '*' };

    // True only when a string, on its own, reads as the review banner - "Code
    // Review" or "Code reviewed" - after stripping surrounding punctuation. A
    // sentence that merely contains the phrase (e.g. "let's request a code
    // review") does not qualify.
    private static bool IsCodeReviewHeading(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim(_headingTrimChars);
        return trimmed.Equals("Code Review", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Code Reviewed", StringComparison.OrdinalIgnoreCase);
    }

    [JsonIgnore]
    public bool IsReview
    {
        get
        {
            if (_cachedIsReview.HasValue)
            {
                return _cachedIsReview.Value;
            }

            // A structured review object is definitive.
            if (Review != null)
            {
                _cachedIsReview = true;
                return true;
            }

            // Otherwise a review is identified only by an activity/progress
            // heading that literally reads "Code Review" / "Code reviewed".
            // Scanning message bodies, or matching a bare "review"/"feedback",
            // flagged ordinary messages that just talk about requesting a review.
            bool headingSaysReview = IsCodeReviewHeading(ProgressUpdated?.Title) || IsCodeReviewHeading(Title);

            // A plain agent/user chat turn with no review payload is discussing a
            // review, not delivering one - real reviews arrive as a Review object
            // or a ProgressUpdated event.
            bool isPlainMessage = ProgressUpdated == null &&
                (!string.IsNullOrWhiteSpace(AgentMessage?.Message) ||
                 !string.IsNullOrWhiteSpace(AgentMessage?.Text) ||
                 !string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage) ||
                 !string.IsNullOrWhiteSpace(UserMessage?.Prompt) ||
                 !string.IsNullOrWhiteSpace(UserMessage?.Text) ||
                 !string.IsNullOrWhiteSpace(UserMessaged?.UserMessage));

            var result = headingSaysReview && !isPlainMessage;

            _cachedIsReview = result;
            return result;
        }
    }

    [JsonIgnore]
    public string? ReviewDisplayTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                return Title;
            }

            if (!string.IsNullOrWhiteSpace(ProgressUpdated?.Title))
            {
                return ProgressUpdated.Title;
            }

            if (Review?.Summary != null)
            {
                return "Code Review";
            }

            return "Code Review";
        }
    }

    // Resolves the Markdown text to render inside the Code Review card
    [JsonIgnore]
    public string? ReviewDisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Review?.Summary))
            {
                return Review.Summary;
            }

            // Only pull from ProgressUpdated if this activity has been confirmed as a Review
            if (IsReview && !string.IsNullOrWhiteSpace(ProgressUpdated?.Description))
            {
                return ProgressUpdated.Description;
            }

            return DisplayText;
        }
    }

    [JsonIgnore]
    public bool ShowProgress => ProgressUpdated?.HasData == true;

    // Prevents double-rendering progress text in standard bubbles if it's already a Review
    [JsonIgnore]
    public bool ShowProgressBlock => ShowProgress && !IsReview;

    [JsonIgnore]
    public bool ShowPlan => PlanGenerated?.HasData == true;

    // Markdown one-liner ("**Updated** `a` and `b`") for a changeset activity,
    // filled in by the view model from the diff (the full diff is the Diff tab).
    [JsonIgnore]
    public string? ChangeSummary { get; set; }

    // Short status label for lifecycle events that should render as a centred
    // system line rather than a chat bubble.
    [JsonIgnore]
    public string? SystemEventText =>
        PlanApproved != null ? "Plan approved" :
        SessionCompleted != null ? "Session completed" :
        null;

    // True when the activity is *only* such an event - no agent message,
    // progress, plan, review or artifacts riding along with it.
    [JsonIgnore]
    public bool IsSystemEvent =>
        SystemEventText != null
        && !IsReview
        && ProgressUpdated?.HasData != true
        && PlanGenerated?.HasData != true
        && SessionFailed == null
        && string.IsNullOrWhiteSpace(AgentMessage?.Message)
        && string.IsNullOrWhiteSpace(AgentMessage?.Text)
        && string.IsNullOrWhiteSpace(AgentMessaged?.AgentMessage)
        && string.IsNullOrWhiteSpace(Text)
        && string.IsNullOrWhiteSpace(Description)
        && (Artifacts == null || Artifacts.Count == 0);

    // A stand-alone "Updated `file` ..." note - the activity carried only a
    // changeset, so it renders as its own left-aligned line.
    [JsonIgnore]
    public bool IsChangeNote =>
        !string.IsNullOrEmpty(ChangeSummary)
        && !IsSystemEvent
        && !IsReview
        && string.IsNullOrWhiteSpace(DisplayText)
        && ProgressUpdated?.HasData != true
        && PlanGenerated?.HasData != true
        && (Artifacts == null || Artifacts.Count == 0);

    // User preferences (Settings) - the chat templates bind visibility to these.
    [JsonIgnore]
    public bool ShowTimestamp => JulesClient.Services.AppSettings.ShowTimestamps;

    [JsonIgnore]
    public bool ShowOriginatorLabel => JulesClient.Services.AppSettings.ShowOriginatorLabels;
}

public record UserMessage(
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("text")] string? Text = null
);

public record UserMessaged([property: JsonPropertyName("userMessage")] string? UserMessage = null);

public record AgentMessaged([property: JsonPropertyName("agentMessage")] string? AgentMessage = null);

public record AgentMessage(
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("text")] string? Text = null
);

public record ProgressUpdated(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null
)
{
    [JsonIgnore]
    public bool HasData => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Description);
}

public record PlanGenerated([property: JsonPropertyName("plan")] Plan? Plan = null)
{
    [JsonIgnore]
    public bool HasData => Plan != null && (!string.IsNullOrWhiteSpace(Plan.Title) || !string.IsNullOrWhiteSpace(Plan.Description) || Plan.Steps?.Any() == true);
}

public record PlanApproved([property: JsonPropertyName("planId")] string? PlanId = null);

public record SessionFailed([property: JsonPropertyName("reason")] string? Reason = null);

public record Artifact(
    [property: JsonPropertyName("bashOutput")] BashOutput? BashOutput = null,
    [property: JsonPropertyName("changeSet")] ChangeSet? ChangeSet = null,
    [property: JsonPropertyName("media")] Media? Media = null,
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest = null
)
{
    [JsonIgnore]
    public bool HasData => BashOutput?.HasData == true || ChangeSet?.HasData == true || Media?.HasData == true || PullRequest?.HasData == true;
}

public record BashOutput(
    [property: JsonPropertyName("command")] string? Command = null,
    [property: JsonPropertyName("output")] string? Output = null,
    [property: JsonPropertyName("exitCode")] int? ExitCode = null
)
{
    [JsonIgnore]
    public bool HasData => !string.IsNullOrWhiteSpace(Output);
}

public record ChangeSet(
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("gitPatch")] GitPatch? GitPatch = null
)
{
    [JsonIgnore]
    public bool HasData => !string.IsNullOrWhiteSpace(GitPatch?.UnidiffPatch);
}

public record GitPatch(
    [property: JsonPropertyName("baseCommitId")] string? BaseCommitId = null,
    [property: JsonPropertyName("unidiffPatch")] string? UnidiffPatch = null,
    [property: JsonPropertyName("suggestedCommitMessage")] string? SuggestedCommitMessage = null
);

public record Media(
    [property: JsonPropertyName("mimeType")] string? MimeType = null,
    [property: JsonPropertyName("data")] string? Data = null
)
{
    [JsonIgnore]
    public bool HasData => !string.IsNullOrWhiteSpace(Data);
}

public record SendMessageResponse
{
    [property: JsonPropertyName("success")] public bool Success { get; init; }
}

public record Review(
    [property: JsonPropertyName("comments")] List<ReviewComment>? Comments = null,
    [property: JsonPropertyName("summary")] string? Summary = null
);

public record ReviewComment(
    [property: JsonPropertyName("filePath")] string? FilePath = null,
    [property: JsonPropertyName("lineNumber")] int? LineNumber = null,
    [property: JsonPropertyName("comment")] string? Comment = null
);

public static class AutomationModes
{
    public const string AutoCreatePR = "AUTO_CREATE_PR";
}