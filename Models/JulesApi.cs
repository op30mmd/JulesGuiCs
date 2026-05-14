using System.Text.Json.Serialization;
namespace JulesClient.Models;

public record SourceListResponse(
    [property: JsonPropertyName("sources")] List<Source>? Sources,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken
);

public record Source(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("githubRepo")] GitHubRepo? GitHubRepo,
    [property: JsonPropertyName("createTime")] string? CreateTime,
    [property: JsonPropertyName("updateTime")] string? UpdateTime
);

public record GitHubRepo(
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("repo")] string? Repo
);

public record SessionListResponse(
    [property: JsonPropertyName("sessions")] List<Session>? Sessions,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken
);

public record Session(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("sourceContext")] SourceContext? SourceContext,
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("createTime")] string? CreateTime,
    [property: JsonPropertyName("updateTime")] string? UpdateTime,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("plan")] Plan? Plan,
    [property: JsonPropertyName("pendingPlan")] Plan? PendingPlan,
    [property: JsonPropertyName("outputs")] List<SessionOutput>? Outputs,
    [property: JsonPropertyName("requirePlanApproval")] bool? RequirePlanApproval
)
{
    public string ShortId => Name?.Replace("sessions/", "") ?? string.Empty;
}

public record SourceContext(
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("githubRepoContext")] GitHubRepoContext? GitHubRepoContext
);

public record GitHubRepoContext(
    [property: JsonPropertyName("startingBranch")] string? StartingBranch
);

public record SessionOutput(
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest
);

public record PullRequest(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description
);

public record CreateSessionRequest(
    [property: JsonPropertyName("sourceContext")] SourceContext SourceContext,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("requirePlanApproval")] bool RequirePlanApproval = false,
    [property: JsonPropertyName("automationMode")] string? AutomationMode = null,
    [property: JsonPropertyName("title")] string? Title = null
);

public record Plan(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("steps")] List<PlanStep>? Steps
);

public record PlanStep(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("index")] int? Index
);

public record ApprovePlanResponse();

public record ActivityListResponse(
    [property: JsonPropertyName("activities")] List<Activity>? Activities,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken
);

public record Activity(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("createTime")] string? CreateTime,
    [property: JsonPropertyName("originator")] string? Originator,
    [property: JsonPropertyName("progressUpdated")] ProgressUpdated? ProgressUpdated,
    [property: JsonPropertyName("planGenerated")] PlanGenerated? PlanGenerated,
    [property: JsonPropertyName("planApproved")] PlanApproved? PlanApproved,
    [property: JsonPropertyName("sessionCompleted")] object? SessionCompleted,
    [property: JsonPropertyName("artifacts")] List<Artifact>? Artifacts,
    [property: JsonPropertyName("userMessage")] UserMessage? UserMessage,
    [property: JsonPropertyName("agentMessage")] AgentMessage? AgentMessage,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("prompt")] string? Prompt
)
{
    public string? DisplayText => !string.IsNullOrEmpty(Text) ? Text : (!string.IsNullOrEmpty(Prompt) ? Prompt : (!string.IsNullOrEmpty(UserMessage?.Prompt) ? UserMessage.Prompt : AgentMessage?.Message));
}

public record UserMessage([property: JsonPropertyName("prompt")] string? Prompt);
public record AgentMessage([property: JsonPropertyName("message")] string? Message);

public record ProgressUpdated(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description
);

public record PlanGenerated([property: JsonPropertyName("plan")] Plan? Plan);
public record PlanApproved([property: JsonPropertyName("planId")] string? PlanId);

public record Artifact(
    [property: JsonPropertyName("bashOutput")] BashOutput? BashOutput,
    [property: JsonPropertyName("changeSet")] ChangeSet? ChangeSet,
    [property: JsonPropertyName("media")] Media? Media,
    [property: JsonPropertyName("pullRequest")] PullRequest? PullRequest
);

public record BashOutput(
    [property: JsonPropertyName("command")] string? Command,
    [property: JsonPropertyName("output")] string? Output,
    [property: JsonPropertyName("exitCode")] int? ExitCode
);

public record ChangeSet(
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("gitPatch")] GitPatch? GitPatch
);

public record GitPatch(
    [property: JsonPropertyName("baseCommitId")] string? BaseCommitId,
    [property: JsonPropertyName("unidiffPatch")] string? UnidiffPatch,
    [property: JsonPropertyName("suggestedCommitMessage")] string? SuggestedCommitMessage
);

public record Media(
    [property: JsonPropertyName("mimeType")] string? MimeType,
    [property: JsonPropertyName("data")] string? Data
);

public record SendMessageResponse
{
    [property: JsonPropertyName("success")] public bool Success { get; init; }
}
