using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

public readonly struct MessageType : IEquatable<MessageType>
{
    public static MessageType System { get; } = new("system");

    public static MessageType Assistant { get; } = new("assistant");

    public static MessageType User { get; } = new("user");

    public static MessageType Result { get; } = new("result");


    public string Value { get; }

    [JsonConstructor]
    public MessageType(string value)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static bool operator ==(MessageType left, MessageType right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MessageType left, MessageType right)
    {
        return !(left == right);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is MessageType otherRole && Equals(otherRole);


    public bool Equals(MessageType other)
        => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);


    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Converter : JsonConverter<MessageType>
    {
        public override MessageType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new(reader.GetString()!);
        }
            

        public override void Write(Utf8JsonWriter writer, MessageType value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.Value);
        }
    }
}
