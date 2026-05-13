using System.Text.Json.Serialization;

namespace JulesClient.Models;

public record SourceListResponse([property: JsonPropertyName("sources")] List<Source> Sources, [property: JsonPropertyName("nextPageToken")] string? NextPageToken);
public record Source([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("githubSource")] GitHubSource? GitHubSource, [property: JsonPropertyName("createTime")] string CreateTime, [property: JsonPropertyName("updateTime")] string UpdateTime);
public record GitHubSource([property: JsonPropertyName("owner")] string Owner, [property: JsonPropertyName("repo")] string Repo, [property: JsonPropertyName("installationId")] string InstallationId, [property: JsonPropertyName("branch")] string? Branch, [property: JsonPropertyName("baseBranch")] string? BaseBranch);

public record SessionListResponse([property: JsonPropertyName("sessions")] List<Session> Sessions, [property: JsonPropertyName("nextPageToken")] string? NextPageToken);
public record Session([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("source")] string Source, [property: JsonPropertyName("prompt")] string Prompt, [property: JsonPropertyName("createTime")] string CreateTime, [property: JsonPropertyName("updateTime")] string UpdateTime, [property: JsonPropertyName("state")] string State, [property: JsonPropertyName("plan")] Plan? Plan, [property: JsonPropertyName("pendingPlan")] Plan? PendingPlan) { public string Id => Name.Replace("sources/-/sessions/", ""); }
public record CreateSessionRequest([property: JsonPropertyName("source")] string Source, [property: JsonPropertyName("prompt")] string Prompt);
public record Plan([property: JsonPropertyName("title")] string Title, [property: JsonPropertyName("description")] string Description, [property: JsonPropertyName("steps")] List<PlanStep> Steps);
public record PlanStep([property: JsonPropertyName("title")] string Title, [property: JsonPropertyName("description")] string Description, [property: JsonPropertyName("status")] string Status);
public record ApprovePlanResponse();

public record ActivityListResponse([property: JsonPropertyName("activities")] List<Activity> Activities, [property: JsonPropertyName("nextPageToken")] string? NextPageToken);
public record Activity([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("originator")] string Originator, [property: JsonPropertyName("createTime")] string CreateTime, [property: JsonPropertyName("progressUpdated")] ProgressUpdated? ProgressUpdated, [property: JsonPropertyName("planGenerated")] PlanGenerated? PlanGenerated, [property: JsonPropertyName("artifacts")] List<Artifact>? Artifacts);
public record ProgressUpdated([property: JsonPropertyName("title")] string Title, [property: JsonPropertyName("description")] string Description);
public record PlanGenerated([property: JsonPropertyName("plan")] Plan Plan);
public record Artifact([property: JsonPropertyName("bashOutput")] BashOutput? BashOutput, [property: JsonPropertyName("changeSet")] ChangeSet? ChangeSet, [property: JsonPropertyName("media")] Media? Media);
public record BashOutput([property: JsonPropertyName("output")] string Output, [property: JsonPropertyName("exitCode")] int ExitCode);
public record ChangeSet([property: JsonPropertyName("patch")] string Patch, [property: JsonPropertyName("files")] List<ChangedFile> Files);
public record ChangedFile([property: JsonPropertyName("path")] string Path, [property: JsonPropertyName("status")] string Status);
public record Media([property: JsonPropertyName("mimeType")] string MimeType, [property: JsonPropertyName("data")] string Data);
public record SendMessageResponse { [property: JsonPropertyName("success")] public bool Success { get; init; } }
