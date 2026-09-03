using JulesClient.Services;
using Xunit;

namespace JulesClient.Tests;

public class DiffParserTests
{
    [Fact]
    public void Parse_ValidPatch_ReturnsParsedPatch()
    {
        string patch = "diff --git a/file.txt b/file.txt\n" +
                       "--- a/file.txt\n" +
                       "+++ b/file.txt\n" +
                       "@@ -1,1 +1,1 @@\n" +
                       "-old\n" +
                       "+new\n";

        var result = DiffParser.Parse(patch);

        Assert.Single(result.Files);
        Assert.Equal("file.txt", result.Files[0].NewPath);
        Assert.Single(result.Files[0].Hunks);
        Assert.Equal(2, result.Files[0].Hunks[0].Lines.Count);
    }

    [Fact]
    public void Merge_MultiplePatches_ReturnsMergedPatch()
    {
        string patch1 = "diff --git a/file1.txt b/file1.txt\n" +
                        "@@ -1,1 +1,1 @@\n" +
                        "-old1\n" +
                        "+new1\n";
        string patch2 = "diff --git a/file2.txt b/file2.txt\n" +
                        "@@ -1,1 +1,1 @@\n" +
                        "-old2\n" +
                        "+new2\n";

        var result = DiffParser.Merge(new[] { patch1, patch2 });

        Assert.Equal(2, result.Files.Count);
        Assert.Equal("file1.txt", result.Files[0].NewPath);
        Assert.Equal("file2.txt", result.Files[1].NewPath);
    }

    [Fact]
    public void BuildFileTree_ComputesPerFileStats()
    {
        string patch = "diff --git a/file.txt b/file.txt\n" +
                       "@@ -1,2 +1,3 @@\n" +
                       " context\n" +
                       "-old\n" +
                       "+new1\n" +
                       "+new2\n";
        var tree = DiffParser.BuildFileTree(DiffParser.Parse(patch));

        Assert.Single(tree);
        Assert.Equal("file.txt", tree[0].DisplayName);
        Assert.Equal(2, tree[0].AddedLines);
        Assert.Equal(1, tree[0].RemovedLines);
        Assert.Equal(4, tree[0].TotalLines);
    }

    [Fact]
    public void DiffFileNode_ShowsRenameArrow()
    {
        string patch = "diff --git a/old/name.txt b/new/name.txt\n" +
                       "@@ -1,1 +1,1 @@\n" +
                       "-a\n" +
                       "+b\n";
        var tree = DiffParser.BuildFileTree(DiffParser.Parse(patch));

        Assert.Equal("old/name.txt → new/name.txt", tree[0].DisplayName);
    }

