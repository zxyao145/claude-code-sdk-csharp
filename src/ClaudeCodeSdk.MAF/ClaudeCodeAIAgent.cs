using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// AIAgent implementation using ClaudeCodeSdk for Claude Code interactions.
/// </summary>
public class ClaudeCodeAIAgent : AIAgent
{
    private readonly ClaudeCodeAIAgentOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// ClaudeCodeOptions.Resume will not working. Please replace with AgentThread
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ClaudeCodeAIAgent(ClaudeCodeOptions? options = null, ILogger? logger = null)
        : this(ClaudeCodeAIAgentOptions.From(options), logger)
    {

    }

    public ClaudeCodeAIAgent(ClaudeCodeAIAgentOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeAIAgentOptions();
        _logger = logger;
    }

    public override AgentThread DeserializeThread(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var sessionId = serializedThread.TryGetProperty("sessionId", out var sidProp)
            ? sidProp.GetString() : null;

        return new ClaudeCodeAgentThread(sessionId);
    }

    public override AgentThread GetNewThread()
    {
        return NewThread();
    }

    private ClaudeCodeAgentThread NewThread()
    {
        return new ClaudeCodeAgentThread();
    }


    #region RunAsync

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = (thread as ClaudeCodeAgentThread ?? NewThread())!;
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);
        await using var client = new ClaudeSdkClient(clientOptions, _logger);

        try
        {
            // Connect to Claude
            await client.ConnectAsync(cancellationToken: cancellationToken);

            // Convert messages to Claude format and send (exclude System messages)
            var combinedMessages = messagesList
                .Where(m => m.Role == ChatRole.User)
                .ToList();


            // Receive and collect all responses
            var responseMessages = new List<ChatMessage>();
            UsageDetails? usageDetails = null;
            foreach (var message in combinedMessages)
            {
                if (message.Role == ChatRole.User)
                {
                    var content = message.Text ?? string.Empty;
                    await client.QueryAsync(content,
                             sessionId: claudeThread.SessionId,
                             cancellationToken: cancellationToken);

                    await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
                    {
                        claudeThread.SetSessionIdIfNull(claudeMessage);

                        if (claudeMessage is ResultMessage resultMessage)
                        {
                            usageDetails = resultMessage.ToUsageDetails();
                        }
                        else
                        {
                            var assistantMessage = claudeMessage.ToChatMessage();
                            if (assistantMessage != null)
                            {
                                responseMessages.Add(assistantMessage);
                            }
                        }
                    }
                }
            }



            // Return complete response
            return new AgentRunResponse
            {
                Usage = usageDetails,
                Messages = responseMessages,
                ResponseId = Guid.NewGuid().ToString()
            };
        }
        finally
        {
            await client.DisconnectAsync();
            await client.DisposeAsync();
        }
    }

    #endregion


    #region RunStreamingAsync

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claudeThread = (thread as ClaudeCodeAgentThread ?? NewThread())!;
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);

        await using var client = new ClaudeSdkClient(clientOptions, _logger);

        // Connect to Claude
        await client.ConnectAsync(cancellationToken: cancellationToken);

        // Convert messages to Claude format and send (exclude System messages)
        var combinedMessages = messagesList
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        foreach (var message in messagesList)
        {
            var content = message.Text ?? string.Empty;
            await client.QueryAsync(content,
                     sessionId: claudeThread.SessionId,
                     cancellationToken: cancellationToken);

            // Receive and yield responses
            await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
            {
                claudeThread.SetSessionIdIfNull(claudeMessage);

                var update = claudeMessage.ToAgentRunResponseUpdate();
                if (update != null)
                {
                    yield return update;
                }
            }
        }
    }


    #endregion

    private ClaudeCodeOptions PrepareOptionsWithThread(ClaudeCodeAgentThread? thread, IEnumerable<ChatMessage> messages)
    {
        var options = _options.ToClaudeCodeOptions();

        // Extract system prompt from messages if present
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage != null)
        {
            var systemPrompt = systemMessage.Text;
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                options = options with { SystemPrompt = systemPrompt };
            }
        }

        if (thread == null)
        {
            return options;
        }

        // If thread has a session ID, use it as Resume parameter
        if (!string.IsNullOrEmpty(thread.SessionId))
        {
            options = options with { Resume = thread.SessionId };
        }

        return options;
    }
}