using JulesClient.Models;

namespace JulesClient.Tests;

public class SessionModelTests
{
    [Fact]
    public void ShortId_StripsPrefix_OrReturnsEmpty()
    {
        var s1 = new Session(Name: "sessions/test-1234");
        Assert.Equal("test-1234", s1.ShortId);

        var s2 = new Session(Name: "custom_id");
        Assert.Equal("custom_id", s2.ShortId);
    }

    [Fact]
    public void DisplayBranch_PrefersSourceContext_FallbackToOutputs()
    {
        var s1 = new Session(
            Name: "sessions/s1",
            SourceContext: new SourceContext(
                Source: "sources/1",
                GitHubRepoContext: new GitHubRepoContext(StartingBranch: "main")
            )
        );
        Assert.Equal("main", s1.DisplayBranch);

        var s2 = new Session(
            Name: "sessions/s2",
            SourceContext: new SourceContext(
                Source: "sources/1",
                GitHubRepoContext: new GitHubRepoContext(StartingBranch: "")
            ),
            Outputs: new List<SessionOutput>
            {
                new SessionOutput(
                    PullRequest: new PullRequest(Url: "https://github.com/a/b/pull/1", BaseRef: "feature/base")
                )
            }
        );
        Assert.Equal("feature/base", s2.DisplayBranch);

        var s3 = new Session(Name: "sessions/s3");
        Assert.Null(s3.DisplayBranch);
    }

    [Fact]
    public void PrimaryPullRequest_ReturnsFirstPRWithData()
    {
        var pr1 = new PullRequest(Url: "");
        var pr2 = new PullRequest(Url: "https://github.com/owner/repo/pull/10", Title: "PR Title");

        var session = new Session(
            Name: "sessions/s1",
            Outputs: new List<SessionOutput>
            {
                new SessionOutput(PullRequest: pr1),
                new SessionOutput(PullRequest: pr2)
            }
        );

        Assert.NotNull(session.PrimaryPullRequest);
        Assert.Equal("https://github.com/owner/repo/pull/10", session.PrimaryPullRequest.Url);
    }

    [Fact]
    public void PullRequestLabel_FormatsPRNumber_OrGenericLabel()
    {
        var s1 = new Session(
            Name: "s1",
            Outputs: new List<SessionOutput>
            {
                new SessionOutput(PullRequest: new PullRequest(Url: "https://github.com/owner/repo/pull/42/"))
            }
        );
        Assert.Equal("PR #42", s1.PullRequestLabel);

        var s2 = new Session(
            Name: "s2",
            Outputs: new List<SessionOutput>
            {
                new SessionOutput(PullRequest: new PullRequest(Url: "https://github.com/owner/repo/pull/abc"))
            }
        );
        Assert.Equal("Pull request", s2.PullRequestLabel);

        var s3 = new Session(Name: "s3");
        Assert.Null(s3.PullRequestLabel);
    }

    [Fact]
    public void RawInfo_GetAndSetWork()
    {
        var session = new Session(Name: "s1") { RawInfo = "{\"name\":\"s1\"}" };
        Assert.Equal("{\"name\":\"s1\"}", session.RawInfo);
    }
}
