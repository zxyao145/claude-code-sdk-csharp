using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ClaudeCodeSdk.Utils;

internal static class JsonUtil
{
    internal static readonly JsonSerializerOptions CAMELCASE_OPTIONS;

    internal static readonly JsonSerializerOptions SNAKECASELOWER_OPTIONS;

    static JsonUtil()
    {
        var resolver = CreateResolver();

        CAMELCASE_OPTIONS = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = resolver,
        };

        SNAKECASELOWER_OPTIONS = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = resolver,
        };
    }

    // Source-generated metadata is always consulted first; reflection is chained in only
    // when the process allows it, so callers serializing an arbitrary type still work
    // outside AOT. IsReflectionEnabledByDefault is a link-time constant that ILLink folds
    // to false and dead-code-eliminates in trimmed/Native AOT publishes, but the build-time
    // analyzer doesn't model that guard yet (dotnet/runtime#107440), hence the suppression.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Guarded by JsonSerializer.IsReflectionEnabledByDefault; see comment above."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Guarded by JsonSerializer.IsReflectionEnabledByDefault; see comment above."
    )]
    private static IJsonTypeInfoResolver CreateResolver()
    {
        return JsonSerializer.IsReflectionEnabledByDefault
            ? JsonTypeInfoResolver.Combine(
                SdkJsonContext.Default,
                new DefaultJsonTypeInfoResolver()
            )
            : SdkJsonContext.Default;
    }

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, GetTypeInfo<T>(CAMELCASE_OPTIONS));
    }

    public static T? Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize(value, GetTypeInfo<T>(CAMELCASE_OPTIONS));
    }

    public static T? Deserialize<T>(JsonElement element, JsonSerializerOptions options)
    {
        return element.Deserialize(GetTypeInfo<T>(options));
    }

    public static T? SerializeToElement<T>(string value)
    {
        return JsonSerializer.Deserialize(value, GetTypeInfo<T>(CAMELCASE_OPTIONS));
    }

    /// <summary>
    /// Resolves a type's <see cref="JsonTypeInfo{T}"/> from <paramref name="options"/> without
    /// calling a reflection-based JsonSerializer overload directly.
    /// </summary>
    internal static JsonTypeInfo<T> GetTypeInfo<T>(JsonSerializerOptions options)
    {
        return (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    }
}
