using JulesClient.Services;

namespace JulesClient.Tests;

public class MarkdownTextTests
{
    private static string Run(string text) =>
        string.Join("\n", MarkdownText.DedupeRepeatedLabels(text.Split('\n')));

    [Fact]
    public void DropsHeadingLabel_WhenNextLineRestatesItWithValue()
    {
        var input = "### Final Rating:\n**Final Rating:** #Correct#";
        Assert.Equal("**Final Rating:** #Correct#", Run(input));
    }

    [Fact]
    public void DropsBoldLabel_WhenNextLineRestatesIt()
    {
        var input = "**Final Rating:**\nFinal Rating: #Partially Correct#";
        Assert.Equal("Final Rating: #Partially Correct#", Run(input));
    }

    [Fact]
    public void DropsLabel_AcrossBlankLine()
    {
        var input = "Final Rating:\n\n**Final Rating:** #Correct#";
        Assert.Equal("\n**Final Rating:** #Correct#", Run(input));
    }

    [Fact]
    public void KeepsLabel_WhenNextLineDoesNotRestateIt()
    {
        var input = "Summary:\nThe change adds a boot toggle.";
        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void KeepsLabel_WhenNextLineIsJustTheLabelAgain()
    {
        // No added content -> not the duplicate-with-value pattern.
        var input = "Final Rating:\nFinal Rating:";
        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void IgnoresLinesThatAreNotLabels()
    {
        var input = "This is a normal sentence.\nAnd another one that continues.";
        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void IgnoresSentenceEndingWithColon()
    {
        var input = "Do the following steps in order. Then verify:\nThen verify: it works";
        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void LeavesSingleLineUnchanged()
    {
        Assert.Equal("Final Rating: #Correct#",
            string.Join("\n", MarkdownText.DedupeRepeatedLabels(new[] { "Final Rating: #Correct#" })));
    }
}
