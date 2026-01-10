namespace ClaudeCodeSdk.Types;

public class ErrorContentBlock : IContentBlock
{
    public ErrorContentBlock(string message)
    {
        Message = message;
    }

    public string Type => "error";

    public string? Message { get; set; }

    public string? ErrorCode { get; set; }

    public string? Details { get; set; }

}