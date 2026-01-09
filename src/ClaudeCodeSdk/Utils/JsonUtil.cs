using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ClaudeCodeSdk.Utils;


internal static class JsonUtil
{
    internal static readonly JsonSerializerOptions CAMELCASE_OPTIONS;

    internal static readonly JsonSerializerOptions SNAKECASELOWER_OPTIONS;

    static JsonUtil()
    {
        CAMELCASE_OPTIONS = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        SNAKECASELOWER_OPTIONS = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, CAMELCASE_OPTIONS);
        return json;
    }

    public static T? Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, CAMELCASE_OPTIONS);
    }

    public static T? SerializeToElement<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, CAMELCASE_OPTIONS);
    }


    public static JsonElement SnakeCaseSerializeToElement<TValue>(TValue value)
    {
        return JsonSerializer.SerializeToElement(value, SNAKECASELOWER_OPTIONS);
    }

    public static TValue SnakeCaseDeserialize<TValue>(string text)
    {
        return JsonSerializer.Deserialize<TValue>(text, SNAKECASELOWER_OPTIONS)!;
    }
}