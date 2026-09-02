using System;
using System.Collections.Generic;
using System.Text;

namespace JulesClient.Services;

public static class MarkdownText
{
    /// <summary>
    /// Drops a short "Label:" line when the next non-blank line restates the same
    /// label with more content. Code-review output sometimes emits both a heading
    /// (or bold) "Final Rating:" and a "**Final Rating:** #Correct#" line right
    /// after it, which renders as a visible duplicate.
    /// </summary>
    public static string[] DedupeRepeatedLabels(string[] lines)
    {
        if (lines.Length < 2) return lines;

        var drop = new bool[lines.Length];

        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var label = StripDecoration(lines[i]);
            if (label.Length < 2 || label.Length > 60) continue;
            if (!label.EndsWith(":", StringComparison.Ordinal)) continue;
            if (label.Contains(". ", StringComparison.Ordinal)) continue; // a sentence, not a label

            int j = i + 1;
            while (j < lines.Length && string.IsNullOrWhiteSpace(lines[j])) j++;
            if (j >= lines.Length) continue;

            var next = StripDecoration(lines[j]);
            if (next.Length > label.Length && next.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                drop[i] = true;
        }

        var result = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
            if (!drop[i]) result.Add(lines[i]);

        return result.ToArray();
    }

    // Strips an ATX heading prefix and *, _, ` decoration so two spellings of the
    // same label compare equal ("### Final Rating:" == "**Final Rating:**").
    private static string StripDecoration(string line)
    {
        var s = line.Trim();

        int h = 0;
        while (h < s.Length && s[h] == '#') h++;
        if (h > 0 && h < s.Length && s[h] == ' ') s = s.Substring(h + 1);

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (c != '*' && c != '_' && c != '`') sb.Append(c);

        return sb.ToString().Trim();
    }
}
