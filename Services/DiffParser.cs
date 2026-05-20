using System.Buffers;

namespace JulesClient.Services;

public partial class DiffParser
{
    private static void ParseHunkRange(ReadOnlySpan<char> rangePart, ref int ol, ref int nl)
    {
        int plusIdx = rangePart.IndexOf('+');
        if (plusIdx > 1)
        {
            var oldRange = rangePart[1..plusIdx].Trim();
            var newRange = rangePart[(plusIdx + 1)..].Trim();

            int oldComma = oldRange.IndexOf(',');
            ol = oldComma >= 0 ? int.Parse(oldRange[..oldComma]) : int.Parse(oldRange);

            int newComma = newRange.IndexOf(',');
            nl = newComma >= 0 ? int.Parse(newRange[..newComma]) : int.Parse(newRange);
        }
    }

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
                int closeIdx = lineSpan[2..].IndexOf("@@");
                string headerFull = lineSpan.ToString();

                if (closeIdx >= 0)
                {
                    var rangePart = lineSpan[3..(closeIdx + 2)].Trim();
                    ParseHunkRange(rangePart, ref ol, ref nl);
                }
                else
                {
                    var rangePart = lineSpan[3..].Trim();
                    ParseHunkRange(rangePart, ref ol, ref nl);
                }

                ch = new() { Header = headerFull, Lines = new() };
                cf.Hunks.Add(ch);
                continue;
            }

            if (ch == null) continue;

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
        var filesMap = new Dictionary<string, ParsedFile>();
        var fileOrder = new List<string>();

        foreach (var patchStr in patches)
        {
            var patch = Parse(patchStr);
            foreach (var file in patch.Files)
            {
                if (!filesMap.ContainsKey(file.NewPath))
                {
                    fileOrder.Add(file.NewPath);
                }

                var latestFile = new ParsedFile
                {
                    OldPath = file.OldPath,
                    NewPath = file.NewPath,
                    Hunks = new List<ParsedHunk>(file.Hunks)
                };
                filesMap[file.NewPath] = latestFile;
            }
        }

        var result = new ParsedPatch { Files = new() };
        foreach (var path in fileOrder)
        {
            result.Files.Add(filesMap[path]);
        }
        return result;
    }

    public static IEnumerable<DiffDisplayItem> Flatten(ParsedPatch patch)
    {
        var result = new List<DiffDisplayItem>();
        foreach (var file in patch.Files)
        {
            result.Add(new DiffDisplayItem(
                DiffLineType.FileHeader,
                file.NewPath,
                null, null
            ));

            foreach (var hunk in file.Hunks)
            {
                result.Add(new DiffDisplayItem(
                    DiffLineType.HunkHeader,
                    hunk.Header,
                    null, null
                ));

                foreach (var line in hunk.Lines)
                {
                    result.Add(new DiffDisplayItem(
                        line.Type,
                        line.Content,
                        line.OldLineNumber,
                        line.NewLineNumber
                    ));
                }
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
public record DiffDisplayItem(DiffLineType Type, string Content, int? OldLineNumber, int? NewLineNumber);
public enum DiffLineType { Added, Removed, Context, Metadata, Unknown, FileHeader, HunkHeader }

public class DiffFileNode
{
    public ParsedFile File { get; }
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
