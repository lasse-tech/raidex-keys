// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.RegularExpressions;

namespace RaidexKeys.AddonPack;

/// <summary>
/// The addon's files without their comments, tidy: a line that held only a
/// comment goes, the source's blank lines stay - one in a row, none at the
/// ends. A Lua error in game therefore names the line of the packed file, as
/// the public repository shows it, not of the source here.
/// </summary>
public static partial class AddonSource
{
    /// <summary>Lua 5.1 without comments: "--" to the end of the line and "--[[ ]]", "--[==[ ]==]" blocks.</summary>
    /// <remarks>Strings - '…', "…" and [[…]] - are copied as they are, "--" inside them included.</remarks>
    public static string StripLua(string source)
    {
        var sb = new StringBuilder(source.Length);
        var verbatim = new HashSet<int>();   // lines inside a string: Tidy leaves them as they are
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '-' && i + 1 < source.Length && source[i + 1] == '-')
            {
                i += 2;
                if (LongBracket(source, i) is { } level)
                {
                    var end = LongBracketEnd(source, i + level + 2, level);
                    var newlines = source.AsSpan(i, end - i).Count('\n');
                    // A block comment is whitespace to Lua: "a--[[x]]b" must not become "ab".
                    sb.Append(newlines == 0 ? " " : new string('\n', newlines));
                    i = end;
                }
                else
                {
                    while (i < source.Length && source[i] != '\n')
                    {
                        i++;
                    }
                }
                continue;
            }
            if (c is '"' or '\'')
            {
                var start = i++;
                while (i < source.Length && source[i] != c && source[i] != '\n')
                {
                    i += source[i] == '\\' ? 2 : 1;   // an escape, "\"" and "\<newline>" included
                }
                i = Math.Min(i + 1, source.Length);
                AppendString(sb, source, start, i, verbatim);
                continue;
            }
            if (c == '[' && LongBracket(source, i) is { } stringLevel)
            {
                var end = LongBracketEnd(source, i + stringLevel + 2, stringLevel);
                AppendString(sb, source, i, end, verbatim);
                i = end;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return Tidy(source, sb.ToString(), verbatim);
    }

    /// <summary>XML without "&lt;!-- --&gt;" comments.</summary>
    public static string StripXml(string source) =>
        Tidy(source, XmlComment().Replace(source, m => new string('\n', m.Value.Count(ch => ch == '\n'))), new HashSet<int>());

    /// <summary>
    /// The table of contents without comment lines (a single "#"; "##" are
    /// the directives), its X-Build set to <paramref name="build"/>.
    /// </summary>
    public static string StripToc(string source, string build) => string.Join("\n", source.Split('\n')
        .Select(line => line.TrimEnd())
        .Where(line => !(line.StartsWith('#') && !line.StartsWith("##", StringComparison.Ordinal)))
        .Select(line => line.StartsWith("## X-Build:", StringComparison.Ordinal) ? "## X-Build: " + build : line));

    /// <summary>Whether a stripped file has nothing left but whitespace - for WoW, a file it cannot open.</summary>
    public static bool IsBlank(string stripped) => string.IsNullOrWhiteSpace(stripped);

    /// <summary>The level of a long bracket opening at <paramref name="at"/> - 0 for "[[", 2 for "[==[" - or null.</summary>
    private static int? LongBracket(string s, int at)
    {
        if (at >= s.Length || s[at] != '[')
        {
            return null;
        }
        var level = 0;
        while (at + 1 + level < s.Length && s[at + 1 + level] == '=')
        {
            level++;
        }
        return at + 1 + level < s.Length && s[at + 1 + level] == '[' ? level : null;
    }

    /// <summary>Where the closing bracket of that level ends; the text's end when it never closes.</summary>
    private static int LongBracketEnd(string s, int from, int level)
    {
        var close = "]" + new string('=', level) + "]";
        var at = s.IndexOf(close, from, StringComparison.Ordinal);
        return at < 0 ? s.Length : at + close.Length;
    }

    /// <summary>A string literal as it is; the lines it runs on to are its own.</summary>
    /// <remarks>Stripping keeps the lines, so the source's line is the output's.</remarks>
    private static void AppendString(StringBuilder sb, string source, int start, int end, HashSet<int> verbatim)
    {
        var line = source.AsSpan(0, start).Count('\n');
        sb.Append(source, start, end - start);
        var lines = source.AsSpan(start, end - start).Count('\n');
        for (var k = 1; k <= lines; k++)
        {
            verbatim.Add(line + k);
        }
    }

    /// <summary>
    /// <paramref name="stripped"/> is <paramref name="source"/> with its
    /// comments blanked, line for line. A line the stripping emptied goes; a
    /// line blank in the source stays, one in a row and none at the ends;
    /// trailing blanks go. The file ends in one newline - or is empty, if
    /// comments were all it held.
    /// </summary>
    private static string Tidy(string source, string stripped, IReadOnlySet<int> verbatim)
    {
        var before = source.Split('\n');
        var after = stripped.Split('\n');
        var lines = new List<string>(after.Length);
        for (var n = 0; n < after.Length; n++)
        {
            if (verbatim.Contains(n))
            {
                lines.Add(after[n]);
                continue;
            }
            var line = after[n].TrimEnd();
            var blankInSource = n >= before.Length || before[n].Trim().Length == 0;
            if (line.Length > 0)
            {
                lines.Add(line);
            }
            else if (blankInSource && lines.Count > 0 && lines[^1].Length > 0)
            {
                lines.Add("");
            }
        }
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex XmlComment();
}
