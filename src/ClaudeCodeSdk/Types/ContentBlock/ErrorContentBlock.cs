using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

public record ErrorContentBlock : ContentBlockBase, IContentBlock
{
    public ErrorContentBlock(string message)
    {
        Message = message;
    }

    [JsonConstructor]
    public ErrorContentBlock(string? message, string? errorCode, string? details)
    {
        Message = message;
        ErrorCode = errorCode;
        Details = details;
    }

    public string? Message { get; set; }

    public string? ErrorCode { get; set; }

    public string? Details { get; set; }

    [JsonIgnore]
    public string Type => "error";

}
