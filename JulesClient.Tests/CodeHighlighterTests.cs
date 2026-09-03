using System.Linq;
using JulesClient.Services;

namespace JulesClient.Tests;

public class CodeHighlighterTests
{
    private static string TextOf(IEnumerable<CodeToken> toks, CodeTokenKind kind) =>
        string.Concat(toks.Where(t => t.Kind == kind).Select(t => t.Text));

    private static bool Has(IEnumerable<CodeToken> toks, CodeTokenKind kind, string fragment) =>
        toks.Any(t => t.Kind == kind && t.Text.Contains(fragment));

    [Fact]
    public void Reconstructs_Source_Exactly()
    {
        const string src = "int x = 0x1F; // note\nstd::string s = \"hi\";";
        var toks = CodeHighlighter.Highlight(src, "cpp");
        Assert.Equal(src, string.Concat(toks.Select(t => t.Text)));
    }

    [Fact]
    public void Cpp_Keywords_Types_Strings_Comments_Numbers()
    {
        const string src = "static std::vector<std::string> f() { return \"x\"; } // done";
        var toks = CodeHighlighter.Highlight(src, "cpp");

        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "static");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "return");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Type && t.Text == "std");
        Assert.True(Has(toks, CodeTokenKind.String, "\"x\""));
        Assert.True(Has(toks, CodeTokenKind.Comment, "// done"));
    }

    [Fact]
    public void Preprocessor_LineIsFlagged_ForCFamily()
    {
        const string src = "#if defined(__linux__)\nint fd = 0;\n#endif";
        var toks = CodeHighlighter.Highlight(src, "cpp");

        Assert.True(Has(toks, CodeTokenKind.Preprocessor, "#if defined(__linux__)"));
        Assert.True(Has(toks, CodeTokenKind.Preprocessor, "#endif"));
        // The '#' line must not swallow the code line between the directives.
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Number && t.Text.StartsWith("0"));
    }

    [Fact]
    public void Hash_IsComment_ForPython_NotPreprocessor()
    {
        var toks = CodeHighlighter.Highlight("x = 1  # a comment", "python");
        Assert.True(Has(toks, CodeTokenKind.Comment, "# a comment"));
        Assert.DoesNotContain(toks, t => t.Kind == CodeTokenKind.Preprocessor);
    }

    [Fact]
    public void BlockComment_SpansLines()
    {
        var toks = CodeHighlighter.Highlight("a /* one\ntwo */ b", "c");
        Assert.True(Has(toks, CodeTokenKind.Comment, "/* one\ntwo */"));
    }

    [Fact]
    public void UnterminatedString_StopsAtLineEnd()
    {
        var toks = CodeHighlighter.Highlight("s = \"oops\nnext", "cpp");
        Assert.Equal("\"oops", TextOf(toks, CodeTokenKind.String));
    }

    [Fact]
    public void HexAndFloatLiterals_AreNumbers()
    {
        var toks = CodeHighlighter.Highlight("a=0xDEAD; b=3.14f; c=1e9;", "cpp");
        Assert.True(Has(toks, CodeTokenKind.Number, "0xDEAD"));
        Assert.True(Has(toks, CodeTokenKind.Number, "3.14f"));
        Assert.True(Has(toks, CodeTokenKind.Number, "1e9"));
    }

    [Fact]
    public void IdentifierWithDigits_IsNotSplitIntoNumber()
    {
        var toks = CodeHighlighter.Highlight("int md5sum = 1;", "c");
        Assert.DoesNotContain(toks, t => t.Kind == CodeTokenKind.Number && t.Text == "5");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Number && t.Text == "1");
    }

    [Fact]
    public void UnknownLanguage_FallsBackToCFamily()
    {
        var toks = CodeHighlighter.Highlight("return x; // k", null);
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "return");
        Assert.True(Has(toks, CodeTokenKind.Comment, "// k"));
    }

    [Fact]
    public void Empty_YieldsNoTokens()
    {
        Assert.Empty(CodeHighlighter.Highlight(null, "cpp"));
        Assert.Empty(CodeHighlighter.Highlight("", "cpp"));
    }

    [Fact]
    public void Python_UsesPythonKeywords_DefNameIsFunction_NoneIsConstant()
    {
        var toks = CodeHighlighter.Highlight("def parse(x):\n    return None", "python");

        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "def");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "return");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Function && t.Text == "parse");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Constant && t.Text == "None");
    }

    [Fact]
    public void PerLanguage_Set_Wins_Over_CommonUnion()
    {
        // "var" is a value type in the common fallback but a keyword in C#.
        var cs = CodeHighlighter.Highlight("var count = 3;", "csharp");
        Assert.Contains(cs, t => t.Kind == CodeTokenKind.Keyword && t.Text == "var");

        // "range" is a Go keyword; it should not light up as one in C.
        var c = CodeHighlighter.Highlight("int range = 1;", "c");
        Assert.DoesNotContain(c, t => t.Kind == CodeTokenKind.Keyword && t.Text == "range");
    }

    [Fact]
    public void FunctionCallNames_AreHighlighted()
    {
        var toks = CodeHighlighter.Highlight("foo(bar(), 2);", "js");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Function && t.Text == "foo");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Function && t.Text == "bar");
        Assert.Equal("foo(bar(), 2);", string.Concat(toks.Select(t => t.Text)));
    }

    [Fact]
    public void BooleanLiterals_AreConstants()
    {
        var toks = CodeHighlighter.Highlight("bool ok = true;", "cpp");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Type && t.Text == "bool");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Constant && t.Text == "true");
    }

    [Fact]
    public void BinaryOctalAndLeadingDotLiterals_AreNumbers()
    {
        Assert.True(Has(CodeHighlighter.Highlight("a = 0b1010;", "rust"), CodeTokenKind.Number, "0b1010"));
        Assert.True(Has(CodeHighlighter.Highlight("a = 0o17;", "rust"), CodeTokenKind.Number, "0o17"));
        Assert.True(Has(CodeHighlighter.Highlight("x = .5;", "js"), CodeTokenKind.Number, ".5"));
    }

    [Fact]
    public void Sql_Keywords_AreCaseInsensitive()
    {
        var toks = CodeHighlighter.Highlight("Select id From t Where id = 1", "sql");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "Select");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "From");
        Assert.Contains(toks, t => t.Kind == CodeTokenKind.Keyword && t.Text == "Where");
        Assert.Equal("Select id From t Where id = 1", string.Concat(toks.Select(t => t.Text)));
    }
}
