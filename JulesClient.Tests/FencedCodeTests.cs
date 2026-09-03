using System.Linq;
using JulesClient.Services;

namespace JulesClient.Tests;

public class FencedCodeTests
{
    [Fact]
    public void SplitsBlock_FromSurroundingProse()
    {
        var segs = FencedCode.Split("before\n\n```js\nconst x = 1;\n```\n\nafter");

        Assert.Equal(3, segs.Count);
        Assert.Null(segs[0].Code);
        Assert.Equal("before", segs[0].Text);
        Assert.NotNull(segs[1].Code);
        Assert.Equal("js", segs[1].Code!.Language);
        Assert.Equal("const x = 1;", segs[1].Code!.Code);
        Assert.Null(segs[2].Code);
        Assert.Equal("after", segs[2].Text);
    }

    [Fact]
    public void InfoString_KeepsOnlyFirstToken_AsLanguage()
    {
        var segs = FencedCode.Split("```ts  title=\"a.ts\"\nlet a = 1;\n```");
        Assert.Single(segs);
        Assert.Equal("ts", segs[0].Code!.Language);
    }

    [Fact]
    public void NoLanguage_YieldsNullLanguage()
    {
        var segs = FencedCode.Split("```\nplain\n```");
        Assert.Null(segs[0].Code!.Language);
        Assert.Equal("plain", segs[0].Code!.Code);
    }

    [Fact]
    public void TildeFences_AreSupported()
    {
        var segs = FencedCode.Split("~~~python\nx = 1\n~~~");
        Assert.Single(segs);
        Assert.Equal("python", segs[0].Code!.Language);
        Assert.Equal("x = 1", segs[0].Code!.Code);
    }

    [Fact]
    public void UnclosedFence_StaysAsPlainText()
    {
        var input = "text\n```js\nconst x = 1;";
        var segs = FencedCode.Split(input);
        Assert.Single(segs);
        Assert.Null(segs[0].Code);
        Assert.Equal(input, segs[0].Text);
    }

    [Fact]
    public void ShorterInnerFence_DoesNotClose_LongerOuterFence()
    {
        var segs = FencedCode.Split("````\n```\nnested\n```\n````");
        Assert.Single(segs);
        Assert.Equal("```\nnested\n```", segs[0].Code!.Code);
    }

    [Fact]
    public void IndentedFence_DeIndentsBody()
    {
        var segs = FencedCode.Split("  ```\n  a\n    b\n  ```");
        Assert.Equal("a\n  b", segs[0].Code!.Code);
    }

    [Fact]
    public void PlainText_WithNoFence_IsOneSegment()
    {
        var segs = FencedCode.Split("just a sentence with `inline code`.");
        Assert.Single(segs);
        Assert.Null(segs[0].Code);
    }

    [Fact]
    public void Empty_YieldsNoSegments()
    {
        Assert.Empty(FencedCode.Split(null));
        Assert.Empty(FencedCode.Split(""));
    }
}
