using System.Text.RegularExpressions;

namespace Shrike.Services;

/// <summary>
/// Turns text copied from an AI chat into a Python script.
///
/// AI replies usually wrap code in Markdown fences and surround it
/// with explanations, so only the code block is kept. This class does
/// not depend on Rhino or Eto.
/// </summary>
public static class ClipboardCodeParser
{
    private static readonly Regex FenceRegex =
        new(
            @"^[ \t]*(`{3,}|~{3,})[ \t]*([\w+#.-]*)[^\n]*\n(.*?)^[ \t]*\1[ \t]*$",
            RegexOptions.Multiline | RegexOptions.Singleline);

    private static readonly Regex HeaderRegex =
        new(
            @"^#\s*shrike-(name|category|tooltip)\s*:\s*(.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex DocstringRegex =
        new(
            "^\\s*(?:#[^\\n]*\\n\\s*)*(?:\"\"\"|''')\\s*(.*?)(?:\"\"\"|''')",
            RegexOptions.Singleline);

    public static ClipboardCodeResult Parse(
        string? text)
    {
        var result =
            new ClipboardCodeResult();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.Warnings.Add(
                "The clipboard is empty.");

            return result;
        }

        var normalized =
            text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

        var blocks =
            FenceRegex
                .Matches(normalized)
                .Select(
                    match =>
                        (
                            Language:
                                match.Groups[2].Value.Trim(),
                            Code:
                                match.Groups[3].Value
                        ))
                .Where(
                    block =>
                        !string.IsNullOrWhiteSpace(
                            block.Code))
                .ToList();

        result.CodeBlockCount =
            blocks.Count;

        string code;

        if (blocks.Count == 0)
        {
            code = normalized;
        }
        else
        {
            // Prefer blocks labelled as Python, then the longest one.
            code =
                blocks
                    .OrderByDescending(
                        block =>
                            IsPythonLanguage(
                                block.Language))
                    .ThenByDescending(
                        block =>
                            block.Code.Length)
                    .First()
                    .Code;
        }

        code =
            TrimBlankLines(code);

        result.Code =
            code;

        result.LineCount =
            ToolHistoryService.CountLines(code);

        ReadMetadata(
            code,
            result);

        AddWarnings(
            code,
            result);

        return result;
    }

    private static bool IsPythonLanguage(
        string language)
    {
        return string.Equals(language, "python", StringComparison.OrdinalIgnoreCase)
               || string.Equals(language, "py", StringComparison.OrdinalIgnoreCase)
               || string.Equals(language, "python3", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimBlankLines(
        string code)
    {
        var lines =
            code
                .Split('\n')
                .Select(
                    line =>
                        line.TrimEnd())
                .ToList();

        while (lines.Count > 0
               && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0
               && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static void ReadMetadata(
        string code,
        ClipboardCodeResult result)
    {
        foreach (Match match in
                 HeaderRegex.Matches(
                     code.Replace("\r\n", "\n")))
        {
            var value =
                match.Groups[2].Value;

            switch (match.Groups[1].Value.ToLowerInvariant())
            {
                case "name":
                    result.SuggestedName = value;
                    break;
                case "category":
                    result.SuggestedCategory = value;
                    break;
                case "tooltip":
                    result.SuggestedTooltip = value;
                    break;
            }
        }

        var docstring =
            DocstringRegex.Match(
                code.Replace("\r\n", "\n"));

        if (!docstring.Success)
            return;

        var firstLine =
            docstring.Groups[1].Value
                .Split('\n')
                .Select(
                    line =>
                        line.Trim())
                .FirstOrDefault(
                    line =>
                        line.Length > 0)
            ?? string.Empty;

        if (firstLine.Length == 0)
            return;

        if (string.IsNullOrWhiteSpace(result.SuggestedTooltip))
            result.SuggestedTooltip = firstLine;

        if (string.IsNullOrWhiteSpace(result.SuggestedName))
            result.SuggestedName = MakeShortName(firstLine);
    }

    private static string MakeShortName(
        string sentence)
    {
        var trimmed =
            sentence
                .TrimEnd('.', '\u3002', ':')
                .Trim();

        var words =
            trimmed.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        return words.Length <= 5
            ? trimmed
            : string.Join(
                ' ',
                words.Take(5));
    }

    private static void AddWarnings(
        string code,
        ClipboardCodeResult result)
    {
        if (code.Contains("ghenv", StringComparison.Ordinal)
            || code.Contains("import Grasshopper", StringComparison.Ordinal))
        {
            result.Warnings.Add(
                "This looks like a Grasshopper script. " +
                "Shrike tools run as Rhino commands, not inside Grasshopper.");
        }

        var firstLine =
            code.TrimStart()
                .Split('\n')
                .FirstOrDefault()
                ?.Trim()
            ?? string.Empty;

        if (firstLine.StartsWith("_", StringComparison.Ordinal)
            || firstLine.StartsWith("!", StringComparison.Ordinal)
            || firstLine.StartsWith("-_", StringComparison.Ordinal))
        {
            result.LooksLikeMacro = true;

            result.Warnings.Add(
                "This looks like a Rhino macro rather than Python. " +
                "Switch Tool Type to Rhino Macro if that is intended.");
        }
    }
}

public sealed class ClipboardCodeResult
{
    public string Code { get; set; } = string.Empty;

    public int CodeBlockCount { get; set; }

    public int LineCount { get; set; }

    public string SuggestedName { get; set; } = string.Empty;

    public string SuggestedCategory { get; set; } = string.Empty;

    public string SuggestedTooltip { get; set; } = string.Empty;

    public bool LooksLikeMacro { get; set; }

    /// <summary>
    /// Set when the clipboard held a copied .py file instead of text.
    /// </summary>
    public string SourceFilePath { get; set; } = string.Empty;

    public List<string> Warnings { get; } = new();

    public bool HasCode =>
        !string.IsNullOrWhiteSpace(Code);

    public string Summary =>
        !string.IsNullOrWhiteSpace(SourceFilePath)
            ? $"{Path.GetFileName(SourceFilePath)} ({LineCount} lines)"
            : CodeBlockCount switch
            {
                0 => $"Clipboard text ({LineCount} lines)",
                1 => $"Code block from clipboard ({LineCount} lines)",
                _ => $"1 of {CodeBlockCount} code blocks from clipboard ({LineCount} lines)"
            };
}
