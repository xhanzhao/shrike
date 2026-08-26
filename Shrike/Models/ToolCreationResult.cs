namespace Shrike.Models;

public sealed class ToolCreationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ToolDirectory { get; init; } = string.Empty;

    public static ToolCreationResult Ok(string message, string toolDirectory)
    {
        return new ToolCreationResult
        {
            Success = true,
            Message = message,
            ToolDirectory = toolDirectory
        };
    }

    public static ToolCreationResult Fail(string message)
    {
        return new ToolCreationResult
        {
            Success = false,
            Message = message
        };
    }
}
