using System.Linq;
using JulesClient.Services;

namespace JulesClient.Tests;

public class MarkdownInlineTests
{
    private static (MarkdownInlineKind Kind, string Text, string? Url)[] Tok(string s) =>
        MarkdownInline.Tokenize(s).Select(t => (t.Kind, t.Text, t.Url)).ToArray();

    [Fact]
    public void PlainText_IsSingleTextToken()
    {
        var t = Tok("just some words");
        Assert.Single(t);
        Assert.Equal((MarkdownInlineKind.Text, "just some words", (string?)null), t[0]);
    }

    [Fact]
    public void Bold_Italic_BoldItalic_AreRecognized()
    {
        Assert.Equal(MarkdownInlineKind.Bold, Tok("a **b** c")[1].Kind);
        Assert.Equal(MarkdownInlineKind.Italic, Tok("a *b* c")[1].Kind);
        Assert.Equal(MarkdownInlineKind.BoldItalic, Tok("a ***b*** c")[1].Kind);
        Assert.Equal("b", Tok("a **b** c")[1].Text);
    }

    [Fact]
    public void UnderscoreEmphasis_IsRecognized()
    {
        Assert.Equal(MarkdownInlineKind.Italic, Tok("an _emphasised_ word")[1].Kind);
        Assert.Equal(MarkdownInlineKind.Bold, Tok("a __strong__ word")[1].Kind);
    }

    [Fact]
    public void UnderscoreInsideWord_IsNotEmphasis()
    {
        var t = Tok("call request_code_review now");
        Assert.Single(t);
        Assert.Equal(MarkdownInlineKind.Text, t[0].Kind);
        Assert.Equal("call request_code_review now", t[0].Text);
    }

    [Fact]
    public void SpacedAsterisks_AreNotItalic()
    {
        // "2 * 3 * 4" must not become "3" in italics.
        var t = Tok("2 * 3 * 4");
        Assert.Single(t);
        Assert.Equal("2 * 3 * 4", t[0].Text);
    }

    [Fact]
    public void UnterminatedEmphasis_StaysLiteral()
    {
        var t = Tok("a **bold that never closes");
        Assert.Single(t);
        Assert.Equal("a **bold that never closes", t[0].Text);
    }

    [Fact]
    public void BackslashEscape_KeepsMarkerLiteral()
    {
        var t = Tok(@"not \*italic\* here");
        Assert.Single(t);
        Assert.Equal("not *italic* here", t[0].Text);
    }

    [Fact]
    public void InlineCode_IsExtractedAndTrimmed()
    {
        var t = Tok("run `dotnet build` please");
        Assert.Equal(MarkdownInlineKind.Code, t[1].Kind);
        Assert.Equal("dotnet build", t[1].Text);
    }

    [Fact]
    public void DoubleBacktickCode_MayContainSingleBacktick()
    {
        var t = Tok("use ``a`b`` here");
        Assert.Equal(MarkdownInlineKind.Code, t[1].Kind);
        Assert.Equal("a`b", t[1].Text);
    }

    [Fact]
    public void Strikethrough_IsRecognized()
    {
        var t = Tok("this ~~was wrong~~ ok");
        Assert.Equal(MarkdownInlineKind.Strikethrough, t[1].Kind);
        Assert.Equal("was wrong", t[1].Text);
    }

    [Fact]
    public void Link_SplitsTextAndUrl()
    {
        var t = Tok("see [the docs](https://example.com/x) now");
        Assert.Equal(MarkdownInlineKind.Link, t[1].Kind);
        Assert.Equal("the docs", t[1].Text);
        Assert.Equal("https://example.com/x", t[1].Url);
    }

    [Fact]
    public void Link_WithTitle_DropsTheTitle()
    {
        var t = Tok("[x](https://example.com \"a title\")");
        Assert.Equal(MarkdownInlineKind.Link, t[0].Kind);
        Assert.Equal("https://example.com", t[0].Url);
    }

    [Fact]
    public void Image_IsRecognizedWithAltAndUrl()
    {
        var t = Tok("![a cat](https://img/cat.png)");
        Assert.Equal(MarkdownInlineKind.Image, t[0].Kind);
        Assert.Equal("a cat", t[0].Text);
        Assert.Equal("https://img/cat.png", t[0].Url);
    }

    [Fact]
    public void BareUrl_BecomesLink_WithoutTrailingPunctuation()
    {
        var t = Tok("go to https://example.com/page, then stop");
        Assert.Equal(MarkdownInlineKind.Link, t[1].Kind);
        Assert.Equal("https://example.com/page", t[1].Url);
        Assert.Equal(", then stop", t[2].Text);
    }

    [Fact]
    public void AngleAutolink_IsRecognized()
    {
        var t = Tok("<https://example.com>");
        Assert.Single(t);
        Assert.Equal(MarkdownInlineKind.Link, t[0].Kind);
        Assert.Equal("https://example.com", t[0].Url);
    }

    [Fact]
    public void BrTag_BecomesLineBreak()
    {
        var t = Tok("line one<br>line two");
        Assert.Equal(MarkdownInlineKind.Text, t[0].Kind);
        Assert.Equal(MarkdownInlineKind.LineBreak, t[1].Kind);
        Assert.Equal("line two", t[2].Text);
    }

    [Fact]
    public void MixedRun_KeepsOrderAndSurroundingText()
    {
        var t = Tok("A **b** and `c` end");
        Assert.Equal(MarkdownInlineKind.Text, t[0].Kind);
        Assert.Equal("A ", t[0].Text);
        Assert.Equal(MarkdownInlineKind.Bold, t[1].Kind);
        Assert.Equal(" and ", t[2].Text);
        Assert.Equal(MarkdownInlineKind.Code, t[3].Kind);
        Assert.Equal(" end", t[4].Text);
    }

    [Fact]
    public void NullOrEmpty_YieldsNoTokens()
    {
        Assert.Empty(MarkdownInline.Tokenize(null));
        Assert.Empty(MarkdownInline.Tokenize(""));
    }

    [Theory]
    [InlineData("#Correct#", "Correct")]
    [InlineData("#Incorrect#", "Incorrect")]
    [InlineData("#Partially Correct#", "Partially Correct")]
    [InlineData("#WRONG#", "WRONG")]
    public void VerdictMarker_IsRecognized(string input, string expectedLabel)
    {
        var t = Tok(input);
        Assert.Single(t);
        Assert.Equal(MarkdownInlineKind.Verdict, t[0].Kind);
        Assert.Equal(expectedLabel, t[0].Text);
    }

    [Fact]
    public void VerdictMarker_KeepsSurroundingText()
    {
        var t = Tok("Final Rating: #Partially Correct#");
        Assert.Equal(2, t.Length);
        Assert.Equal((MarkdownInlineKind.Text, "Final Rating: ", (string?)null), t[0]);
        Assert.Equal(MarkdownInlineKind.Verdict, t[1].Kind);
        Assert.Equal("Partially Correct", t[1].Text);
    }

    [Theory]
    [InlineData("issue #42 fixed")]              // digits, no closing #
    [InlineData("color #FF0000 here")]           // hex-ish, digits
    [InlineData("#include <stdio.h>")]           // not in the verdict vocabulary
    [InlineData("rated a#Correct#thing")]        // glued to surrounding words
    [InlineData("#Almost There#")]               // unknown label of the right shape
    public void NonVerdictHashes_StayLiteral(string input)
    {
        Assert.DoesNotContain(MarkdownInline.Tokenize(input), t => t.Kind == MarkdownInlineKind.Verdict);
    }
}
