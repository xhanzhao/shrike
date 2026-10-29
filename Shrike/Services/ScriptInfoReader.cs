using System.Globalization;
using System.Text.RegularExpressions;

namespace Shrike.Services;

/// <summary>
/// Reads suggestions for a new tool from a Python script:
/// a display name from the file name, and a tooltip from the
/// script's docstring or first comment line.
/// </summary>
public static class ScriptInfoReader
{
    private static readonly Regex DocstringPattern =
        new(
            "^\\s*[rRuU]?(\"\"\"|''')(?<body>.*?)\\1",
            RegexOptions.Singleline);

    // Header comments that Rhino 8's Script Editor uses,
    // which are not descriptions: "#! python3", "# r: numpy",
    // "# venv: ...", "# env: ...", "# -*- coding: utf-8 -*-".
    private static readonly Regex DirectiveCommentPattern =
        new(
            "^#\\s*(!|-\\*-|(r|venv|env|requirements|flag)\\s*:)",
            RegexOptions.IgnoreCase);

    /// <summary>
    /// "select_by_layer.py" becomes "Select By Layer".
    /// </summary>
    public static string SuggestName(
        string scriptPath)
    {
        var baseName =
            Path.GetFileNameWithoutExtension(
                scriptPath)
            ?? string.Empty;

        var words =
            baseName
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            words.Select(
                word =>
                    char.ToUpper(
                        word[0],
                        CultureInfo.InvariantCulture)
                    + word[1..]));
    }

    /// <summary>
    /// Returns the first line of the module docstring, or else the
    /// first plain comment at the top of the script.
    /// Returns an empty string when the script has neither.
    /// </summary>
    public static string SuggestTooltip(
        string scriptPath)
    {
        string source;

        try
        {
            source =
                File.ReadAllText(
                    scriptPath);
        }
        catch
        {
            return string.Empty;
        }

        var lines =
            source
                .Replace("\r\n", "\n")
                .Split('\n');

        var index = 0;

        string? firstComment = null;

        // Skip the header comments and blank lines
        // that come before the code.
        while (index < lines.Length)
        {
            var line =
                lines[index].Trim();

            if (line.Length == 0)
            {
                index++;
                continue;
            }

            if (!line.StartsWith('#'))
                break;

            if (firstComment is null
                && !DirectiveCommentPattern.IsMatch(line))
            {
                var text =
                    line.TrimStart('#').Trim();

                if (text.Length > 0)
                    firstComment = text;
            }

            index++;
        }

        var rest =
            string.Join(
                "\n",
                lines.Skip(index));

        var docstring =
            DocstringPattern.Match(
                rest);

        if (docstring.Success)
        {
            var firstLine =
                docstring.Groups["body"].Value
                    .Split('\n')
                    .Select(
                        line =>
                            line.Trim())
                    .FirstOrDefault(
                        line =>
                            line.Length > 0);

            if (!string.IsNullOrEmpty(firstLine))
                return firstLine;
        }

        return firstComment ?? string.Empty;
    }
}
