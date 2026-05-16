using System.Buffers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace JulesClient.Services;

public partial class DiffParser
{
    public static ParsedPatch Parse(string patch)
    {
        var res = new ParsedPatch { Files = new() };
        if (string.IsNullOrWhiteSpace(patch)) return res;

        ParsedFile? cf = null;
        ParsedHunk? ch = null;
        int ol = 0, nl = 0;

        var span = patch.AsSpan();
        while (span.Length > 0)
        {
            int lineEnd = span.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> lineSpan = lineEnd >= 0 ? span[..lineEnd] : span;

            if (lineEnd >= 0)
            {
                int skip = lineEnd;
                if (skip < span.Length && span[skip] == '\r') skip++;
                if (skip < span.Length && span[skip] == '\n') skip++;
                span = span[skip..];
            }
            else
            {
                span = ReadOnlySpan<char>.Empty;
            }

            if (lineSpan.IsEmpty) continue;

            if (lineSpan.StartsWith("diff --git a/"))
            {
                var rest = lineSpan["diff --git a/".Length..];
                int bIdx = rest.IndexOf(" b/");
                string oldPath = bIdx >= 0 ? rest[..bIdx].ToString() : rest.ToString();
                string newPath = bIdx >= 0 ? rest[(bIdx + 3)..].ToString() : oldPath;
                cf = new() { OldPath = oldPath, NewPath = newPath, Hunks = new() };
                res.Files.Add(cf);
                ch = null;
                continue;
            }

            if (cf == null) continue;

            if (lineSpan.StartsWith("@@ -"))
            {
                int plusIdx = lineSpan.IndexOf('+');
                if (plusIdx > 0)
                {
                    var afterPlus = lineSpan[(plusIdx + 1)..];
                    int spaceIdx = afterPlus.IndexOf(' ');
                    int endIdx = spaceIdx >= 0 ? plusIdx + 1 + spaceIdx : lineSpan.Length;

                    var oldPart = lineSpan[3..plusIdx];
                    var newPart = lineSpan[(plusIdx + 1)..endIdx];

                    int oldComma = oldPart.IndexOf(',');
                    ol = oldComma >= 0 ? int.Parse(oldPart[..oldComma]) : int.Parse(oldPart);

                    int newComma = newPart.IndexOf(',');
                    nl = newComma >= 0 ? int.Parse(newPart[..newComma]) : int.Parse(newPart);
                }

                ch = new() { Header = lineSpan.ToString(), Lines = new() };
                cf.Hunks.Add(ch);
                continue;
            }

            if (ch == null) continue;

            if (lineSpan.Length == 0) continue;

            char first = lineSpan[0];
            var content = lineSpan.Length > 1 ? lineSpan[1..].ToString() : "";

            var dl = first switch
            {
                '+' => new ParsedLine { Type = DiffLineType.Added, Content = content, OldLineNumber = null, NewLineNumber = nl++ },
                '-' => new ParsedLine { Type = DiffLineType.Removed, Content = content, OldLineNumber = ol++, NewLineNumber = null },
                ' ' => new ParsedLine { Type = DiffLineType.Context, Content = content, OldLineNumber = ol++, NewLineNumber = nl++ },
                '\\' => new ParsedLine { Type = DiffLineType.Metadata, Content = lineSpan.ToString(), OldLineNumber = null, NewLineNumber = null },
                _ => new ParsedLine { Type = DiffLineType.Unknown, Content = lineSpan.ToString(), OldLineNumber = null, NewLineNumber = null }
            };
            ch.Lines.Add(dl);
        }

        return res;
    }

    public static ParsedPatch Merge(IEnumerable<string> patches)
    {
        var result = new ParsedPatch();
        var filesMap = new Dictionary<string, ParsedFile>();

        foreach (var patchStr in patches)
        {
            var patch = Parse(patchStr);
            foreach (var file in patch.Files)
            {
                if (!filesMap.TryGetValue(file.NewPath, out var existing))
                {
                    existing = new ParsedFile { OldPath = file.OldPath, NewPath = file.NewPath, Hunks = new() };
                    filesMap[file.NewPath] = existing;
                    result.Files.Add(existing);
                }
                existing.Hunks.AddRange(file.Hunks);
            }
        }
        return result;
    }

    public static List<DiffFileNode> BuildFileTree(ParsedPatch patch)
    {
        var result = new List<DiffFileNode>(patch.Files.Count);
        foreach (var file in patch.Files)
        {
            var fileNode = new DiffFileNode(file);
            result.Add(fileNode);
        }
        return result;
    }
}

public record ParsedPatch { public List<ParsedFile> Files { get; init; } = new(); }
public record ParsedFile { public string OldPath { get; init; } = ""; public string NewPath { get; init; } = ""; public List<ParsedHunk> Hunks { get; init; } = new(); }
public record ParsedHunk { public string Header { get; init; } = ""; public List<ParsedLine> Lines { get; init; } = new(); }
public record ParsedLine { public DiffLineType Type { get; init; } public string Content { get; init; } = ""; public int? OldLineNumber { get; init; } public int? NewLineNumber { get; init; } }
public enum DiffLineType { Added, Removed, Context, Metadata, Unknown, FileHeader, HunkHeader }

public class DiffFileNode
{
    public ParsedFile File { get; }
    public bool IsExpanded { get; set; }
    public int TotalLines { get; }
    public int AddedLines { get; }
    public int RemovedLines { get; }

    public DiffFileNode(ParsedFile file)
    {
        File = file;
        int total = 0, added = 0, removed = 0;
        foreach (var hunk in file.Hunks)
        {
            foreach (var line in hunk.Lines)
            {
                total++;
                if (line.Type == DiffLineType.Added) added++;
                else if (line.Type == DiffLineType.Removed) removed++;
            }
        }
        TotalLines = total;
        AddedLines = added;
        RemovedLines = removed;
    }

    public string DisplayName
    {
        get
        {
            if (File.OldPath == File.NewPath) return File.NewPath;
            return $"{File.OldPath} → {File.NewPath}";
        }
    }

    public string Stats => $"+{AddedLines} -{RemovedLines}";
}

public class DiffHunkNode
{
    public ParsedHunk Hunk { get; }
    public string Header { get; }

    public DiffHunkNode(ParsedHunk hunk)
    {
        Hunk = hunk;
        Header = hunk.Header;
    }
}
