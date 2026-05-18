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
}

public record SourceContext(
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("startingBranch")] string? StartingBranch = null
);

public record SessionOutput(
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest = null
);

public record PullRequest(
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null
);

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
    [property: JsonPropertyName("description")] string? Description = null
)
{
    [JsonIgnore] public string? RawInfo { get; set; }

    [JsonIgnore] public string? EffectiveOriginator
    {
        get
        {
            // If activity contains user message data, treat it as user originator
            if (!string.IsNullOrWhiteSpace(UserMessage?.Prompt) ||
                !string.IsNullOrWhiteSpace(UserMessage?.Text) ||
                !string.IsNullOrWhiteSpace(UserMessaged?.UserMessage))
            {
                return "user";
            }
            return Originator;
        }
    }

    public string? DisplayText
    {
        get
        {
            // Check if this activity contains user message data
            bool hasUserMessage = !string.IsNullOrWhiteSpace(UserMessage?.Prompt) ||
                                  !string.IsNullOrWhiteSpace(UserMessage?.Text) ||
                                  !string.IsNullOrWhiteSpace(UserMessaged?.UserMessage);

            // If it has user message content, prioritize that regardless of Originator
            if (hasUserMessage)
            {
                if (!string.IsNullOrWhiteSpace(UserMessage?.Prompt)) return UserMessage.Prompt;
                if (!string.IsNullOrWhiteSpace(UserMessage?.Text)) return UserMessage.Text;
                if (!string.IsNullOrWhiteSpace(UserMessaged?.UserMessage)) return UserMessaged.UserMessage;
            }

            // Otherwise, treat as agent/system message
            if (!string.IsNullOrWhiteSpace(AgentMessage?.Message)) return AgentMessage.Message;
            if (!string.IsNullOrWhiteSpace(AgentMessage?.Text)) return AgentMessage.Text;
            if (!string.IsNullOrWhiteSpace(Review?.Summary)) return Review.Summary;
            if (!string.IsNullOrWhiteSpace(SessionFailed?.Reason)) return SessionFailed.Reason;
            if (PlanApproved != null) return "Plan Approved";
            if (SessionCompleted != null) return "Session Completed";

            return null;
        }
    }

    public bool HasContent
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DisplayText)) return true;
            if (ProgressUpdated?.HasData == true) return true;
            if (PlanGenerated?.HasData == true) return true;
            if (Artifacts?.Any(a => a.HasData) == true) return true;
            if (PlanApproved != null || SessionCompleted != null || SessionFailed != null) return true;
            if (BashOutput != null || ChangeSet != null || Media != null || PullRequest != null) return true;
            if (HasDebugInfo) return true;
            return false;
        }
    }

    public bool HasDebugInfo => !string.IsNullOrWhiteSpace(RawInfo);

    [JsonIgnore] public bool IsReview => Review != null || (Originator == "agent" && (DisplayText?.Contains("review", StringComparison.OrdinalIgnoreCase) == true || DisplayText?.Length > 500));
    [JsonIgnore] public bool ShowProgress => ProgressUpdated?.HasData == true;
    [JsonIgnore] public bool ShowPlan => PlanGenerated?.HasData == true;
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
    public bool HasData => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Description);
}

public record PlanGenerated([property: JsonPropertyName("plan")] Plan? Plan = null)
{
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
    public bool HasData => BashOutput != null || ChangeSet != null || Media != null || PullRequest != null;
}

public record BashOutput(
    [property: JsonPropertyName("command")] string? Command = null,
    [property: JsonPropertyName("output")] string? Output = null,
    [property: JsonPropertyName("exitCode")] int? ExitCode = null
);

public record ChangeSet(
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("gitPatch")] GitPatch? GitPatch = null
);

public record GitPatch(
    [property: JsonPropertyName("baseCommitId")] string? BaseCommitId = null,
    [property: JsonPropertyName("unidiffPatch")] string? UnidiffPatch = null,
    [property: JsonPropertyName("suggestedCommitMessage")] string? SuggestedCommitMessage = null
);

public record Media(
    [property: JsonPropertyName("mimeType")] string? MimeType = null,
    [property: JsonPropertyName("data")] string? Data = null
);

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
