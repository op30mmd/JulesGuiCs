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
    public void Flatten_ParsedPatch_ReturnsFlattenedItems()
    {
        string patch = "diff --git a/file.txt b/file.txt\n" +
                       "@@ -1,1 +1,1 @@\n" +
                       "-old\n" +
                       "+new\n";
        var parsed = DiffParser.Parse(patch);

        var flattened = DiffParser.Flatten(parsed).ToList();

        // 1 FileHeader, 1 HunkHeader, 2 Lines = 4 total
        Assert.Equal(4, flattened.Count);
        Assert.Equal(DiffLineType.FileHeader, flattened[0].Type);
        Assert.Equal("file.txt", flattened[0].Content);
        Assert.Equal(DiffLineType.HunkHeader, flattened[1].Type);
        Assert.Equal(DiffLineType.Removed, flattened[2].Type);
        Assert.Equal(DiffLineType.Added, flattened[3].Type);
    }
}
