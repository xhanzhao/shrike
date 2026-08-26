namespace Shrike.Models;

public sealed class ToolPackageInfo
{
    public string Name
    {
        get;
        init;
    } = string.Empty;

    public string Category
    {
        get;
        init;
    } = string.Empty;

    public string Type
    {
        get;
        init;
    } = string.Empty;

    public string Entry
    {
        get;
        init;
    } = string.Empty;
}
