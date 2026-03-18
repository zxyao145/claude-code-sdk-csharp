using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

internal sealed class UserMessageContentJsonConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.String => root.GetString() ?? string.Empty,
            JsonValueKind.Array => root.Deserialize<List<IContentBlock>>(options) ?? [],
            _ => throw new JsonException($"Unsupported user message content kind: {root.ValueKind}")
        };
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                return;
            case IReadOnlyList<IContentBlock> contentBlocks:
                JsonSerializer.Serialize(writer, contentBlocks, options);
                return;
            case IEnumerable<IContentBlock> contentBlocks:
                JsonSerializer.Serialize(writer, contentBlocks.ToList(), options);
                return;
            default:
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
                return;
        }
    }
}
