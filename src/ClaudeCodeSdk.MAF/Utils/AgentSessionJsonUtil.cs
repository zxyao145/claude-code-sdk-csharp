using Microsoft.Agents.AI;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.MAF.Utils;


internal static partial class AgentSessionJsonUtil
{
    internal static readonly JsonSerializerOptions ClaudeCodeAgentSession_OPTIONS;

    static AgentSessionJsonUtil()
    {
        JsonSerializerOptions options = new(JsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // same as in AgentAbstractionsJsonUtilities and AIJsonUtilities
        };

        // Chain in the resolvers from both AgentAbstractionsJsonUtilities and our source generated context.
        // We want AgentAbstractionsJsonUtilities first to ensure any M.E.AI types are handled via its resolver.
        options.TypeInfoResolverChain.Clear();
        options.TypeInfoResolverChain.Add(AgentAbstractionsJsonUtilities.DefaultOptions.TypeInfoResolver!);
        options.TypeInfoResolverChain.Add(JsonContext.Default.Options.TypeInfoResolver!);

        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        ClaudeCodeAgentSession_OPTIONS = options;

    }
    // Keep in sync with CreateDefaultOptions above.
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web,
        UseStringEnumConverter = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)]

    // Agent abstraction types
    [JsonSerializable(typeof(ClaudeCodeAgentSession))]
    [ExcludeFromCodeCoverage]
    internal partial class JsonContext : JsonSerializerContext;
}
