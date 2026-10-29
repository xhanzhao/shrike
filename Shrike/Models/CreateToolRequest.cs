namespace Shrike.Models;

public sealed class CreateToolRequest
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "Utility";

    public string Tooltip { get; set; } = string.Empty;

    public string Type { get; set; } = "python3";

    public string SourcePythonPath { get; set; } = string.Empty;

    /// <summary>
    /// Python source taken from the clipboard. When set, it is used
    /// instead of <see cref="SourcePythonPath"/>.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    public string Macro { get; set; } = string.Empty;

    public string SourceIconPath { get; set; } = string.Empty;
}
