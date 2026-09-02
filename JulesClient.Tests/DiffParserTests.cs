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
}
