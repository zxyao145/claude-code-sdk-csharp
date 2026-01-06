using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

public static class JsonUtil
{
    static JsonSerializerOptions OPTIONS;
    static JsonUtil()
    {
        OPTIONS = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    public static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, OPTIONS);
        return json;
    }

    public static T? Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, OPTIONS);
    }

}
