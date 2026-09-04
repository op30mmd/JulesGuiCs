using JulesClient.Models;
using JulesClient.Services;

namespace JulesClient.Tests;

public class ViewModelAndConverterTests
{
    [Fact]
    public void AutomationModes_HasExpectedConstant()
    {
        Assert.Equal("AUTO_CREATE_PR", AutomationModes.AutoCreatePR);
    }

    [Fact]
    public void CreateSessionRequest_RecordInitialization()
    {
        var req = new CreateSessionRequest(
            SourceContext: new SourceContext("sources/1"),
            Prompt: "Do something",
            RequirePlanApproval: true,
            AutomationMode: AutomationModes.AutoCreatePR,
            Title: "Test Session"
        );

        Assert.Equal("sources/1", req.SourceContext.Source);
        Assert.Equal("Do something", req.Prompt);
        Assert.True(req.RequirePlanApproval);
        Assert.Equal("AUTO_CREATE_PR", req.AutomationMode);
        Assert.Equal("Test Session", req.Title);
    }

    [Fact]
    public void PlanAndSteps_InitializationAndHasData()
    {
        var step1 = new PlanStep(Id: "step1", Title: "First Step", Status: "COMPLETED", Index: 0);
        var step2 = new PlanStep(Id: "step2", Title: "Second Step", Status: "PENDING", Index: 1);

        var plan = new Plan(
            Id: "plan1",
            Title: "Test Plan",
            Description: "Plan description",
            Steps: new List<PlanStep> { step1, step2 }
        );

        Assert.Equal(2, plan.Steps!.Count);

        var planGen = new PlanGenerated(Plan: plan);
        Assert.True(planGen.HasData);
    }

    [Fact]
    public void ReviewAndComments_Initialization()
    {
        var comment = new ReviewComment(FilePath: "Services/Test.cs", LineNumber: 15, Comment: "Clean code");
        var review = new Review(
            Summary: "Review summary",
            Comments: new List<ReviewComment> { comment }
        );

        Assert.Equal("Review summary", review.Summary);
        Assert.Single(review.Comments!);
        Assert.Equal("Services/Test.cs", review.Comments![0].FilePath);
        Assert.Equal(15, review.Comments[0].LineNumber);
    }

    [Fact]
    public void OriginatorLabel_MappingRules()
    {
        Func<string?, string> getLabel = originator => originator switch
        {
            "user" => "You",
            "agent" => "Jules",
            "review" => "Code Review",
            null or "" => string.Empty,
            var other => char.ToUpperInvariant(other[0]) + other[1..]
        };

        Assert.Equal("You", getLabel("user"));
        Assert.Equal("Jules", getLabel("agent"));
        Assert.Equal("Code Review", getLabel("review"));
        Assert.Equal(string.Empty, getLabel(null));
        Assert.Equal(string.Empty, getLabel(""));
        Assert.Equal("Custom", getLabel("custom"));
    }

    [Fact]
    public void StatusKindMapping_Rules()
    {
        Func<string?, string> getHexColor = statusKind => statusKind switch
        {
            "working" => "#3B82F6",
            "done" => "#16A34A",
            "failed" => "#DC2626",
            "attention" => "#D97706",
            _ => "#9AA0A6",
        };

        Assert.Equal("#3B82F6", getHexColor("working"));
        Assert.Equal("#16A34A", getHexColor("done"));
        Assert.Equal("#DC2626", getHexColor("failed"));
        Assert.Equal("#D97706", getHexColor("attention"));
        Assert.Equal("#9AA0A6", getHexColor("idle"));
    }
}
