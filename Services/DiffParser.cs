using System.Buffers;
using System.Text;

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

    // The distinct file paths touched by a unified diff, in first-seen order.
    public static IReadOnlyList<string> ChangedFilePaths(string? patch)
    {
        var paths = new List<string>();
        foreach (var (p, _) in FilePatchBodies(patch))
        {
            if (!paths.Contains(p)) paths.Add(p);
        }
        return paths;
    }

    // Splits a unified diff into per-file sections: (path, that file's portion of
    // the diff). Handles git ("diff --git a/x b/x"), plain ("--- a/x" / "+++ b/x"
    // / "@@") and mixed headers. Used to tell which files actually changed between
    // two snapshots of an evolving changeset.
    public static List<(string Path, string Body)> FilePatchBodies(string? patch)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(patch)) return result;

        var lines = patch.Replace("\r\n", "\n").Split('\n');
        string? curPath = null;
        var body = new StringBuilder();

        void Flush()
        {
            if (curPath != null) result.Add((curPath, body.ToString()));
            body.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                Flush();
                var rest = line["diff --git ".Length..];
                int b = rest.LastIndexOf(" b/", StringComparison.Ordinal);
                curPath = NormalizePathToken(b > 0 ? rest[(b + 3)..] : rest);
                continue;
            }

            // Plain header triplet: "--- X" / "+++ Y" / "@@ ..." naming the same
            // file (or one side /dev/null). The same-path check keeps content
            // lines like "--- foo" / "+++ bar" from being read as a header.
            if (line.StartsWith("--- ", StringComparison.Ordinal)
                && i + 2 < lines.Length
                && lines[i + 1].StartsWith("+++ ", StringComparison.Ordinal)
                && lines[i + 2].StartsWith("@@ ", StringComparison.Ordinal))
            {
                var minus = NormalizePathToken(line[4..]);
                var plus = NormalizePathToken(lines[i + 1][4..]);
                if (minus == plus || minus == "/dev/null" || plus == "/dev/null")
                {
                    var p = !string.IsNullOrEmpty(plus) && plus != "/dev/null" ? plus : minus;
                    if (p != curPath)
                    {
                        Flush();
                        curPath = p;
                    }
                    body.Append(lines[i + 2]).Append('\n');
                    i += 2;
                    continue;
                }
            }

            if (curPath != null) body.Append(line).Append('\n');
        }

        Flush();
        return result;
    }

    // Turns a "--- "/"+++ " token into a bare path: drops a trailing tab-separated
    // timestamp, surrounding quotes, and a leading "a/" or "b/".
    private static string? NormalizePathToken(string? token)
    {
        if (token is null) return null;

        var s = token.Trim();
        int tab = s.IndexOf('\t');
        if (tab >= 0) s = s[..tab].TrimEnd();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') s = s[1..^1];
        if (s == "/dev/null") return s;
        if (s.StartsWith("a/", StringComparison.Ordinal) || s.StartsWith("b/", StringComparison.Ordinal))
        {
            s = s[2..];
        }
        return s;
    }

    // True when a per-file body from FilePatchBodies contains at least one hunk,
    // i.e. real line changes rather than a binary or mode-only change.
    public static bool BodyHasHunks(string? body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("@@ ", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    // A Markdown one-liner summarising a diff - "**Updated** `a` and `b`" with
    // the repo-relative paths as inline code - or null if it touches no files.
    public static string? SummarizeChange(string? patch) => SummarizeFiles(ChangedFilePaths(patch));

    // As above, for an already-resolved (e.g. pre-filtered) list of paths. At
    // most three are listed; the rest collapse to "and N more file(s)".
    public static string? SummarizeFiles(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return null;

        static string Chip(string p) => "`" + p + "`";
        int extra = paths.Count - 3;

        var list = paths.Count switch
        {
            1 => Chip(paths[0]),
            2 => $"{Chip(paths[0])} and {Chip(paths[1])}",
            3 => $"{Chip(paths[0])}, {Chip(paths[1])} and {Chip(paths[2])}",
            _ => $"{Chip(paths[0])}, {Chip(paths[1])}, {Chip(paths[2])} and " +
                 (extra == 1 ? "1 more file" : $"{extra} more files"),
        };
        return "**Updated** " + list;
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
public enum DiffLineType { Added, Removed, Context, Metadata, Unknown }

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
