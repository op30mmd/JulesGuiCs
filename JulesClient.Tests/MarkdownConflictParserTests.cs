using System.Linq;
using JulesClient.Services;

namespace JulesClient.Tests;

public class MarkdownConflictParserTests
{
    private const string Fenced =
        "```cpp\n" +
        "<<<<<<< SEARCH\n" +
        "    if (a) {\n" +
        "        continue;\n" +
        "    }\n" +
        "=======\n" +
        "    if (b) {\n" +
        "        return;\n" +
        "    }\n" +
        ">>>>>>> REPLACE\n" +
        "```";

    [Fact]
    public void FencedConflict_BecomesOneConflictSegment_FencesStripped()
    {
        var segs = MarkdownConflictParser.Split(Fenced);

        Assert.Single(segs);
        var c = segs[0].Conflict;
        Assert.NotNull(c);
        Assert.Equal("cpp", c!.Language);
        Assert.Equal("    if (a) {\n        continue;\n    }", c.Search);
        Assert.Equal("    if (b) {\n        return;\n    }", c.Replace);
        Assert.DoesNotContain("```", segs[0].Text);
        Assert.DoesNotContain("<<<<<<<", c.Search);
        Assert.DoesNotContain("=======", c.Search);
    }

    [Fact]
    public void ProseAroundConflict_IsSplitIntoThreeSegments()
    {
        var text = "Here is the change:\n\n" + Fenced + "\n\nApply it carefully.";
        var segs = MarkdownConflictParser.Split(text);

        Assert.Equal(3, segs.Count);
        Assert.Null(segs[0].Conflict);
        Assert.Equal("Here is the change:", segs[0].Text);
        Assert.NotNull(segs[1].Conflict);
        Assert.Null(segs[2].Conflict);
        Assert.Equal("Apply it carefully.", segs[2].Text);
    }

    [Fact]
    public void BareConflict_WithoutFence_IsRecognized_LanguageNull()
    {
        var text =
            "<<<<<<< SEARCH\n" +
            "old line\n" +
            "=======\n" +
            "new line\n" +
            ">>>>>>> REPLACE";

        var segs = MarkdownConflictParser.Split(text);
        Assert.Single(segs);
        var c = segs[0].Conflict;
        Assert.NotNull(c);
        Assert.Null(c!.Language);
        Assert.Equal("old line", c.Search);
        Assert.Equal("new line", c.Replace);
    }

    [Fact]
    public void GitStyleHeadMarkers_AreRecognized()
    {
        var text =
            "<<<<<<< HEAD\n" +
            "ours\n" +
            "=======\n" +
            "theirs\n" +
            ">>>>>>> feature/x";

        var segs = MarkdownConflictParser.Split(text);
        Assert.Single(segs);
        var c = segs[0].Conflict;
        Assert.NotNull(c);
        Assert.Equal("ours", c!.Search);
        Assert.Equal("theirs", c.Replace);
    }

    [Fact]
    public void MultipleConflicts_Alternate()
    {
        var text = Fenced + "\n\nthen\n\n" + Fenced;
        var segs = MarkdownConflictParser.Split(text);

        Assert.Equal(3, segs.Count);
        Assert.NotNull(segs[0].Conflict);
        Assert.Equal("then", segs[1].Text);
        Assert.NotNull(segs[2].Conflict);
    }

    [Fact]
    public void IncompleteMarkers_StayAsPlainText()
    {
        var text = "text with <<<<<<< SEARCH but no separator or end";
        var segs = MarkdownConflictParser.Split(text);

        Assert.Single(segs);
        Assert.Null(segs[0].Conflict);
        Assert.Contains("<<<<<<<", segs[0].Text);
    }

    [Fact]
    public void PlainMarkdown_IsOneNonConflictSegment()
    {
        var segs = MarkdownConflictParser.Split("# Title\n\nSome **text** and `code`.");
        Assert.Single(segs);
        Assert.Null(segs[0].Conflict);
    }

    [Fact]
    public void NullOrWhitespace_YieldsNoSegments()
    {
        Assert.Empty(MarkdownConflictParser.Split(null));
        Assert.Empty(MarkdownConflictParser.Split(""));
        Assert.Empty(MarkdownConflictParser.Split("   \n  \n"));
    }
}
