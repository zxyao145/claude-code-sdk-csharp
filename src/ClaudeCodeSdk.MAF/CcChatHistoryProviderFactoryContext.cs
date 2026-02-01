using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

public class CcChatHistoryProviderFactoryContext
{
    /// <summary>
    /// Gets or sets the serialized state of the <see cref="ChatHistoryProvider"/>, if any.
    /// </summary>
    /// <value><see langword="default"/> if there is no state, e.g. when the <see cref="ChatHistoryProvider"/> is first created.</value>
    public JsonElement SerializedState { get; set; }

    /// <summary>
    /// Gets or sets the JSON serialization options to use when deserializing the <see cref="SerializedState"/>.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    public Guid SessionId { get; init;  }
}

/// <summary>
/// Context object passed to the <see cref="AIContextProviderFactory"/> to create a new instance of <see cref="AIContextProvider"/>.
/// </summary>
public sealed class CcAIContextProviderFactoryContext
{
    /// <summary>
    /// Gets or sets the serialized state of the <see cref="AIContextProvider"/>, if any.
    /// </summary>
    /// <value><see langword="default"/> if there is no state, e.g. when the <see cref="AIContextProvider"/> is first created.</value>
    public JsonElement SerializedState { get; set; }

    /// <summary>
    /// Gets or sets the JSON serialization options to use when deserializing the <see cref="SerializedState"/>.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    public Guid SessionId { get; init; }
}


