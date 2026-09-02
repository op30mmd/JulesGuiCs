using JulesClient.Models;

namespace JulesClient.Tests;

public class ActivityReviewTests
{
    [Fact]
    public void AgentMessage_MentioningCodeReview_IsNotAReview()
    {
        var a = new Activity(
            Name: "activities/1",
            Originator: "agent",
            AgentMessage: new AgentMessage(Message:
                "Now we can request a final code review of our changes just to make sure " +
                "everything is completely solid and rated #Correct#. Let's call request_code_review."));

        Assert.False(a.IsReview);
    }

    [Fact]
    public void AgentMessage_TitledCodeReview_IsStillNotAReview()
    {
        // A plain chat turn talking about a review must not be counted even if the
        // backend also stamps it with a "Code Review" title.
        var a = new Activity(
            Name: "activities/2",
            Originator: "agent",
            Title: "Code Review",
            AgentMessage: new AgentMessage(Message: "Let's call request_code_review next."));

        Assert.False(a.IsReview);
    }

    [Fact]
    public void MessageBody_MentioningFeedback_IsNotAReview()
    {
        var a = new Activity(
            Name: "activities/3",
            Originator: "agent",
            AgentMessage: new AgentMessage(Message: "I addressed your feedback and the code review comments."));

        Assert.False(a.IsReview);
    }

    [Fact]
    public void ProgressUpdate_WithUnrelatedTitle_IsNotAReview_EvenIfBodyMentionsReview()
    {
        var a = new Activity(
            Name: "activities/4",
            ProgressUpdated: new ProgressUpdated(Title: "Running tests", Description: "Preparing for code review"));

        Assert.False(a.IsReview);
    }

    [Fact]
    public void ProgressUpdate_TitledCodeReviewed_IsAReview()
    {
        var a = new Activity(
            Name: "activities/5",
            ProgressUpdated: new ProgressUpdated(
                Title: "Code reviewed",
                Description: "Final Rating: #Partially Correct"));

        Assert.True(a.IsReview);
    }

    [Fact]
    public void ProgressUpdate_TitledCodeReview_IsAReview()
    {
        var a = new Activity(
            Name: "activities/6",
            ProgressUpdated: new ProgressUpdated(Title: "Code Review", Description: "Some review text."));

        Assert.True(a.IsReview);
    }

    [Fact]
    public void HeadingWithMarkdownAndPunctuation_IsAReview()
    {
        var a = new Activity(
            Name: "activities/7",
            ProgressUpdated: new ProgressUpdated(Title: "## Code Review:", Description: "body"));

        Assert.True(a.IsReview);
    }

    [Fact]
    public void StructuredReviewObject_IsAlwaysAReview()
    {
        var a = new Activity(
            Name: "activities/8",
            Review: new Review(Summary: "Looks good overall."));

        Assert.True(a.IsReview);
    }

    [Fact]
    public void SessionLikeTitle_OnPlainMessage_IsNotAReview()
    {
        var a = new Activity(
            Name: "activities/9",
            Originator: "user",
            Title: "Review Direct Includes and Curl usage",
            UserMessage: new UserMessage(Prompt: "Please review the direct includes."));

        Assert.False(a.IsReview);
    }

    [Fact]
    public void PlanApprovedOnly_IsASystemEvent()
    {
        var a = new Activity(Name: "activities/10", PlanApproved: new PlanApproved(PlanId: "p1"));

        Assert.True(a.IsSystemEvent);
        Assert.Equal("Plan approved", a.SystemEventText);
    }

    [Fact]
    public void SessionCompletedOnly_IsASystemEvent()
    {
        var a = new Activity(Name: "activities/11", SessionCompleted: new object());

        Assert.True(a.IsSystemEvent);
        Assert.Equal("Session completed", a.SystemEventText);
    }

    [Fact]
    public void PlanApprovedWithAgentMessage_IsNotASystemEvent()
    {
        var a = new Activity(
            Name: "activities/12",
            PlanApproved: new PlanApproved(PlanId: "p1"),
            AgentMessage: new AgentMessage(Message: "I approved the plan and started work."));

        Assert.False(a.IsSystemEvent);
    }

    [Fact]
    public void OrdinaryMessage_HasNoSystemEvent()
    {
        var a = new Activity(Name: "activities/13", AgentMessage: new AgentMessage(Message: "Working on it."));

        Assert.Null(a.SystemEventText);
        Assert.False(a.IsSystemEvent);
    }
}
