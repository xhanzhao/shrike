using System.Text.RegularExpressions;

namespace Shrike.Services;

/// <summary>
/// Handles the language directive at the top of a Python tool,
/// such as "#! python 3" or "#! python2".
///
/// Rhino 8's Script Editor reads this directive to choose between
/// CPython 3 and IronPython 2, so Shrike must keep the one the
/// script was tested with. A script without one gets "#! python 3".
/// </summary>
public static class PythonScriptHeader
{
    private const string DefaultDirective =
        "#! python 3";

    // Script Editor accepts "#! python 3", "#! python3",
    // "#!python2" and so on.
    private static readonly Regex DirectivePattern =
        new(
            "^#!\\s*python\\s*[23]\\b",
            RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns the source unchanged when it already names a
    /// Python version, otherwise with "#! python 3" prepended.
    /// </summary>
    public static string Prepare(
        string source)
    {
        if (FindDirective(source, skipLines: 0) is not null)
            return source;

        return
            DefaultDirective +
            Environment.NewLine +
            Environment.NewLine +
            source;
    }

    /// <summary>
    /// Earlier Shrike versions only recognised "#! python 3" and
    /// "#! python 2", so a script starting with "#! python2" got
    /// "#! python 3" put in front of it and ran on the wrong engine.
    /// Removes that added line when the script names its own version
    /// further down. Returns true when the source was changed.
    /// </summary>
    public static bool TryRemoveInjectedDirective(
        string source,
        out string repaired)
    {
        repaired = source;

        var lines =
            SplitLines(source);

        if (lines.Length < 2
            ||
            !string.Equals(
                lines[0].TrimStart('﻿').Trim(),
                DefaultDirective,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (FindDirective(source, skipLines: 1) is null)
            return false;

        var skip = 1;

        if (lines.Length > 1
            &&
            lines[1].Trim().Length == 0)
        {
            skip = 2;
        }

        repaired =
            string.Join(
                "\n",
                lines.Skip(skip));

        // Keep the file's own line endings.
        if (source.Contains("\r\n"))
        {
            repaired =
                repaired.Replace("\n", "\r\n");
        }

        return true;
    }

    // Looks through the leading comment block only, as Script
    // Editor does: the directive belongs to the file header.
    private static string? FindDirective(
        string source,
        int skipLines)
    {
        foreach (var raw in SplitLines(source).Skip(skipLines))
        {
            var line =
                raw.TrimStart('﻿').Trim();

            if (line.Length == 0)
                continue;

            if (!line.StartsWith('#'))
                return null;

            if (DirectivePattern.IsMatch(line))
                return line;
        }

        return null;
    }

    private static string[] SplitLines(
        string source)
    {
        return source
            .Replace("\r\n", "\n")
            .Split('\n');
    }
}
