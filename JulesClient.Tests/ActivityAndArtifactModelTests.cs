using JulesClient.Models;

namespace JulesClient.Tests;

public class ActivityAndArtifactModelTests
{
    [Fact]
    public void SubModels_HasData_Validation()
    {
        Assert.False(new PullRequest(Url: "").HasData);
        Assert.False(new PullRequest(Url: "   ").HasData);
        Assert.True(new PullRequest(Url: "https://example.com").HasData);

        Assert.False(new ProgressUpdated().HasData);
        Assert.True(new ProgressUpdated(Title: "In Progress").HasData);
        Assert.True(new ProgressUpdated(Description: "Working...").HasData);

        Assert.False(new PlanGenerated().HasData);
        Assert.True(new PlanGenerated(Plan: new Plan(Title: "Plan 1")).HasData);

        Assert.False(new BashOutput().HasData);
        Assert.True(new BashOutput(Output: "output text").HasData);

        Assert.False(new ChangeSet().HasData);
        Assert.True(new ChangeSet(GitPatch: new GitPatch(UnidiffPatch: "diff text")).HasData);

        Assert.False(new Media().HasData);
        Assert.True(new Media(Data: "base64data").HasData);

        Assert.False(new Artifact().HasData);
        Assert.True(new Artifact(BashOutput: new BashOutput(Output: "out")).HasData);
    }

    [Fact]
    public void Activity_EffectiveOriginator_Logic()
    {
        var reviewAct = new Activity(Name: "a1", Review: new Review(Summary: "Summary"));
        Assert.Equal("review", reviewAct.EffectiveOriginator);

        var agentAct = new Activity(Name: "a2", AgentMessage: new AgentMessage(Message: "Hello"));
        Assert.Equal("agent", agentAct.EffectiveOriginator);

        var userAct = new Activity(Name: "a3", UserMessage: new UserMessage(Prompt: "Hi"));
        Assert.Equal("user", userAct.EffectiveOriginator);

        var fallbackAct = new Activity(Name: "a4", Originator: "custom");
        Assert.Equal("custom", fallbackAct.EffectiveOriginator);
    }

    [Fact]
    public void Activity_DisplayText_Precedence()
    {
        var a1 = new Activity(
            Name: "a1",
            AgentMessage: new AgentMessage(Message: "Agent Msg", Text: "Agent Text"),
            Text: "Root Text"
        );
        Assert.Equal("Agent Msg", a1.DisplayText);

        var a2 = new Activity(
            Name: "a2",
            SessionFailed: new SessionFailed(Reason: "Crash"),
            Text: "Root Text"
        );
        Assert.Equal("Crash", a2.DisplayText);

        var a3 = new Activity(
            Name: "a3",
            Originator: "user",
            UserMessage: new UserMessage(Prompt: "User Prompt")
        );
        Assert.Equal("User Prompt", a3.DisplayText);
    }

    [Fact]
    public void Activity_HasContent_Logic()
    {
        var empty = new Activity(Name: "e1");
        Assert.False(empty.HasContent);

        var withText = new Activity(Name: "e2", Text: "Some content");
        Assert.True(withText.HasContent);

        var withArtifact = new Activity(Name: "e3", Artifacts: new List<Artifact> { new Artifact(BashOutput: new BashOutput(Output: "ok")) });
        Assert.True(withArtifact.HasContent);
    }

    [Fact]
    public void Activity_IsChangeNote_Logic()
    {
        var act = new Activity(Name: "c1") { ChangeSummary = "Updated file.cs" };
        Assert.True(act.IsChangeNote);

        var actWithMessage = new Activity(Name: "c2", AgentMessage: new AgentMessage(Message: "Done"))
        {
            ChangeSummary = "Updated file.cs"
        };
        Assert.False(actWithMessage.IsChangeNote);
    }

    [Fact]
    public void Activity_ReviewTitlesAndTexts()
    {
        var a1 = new Activity(Name: "r1", Title: "Custom Title", Review: new Review(Summary: "Summary text"));
        Assert.Equal("Custom Title", a1.ReviewDisplayTitle);
        Assert.Equal("Summary text", a1.ReviewDisplayText);

        var a2 = new Activity(Name: "r2", ProgressUpdated: new ProgressUpdated(Title: "Code Review", Description: "Review details"));
        Assert.Equal("Code Review", a2.ReviewDisplayTitle);
        Assert.Equal("Review details", a2.ReviewDisplayText);
    }
}