    private static string GitPatch(params string[] files)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var f in files)
        {
            sb.Append($"diff --git a/{f} b/{f}\n--- a/{f}\n+++ b/{f}\n@@ -1,1 +1,1 @@\n-x\n+y\n");
        }
        return sb.ToString();
    }

    [Fact]
    public void ChangedFilePaths_ListsEachFileOnce_InOrder()
    {
        var paths = DiffParser.ChangedFilePaths(GitPatch("src/A.cs", "docs/B.md"));
        Assert.Equal(new[] { "src/A.cs", "docs/B.md" }, paths);
    }

    [Fact]
    public void ChangedFilePaths_FallsBackToPlusHeader_WithoutGitLine()
    {
        var patch = "--- a/only.txt\n+++ b/only.txt\n@@ -1,1 +1,1 @@\n-a\n+b\n";
        Assert.Equal(new[] { "only.txt" }, DiffParser.ChangedFilePaths(patch));
    }

    [Fact]
    public void ChangedFilePaths_MultiFile_PlainDiff_NoGitLine()
    {
        var patch =
            "--- a/one.cs\n+++ b/one.cs\n@@ -1 +1 @@\n-x\n+y\n" +
            "--- a/two/three.cs\n+++ b/two/three.cs\n@@ -1 +1 @@\n-a\n+b\n";
        Assert.Equal(new[] { "one.cs", "two/three.cs" }, DiffParser.ChangedFilePaths(patch));
    }

    [Fact]
    public void ChangedFilePaths_PlusHeader_WithoutBPrefix()
    {
        var patch = "--- src/x.cpp\n+++ src/x.cpp\n@@ -1 +1 @@\n-a\n+b\n";
        Assert.Equal(new[] { "src/x.cpp" }, DiffParser.ChangedFilePaths(patch));
    }

    [Fact]
    public void ChangedFilePaths_HandlesDeletion_UsingMinusPath()
    {
        var patch = "--- a/gone.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-a\n";
        Assert.Equal(new[] { "gone.txt" }, DiffParser.ChangedFilePaths(patch));
    }

    [Fact]
    public void ChangedFilePaths_MixedGitAndPlainHeaders()
    {
        var patch =
            "diff --git a/g.cs b/g.cs\n--- a/g.cs\n+++ b/g.cs\n@@ -1 +1 @@\n-x\n+y\n" +
            "--- a/p.cs\n+++ b/p.cs\n@@ -1 +1 @@\n-a\n+b\n";
        Assert.Equal(new[] { "g.cs", "p.cs" }, DiffParser.ChangedFilePaths(patch));
    }

    [Fact]
    public void ChangedFilePaths_NotFooledByContentLines()
    {
        // A file whose content has "-- x" / "++ y" lines: the diff shows
        // "--- x" / "+++ y" as *content*, not a header (no matching path, no @@).
        var patch = "--- a/lua.lua\n+++ b/lua.lua\n@@ -1,2 +1,2 @@\n--- x\n+++ y\n";
        Assert.Equal(new[] { "lua.lua" }, DiffParser.ChangedFilePaths(patch));
    }

    [Theory]
    [InlineData("**Updated** `src/A.cs`", "src/A.cs")]
    [InlineData("**Updated** `src/A.cs` and `docs/B.md`", "src/A.cs", "docs/B.md")]
    [InlineData("**Updated** `A.cs`, `B.md` and `C.txt`", "A.cs", "B.md", "C.txt")]
    [InlineData("**Updated** `A.cs`, `B.md`, `C.txt` and 1 more file", "A.cs", "B.md", "C.txt", "D.txt")]
    [InlineData("**Updated** `A.cs`, `B.md`, `C.txt` and 3 more files", "A.cs", "B.md", "C.txt", "D.txt", "E.txt", "F.txt")]
    public void SummarizeChange_FormatsFileList(string expected, params string[] files)
    {
        Assert.Equal(expected, DiffParser.SummarizeChange(GitPatch(files)));
    }

    [Fact]
    public void SummarizeChange_ReturnsNull_ForEmptyOrNoFiles()
    {
        Assert.Null(DiffParser.SummarizeChange(null));
        Assert.Null(DiffParser.SummarizeChange(""));
        Assert.Null(DiffParser.SummarizeChange("just some text, no diff"));
    }

    [Fact]
    public void SummarizeFiles_TakesAPreFilteredList()
    {
        Assert.Equal("**Updated** `x/new.cs`", DiffParser.SummarizeFiles(new[] { "x/new.cs" }));
        Assert.Null(DiffParser.SummarizeFiles(System.Array.Empty<string>()));
    }

    [Fact]
    public void FilePatchBodies_SplitsPerFile_AndDetectsChangeBetweenSnapshots()
    {
        var s1 =
            "diff --git a/x.cs b/x.cs\n--- a/x.cs\n+++ b/x.cs\n@@ -1 +1 @@\n-a\n+b\n" +
            "diff --git a/y.cs b/y.cs\n--- a/y.cs\n+++ b/y.cs\n@@ -1 +1 @@\n-c\n+d\n";
        // Same snapshot but x.cs revised further; y.cs untouched.
        var s2 =
            "diff --git a/x.cs b/x.cs\n--- a/x.cs\n+++ b/x.cs\n@@ -1 +1 @@\n-a\n+B\n" +
            "diff --git a/y.cs b/y.cs\n--- a/y.cs\n+++ b/y.cs\n@@ -1 +1 @@\n-c\n+d\n";

        var b1 = DiffParser.FilePatchBodies(s1);
        var b2 = DiffParser.FilePatchBodies(s2);

        Assert.Equal(new[] { "x.cs", "y.cs" }, b1.Select(x => x.Path));
        Assert.NotEqual(b1[0].Body, b2[0].Body); // x.cs differs
        Assert.Equal(b1[1].Body, b2[1].Body);    // y.cs identical
    }

    [Fact]
    public void BodyHasHunks_TrueOnlyForTextChanges()
    {
        var textFile = "diff --git a/x.cs b/x.cs\n--- a/x.cs\n+++ b/x.cs\n@@ -1 +1 @@\n-a\n+b\n";
        var binaryFile = "diff --git a/test_bin b/test_bin\nindex 111..222 100755\nBinary files a/test_bin and b/test_bin differ\n";

        Assert.True(DiffParser.BodyHasHunks(DiffParser.FilePatchBodies(textFile)[0].Body));
        Assert.False(DiffParser.BodyHasHunks(DiffParser.FilePatchBodies(binaryFile)[0].Body));
        Assert.False(DiffParser.BodyHasHunks(null));
    }
}
