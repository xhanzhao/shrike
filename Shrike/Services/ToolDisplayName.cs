using System.Text.RegularExpressions;

namespace Shrike.Services;

/// <summary>
/// Turns a code-style tool name into readable words for display:
/// "MoveBlockObjectsToLayer" becomes "Move Block Objects To Layer",
/// "TagAndExportSeatRoomSVG" becomes "Tag And Export Seat Room SVG".
///
/// Names that already contain spaces are shown as typed.
/// Only the display changes; tool.json keeps the original name.
/// </summary>
public static class ToolDisplayName
{
    // lower→Upper ("moveBlock"), or an acronym followed by a word
    // ("SVGFile" → "SVG File", "Offset3DCurve" → "Offset3D Curve").
    private static readonly Regex WordBoundary =
        new(
            "(?<=[a-z])(?=[A-Z])|(?<=[A-Z0-9])(?=[A-Z][a-z])",
            RegexOptions.Compiled);

    public static string For(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var trimmed =
            name.Trim();

        if (trimmed.Contains(' '))
            return trimmed;

        var spaced =
            WordBoundary.Replace(
                trimmed.Replace('_', ' '),
                " ");

        return Regex.Replace(
            spaced,
            @"\s+",
            " ");
    }
}
