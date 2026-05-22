using System.Reactive.Linq;
using JulesClient.Models;

namespace JulesClient.Services;

public class DemoJulesApiClient : IJulesApiClient
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly List<Session> _sessions;
    private readonly Dictionary<string, List<Activity>> _activities;

    public DemoJulesApiClient()
    {
        _sessions = new List<Session>
        {
            new Session(
                Name: "sessions/demo-1",
                Id: "demo-1",
                Title: "Demo: Implement Login Page",
                Prompt: "Create a modern login page using WinUI 3",
                CreateTime: DateTime.UtcNow.AddHours(-1).ToString("O"),
                State: "ACTIVE"
            ),
            new Session(
                Name: "sessions/demo-2",
                Id: "demo-2",
                Title: "Demo: Bug Fix in Auth Service",
                Prompt: "Fix null reference in TokenValidator",
                CreateTime: DateTime.UtcNow.AddDays(-1).ToString("O"),
                State: "COMPLETED"
            )
        };

        foreach (var s in _sessions)
        {
            s.RawInfo = System.Text.Json.JsonSerializer.Serialize(s, _json);
        }

        _activities = new Dictionary<string, List<Activity>>
        {
            ["sessions/demo-1"] = new List<Activity>
            {
                new Activity(
                    Name: "activities/1",
                    Id: "1",
                    CreateTime: DateTime.UtcNow.AddMinutes(-50).ToString("O"),
                    Originator: "user",
                    UserMessage: new UserMessage(Prompt: "Create a modern login page using WinUI 3")
                ),
                new Activity(
                    Name: "activities/2",
                    Id: "2",
                    CreateTime: DateTime.UtcNow.AddMinutes(-45).ToString("O"),
                    Originator: "agent",
                    ProgressUpdated: new ProgressUpdated(Title: "Analyzing project structure", Description: "Scanning Views and ViewModels...")
                ),
                new Activity(
                    Name: "activities/3",
                    Id: "3",
                    CreateTime: DateTime.UtcNow.AddMinutes(-40).ToString("O"),
                    Originator: "agent",
                    ProgressUpdated: new ProgressUpdated(Title: "Code Review", Description: "I have reviewed the current authentication logic. It seems we need a new `LoginPage.xaml` and a corresponding `LoginViewModel.cs`.")
                ),
                new Activity(
                    Name: "activities/4",
                    Id: "4",
                    CreateTime: DateTime.UtcNow.AddMinutes(-35).ToString("O"),
                    Originator: "agent",
                    Review: new Review(
                        Summary: "Detailed Review of UI requirements",
                        Comments: new List<ReviewComment>
                        {
                            new ReviewComment(FilePath: "Views/LoginPage.xaml", LineNumber: 10, Comment: "Use `PasswordBox` for the password field to ensure security."),
                            new ReviewComment(FilePath: "ViewModels/LoginViewModel.cs", LineNumber: 25, Comment: "Implement `RelayCommand` for the Login button.")
                        }
                    )
                ),
                new Activity(
                    Name: "activities/5",
                    Id: "5",
                    CreateTime: DateTime.UtcNow.AddMinutes(-30).ToString("O"),
                    Originator: "agent",
                    BashOutput: new BashOutput(Command: "mkdir -p Views ViewModels", Output: "", ExitCode: 0)
                ),
                new Activity(
                    Name: "activities/6",
                    Id: "6",
                    CreateTime: DateTime.UtcNow.AddMinutes(-25).ToString("O"),
                    Originator: "agent",
                    ChangeSet: new ChangeSet(
                        Source: "Views/LoginPage.xaml",
                        GitPatch: new GitPatch(
                            UnidiffPatch: "--- a/Views/LoginPage.xaml\n+++ b/Views/LoginPage.xaml\n@@ -0,0 +1,10 @@\n+<Page x:Class=\"Demo.LoginPage\">\n+  <StackPanel>\n+    <TextBox x:Name=\"Username\"/>\n+    <PasswordBox x:Name=\"Password\"/>\n+    <Button Content=\"Login\"/>\n+  </StackPanel>\n+</Page>",
                            SuggestedCommitMessage: "Add initial login page layout"
                        )
                    )
                )
            }
        };
    }

    public Task<SourceListResponse> ListSourcesAsync(string? pageToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(new SourceListResponse(Sources: new List<Source>
        {
            new Source(Name: "sources/demo", Id: "demo", GitHubRepo: new GitHubRepo(Owner: "demo-user", Repo: "demo-repo"))
        }));
    }

    public Task<Session> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default)
    {
        var session = new Session(
            Name: $"sessions/demo-{Guid.NewGuid()}",
            Id: Guid.NewGuid().ToString(),
            Title: req.Title ?? "New Demo Session",
            Prompt: req.Prompt,
            CreateTime: DateTime.UtcNow.ToString("O"),
            State: "ACTIVE"
        );
        _sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<SessionListResponse> ListSessionsAsync(int pageSize = 5, string? pageToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(new SessionListResponse(Sessions: _sessions));
    }

    public Task<Session> GetSessionAsync(string id, CancellationToken ct = default)
    {
        var session = _sessions.FirstOrDefault(s => s.Name == id || s.Id == id);
        return Task.FromResult(session ?? throw new Exception("Session not found"));
    }

    public Task<ApprovePlanResponse> ApprovePlanAsync(string id, CancellationToken ct = default)
    {
        return Task.FromResult(new ApprovePlanResponse());
    }

    public Task<ActivityListResponse> ListActivitiesAsync(string sid, int pageSize = 10, string? pageToken = null, string? filter = null, CancellationToken ct = default)
    {
        if (_activities.TryGetValue(sid, out var list))
        {
            return Task.FromResult(new ActivityListResponse(Activities: list));
        }
        return Task.FromResult(new ActivityListResponse(Activities: new List<Activity>()));
    }

    public Task<SendMessageResponse> SendMessageAsync(string sid, string prompt, CancellationToken ct = default)
    {
        if (!_activities.ContainsKey(sid)) _activities[sid] = new List<Activity>();

        _activities[sid].Add(new Activity(
            Name: $"activities/{Guid.NewGuid()}",
            CreateTime: DateTime.UtcNow.ToString("O"),
            Originator: "user",
            UserMessage: new UserMessage(Prompt: prompt)
        ));

        // Simulate agent response
        _activities[sid].Add(new Activity(
            Name: $"activities/{Guid.NewGuid()}",
            CreateTime: DateTime.UtcNow.AddSeconds(2).ToString("O"),
            Originator: "agent",
            Text: "This is a demo response to your message."
        ));

        return Task.FromResult(new SendMessageResponse { Success = true });
    }

    public IObservable<ActivityListResponse> PollActivitiesAsync(string sid, TimeSpan interval, CancellationToken ct = default)
    {
        return Observable.Interval(interval)
            .SelectMany(_ => ListActivitiesAsync(sid, ct: ct));
    }
}
