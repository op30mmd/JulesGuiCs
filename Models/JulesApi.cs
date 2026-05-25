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
    [property: JsonPropertyName("createTime")] string? CreateTime = null,
    [property: JsonPropertyName("updateTime")] string? UpdateTime = null
);

public record GitHubRepo(
    [property: JsonPropertyName("owner")] string? Owner = null,
    [property: JsonPropertyName("repo")] string? Repo = null
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
    [property: JsonPropertyName("createTime")] string? CreateTime = null,
    [property: JsonPropertyName("updateTime")] string? UpdateTime = null,
    [property: JsonPropertyName("state")] string? State = null,
    [property: JsonPropertyName("plan")] Plan? Plan = null,
    [property: JsonPropertyName("pendingPlan")] Plan? PendingPlan = null,
    [property: JsonPropertyName("outputs")] List<SessionOutput>? Outputs = null,
    [property: JsonPropertyName("requirePlanApproval")] bool? RequirePlanApproval = null
)
{
    public string ShortId => Name?.Replace("sessions/", "") ?? string.Empty;

    [JsonIgnore]
    public string? RawInfo { get; set; }
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
    public bool HasData => !string.IsNullOrWhiteSpace(Url) || !string.IsNullOrWhiteSpace(Title);
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
    [property: JsonPropertyName("createTime")] string? CreateTime = null,
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
    [property: JsonPropertyName("review")] Review? Review = null,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("updateTime")] string? UpdateTime = null,
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

    [JsonIgnore]
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

    [JsonIgnore]
    public bool IsReview
    {
        get
        {
            if (_cachedIsReview.HasValue)
            {
                return _cachedIsReview.Value;
            }

            // 1. If a structured review object exists, it is definitely a review
            if (Review != null)
            {
                _cachedIsReview = true;
                return true;
            }

            // 2. Check root Title (safeguarded against nulls)
            var title = Title ?? "";
            bool titleIndicatesReview = !string.IsNullOrWhiteSpace(title) &&
                (title.Contains("Code Reviewed", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("Code Review", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("Review", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("Feedback", StringComparison.OrdinalIgnoreCase));

            // 3. Check DisplayText
            var text = DisplayText ?? "";
            bool textIndicatesReview = !string.IsNullOrWhiteSpace(text) &&
                (text.Contains("Code Reviewed", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("Code Review", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("Feedback", StringComparison.OrdinalIgnoreCase));

            // 4. Check ProgressUpdated Title strictly (NO Description scanning to prevent code diff false positives)
            var progressTitle = ProgressUpdated?.Title ?? "";
            bool progressTitleIndicatesReview = !string.IsNullOrWhiteSpace(progressTitle) &&
                (progressTitle.Contains("Code Reviewed", StringComparison.OrdinalIgnoreCase) ||
                 progressTitle.Contains("Code Review", StringComparison.OrdinalIgnoreCase));

            var result = titleIndicatesReview ||
                         textIndicatesReview ||
                         progressTitleIndicatesReview;

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
}

public record UserMessage(
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("text")] string? Text = null
);

public record UserMessaged([property: JsonPropertyName("userMessage")] string? UserMessage = null);

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
    public bool HasData => !string.IsNullOrWhiteSpace(Command) || !string.IsNullOrWhiteSpace(Output);
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