using System;
using System.Collections.Generic;

namespace JulesClient.Services;

/// <summary>
/// A search/replace edit block in the merge-conflict form that code agents emit:
/// <c>&lt;&lt;&lt;&lt;&lt;&lt;&lt; SEARCH</c> … <c>=======</c> … <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt; REPLACE</c>.
/// </summary>
public sealed record ConflictBlock(string? Language, string Search, string Replace);

/// <summary>One slice of a Markdown string: plain markdown to render when
/// <see cref="Conflict"/> is null, otherwise the extracted edit block (and
/// <see cref="Text"/> holds the raw block for copy/debug).</summary>
public sealed record MarkdownSegment(string Text, ConflictBlock? Conflict);

/// <summary>
/// Splits Markdown text into renderable segments, pulling out any
/// merge-conflict-style search/replace blocks so the UI can show them in their
/// own collapsible container instead of as an opaque code fence.
/// </summary>
public static class MarkdownConflictParser
{
    public static IReadOnlyList<MarkdownSegment> Split(string? text)
    {
        var segments = new List<MarkdownSegment>();
        if (string.IsNullOrEmpty(text)) return segments;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var normal = new List<string>();

        void FlushNormal()
        {
            if (normal.Count == 0) return;
            var joined = string.Join("\n", normal).Trim('\n');
            if (joined.Trim().Length > 0)
                segments.Add(new MarkdownSegment(joined, null));
            normal.Clear();
        }

        int i = 0;
        while (i < lines.Length)
        {
            if (IsMarker(lines[i], '<'))
            {
                int sep = FindSeparator(lines, i + 1);
                int end = sep < 0 ? -1 : FindEnd(lines, sep + 1);

                if (sep >= 0 && end >= 0)
                {
                    // A fenced-block opener on the preceding kept line belongs to
                    // this block (and its closing fence, just after, is dropped too).
                    string? lang = null;
                    bool hadFence = normal.Count > 0 && IsFence(normal[^1], out lang);
                    if (hadFence) normal.RemoveAt(normal.Count - 1);

                    FlushNormal();

                    var search = string.Join("\n", Slice(lines, i + 1, sep));
                    var replace = string.Join("\n", Slice(lines, sep + 1, end));
                    var raw = string.Join("\n", Slice(lines, i, end + 1));

                    segments.Add(new MarkdownSegment(raw, new ConflictBlock(
                        string.IsNullOrWhiteSpace(lang) ? null : lang!.Trim(), search, replace)));

                    i = end + 1;
                    if (hadFence && i < lines.Length && IsFence(lines[i], out _)) i++;
                    continue;
                }
            }

            normal.Add(lines[i]);
            i++;
        }

        FlushNormal();
        return segments;
    }

    private static string[] Slice(string[] lines, int start, int endExclusive)
    {
        int count = Math.Max(0, endExclusive - start);
        var result = new string[count];
        Array.Copy(lines, start, result, 0, count);
        return result;
    }

    // The separator '=======' must come before any other conflict marker.
    private static int FindSeparator(string[] lines, int from)
    {
        for (int i = from; i < lines.Length; i++)
        {
            if (IsMarker(lines[i], '=')) return i;
            if (IsMarker(lines[i], '<') || IsMarker(lines[i], '>')) return -1;
        }
        return -1;
    }

    private static int FindEnd(string[] lines, int from)
    {
        for (int i = from; i < lines.Length; i++)
        {
            if (IsMarker(lines[i], '>')) return i;
            if (IsMarker(lines[i], '<') || IsMarker(lines[i], '=')) return -1;
        }
        return -1;
    }

    private static bool IsMarker(string line, char ch)
    {
        var t = line.TrimStart();
        int run = 0;
        while (run < t.Length && t[run] == ch) run++;
        if (run < 7) return false;
        // "=======" stands on its own line; "<<<<<<<"/">>>>>>>" may carry a label.
        return ch != '=' || t.TrimEnd().Length == run;
    }

    private static bool IsFence(string line, out string? lang)
    {
        lang = null;
        var t = line.Trim();
        if (!t.StartsWith("```")) return false;
        var rest = t.TrimStart('`').Trim();
        lang = rest.Length == 0 ? null : rest;
        return true;
    }
}
