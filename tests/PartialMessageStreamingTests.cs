using System.Text.Json;
using System.Threading.Channels;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class PartialMessageStreamingTests
{
    [Fact]
    public void BuildCommand_IncludePartialMessages_AddsFlagOnlyWhenEnabled()
    {
        // Arrange
        var disabled = new ClaudeCodeOptions();
        var enabled = new ClaudeCodeOptions { IncludePartialMessages = true };

        // Act
        var disabledCommand = CommandUtil.BuildCommand(
            disabled,
            isStreaming: true,
            prompt: string.Empty
        );
        var enabledCommand = CommandUtil.BuildCommand(
            enabled,
            isStreaming: true,
            prompt: string.Empty
        );

        // Assert
        Assert.DoesNotContain("--include-partial-messages", disabledCommand);
        Assert.Contains("--include-partial-messages", enabledCommand);
    }

    [Fact]
    public void ClaudeCodeAIAgentOptions_IncludePartialMessages_RoundTripsCoreOptions()
    {
        // Arrange
        var mafOptions = new ClaudeCodeAIAgentOptions { IncludePartialMessages = true };

        // Act
        var coreOptions = mafOptions.ToClaudeCodeOptions();
        var roundTripped = ClaudeCodeAIAgentOptions.From(coreOptions);

        // Assert
        Assert.True(coreOptions.IncludePartialMessages);
        Assert.True(roundTripped!.IncludePartialMessages);
        Assert.False(new ClaudeCodeOptions().IncludePartialMessages);
    }

    [Fact]
    public void ParseMessage_StreamEvent_PreservesRawUnknownEvent()
    {
        // Arrange
        const string json = """
            {
              "type": "stream_event",
              "uuid": "event-1",
              "session_id": "session-1",
              "parent_tool_use_id": "parent-1",
              "event": {
                "type": "future_event",
                "future_value": { "enabled": true }
              }
            }
            """;

        // Act
        var message = Assert.IsType<StreamEvent>(MessageParser.ParseMessage(json));

        // Assert
        Assert.Equal(MessageType.StreamEvent, message.Type);
        Assert.Equal("event-1", message.Id);
        Assert.Equal("session-1", message.SessionId);
        Assert.Equal("parent-1", message.ParentToolUseId);
        Assert.Equal("future_event", message.Event.GetProperty("type").GetString());
        Assert.True(message.Event.GetProperty("future_value").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Map_TextThinkingAndToolEvents_ProducesComposableAgentResponseUpdates()
    {
        // Arrange
        var mapper = new ClaudePartialMessageMapper();
        var messages = new IMessage[]
        {
            ParseStreamEvent(MessageStart("event-start", "message-1", "claude-sonnet")),
            ParseStreamEvent(ContentBlockStart("event-thinking-start", 0, "thinking")),
            ParseStreamEvent(
                ContentBlockDelta("event-thinking", 0, "thinking_delta", "thinking", "Plan ")
            ),
            ParseStreamEvent(
                ContentBlockDelta("event-thinking-2", 0, "thinking_delta", "thinking", "carefully")
            ),
            ParseStreamEvent(ContentBlockStop("event-thinking-stop", 0)),
            ParseStreamEvent(ContentBlockStart("event-text-start", 1, "text")),
            ParseStreamEvent(ContentBlockDelta("event-text", 1, "text_delta", "text", "Hello")),
            ParseStreamEvent(ContentBlockDelta("event-text-2", 1, "text_delta", "text", " world")),
            ParseStreamEvent(ToolBlockStart("event-tool-start", 2, "call-1", "Bash")),
            ParseStreamEvent(
                ContentBlockDelta(
                    "event-tool-input",
                    2,
                    "input_json_delta",
                    "partial_json",
                    "{\"command\":\"pwd\"}"
                )
            ),
            ParseStreamEvent(ContentBlockStop("event-tool-stop", 2)),
            ParseStreamEvent(MessageDelta("event-message-delta", "tool_use")),
            ParseStreamEvent(MessageStop("event-message-stop")),
            ParseAssistantMessage(),
        };

        // Act
        var updates = messages.SelectMany(mapper.Map).ToList();
        var response = updates.ToAgentResponse();

        // Assert
        Assert.Equal(6, updates.Count);
        Assert.Single(updates.Select(update => update.ResponseId).Distinct());
        Assert.All(
            updates,
            update =>
            {
                Assert.Equal("message-1", update.MessageId);
                Assert.Equal(ChatRole.Assistant, update.Role);
                Assert.Equal("claude-sonnet", update.AuthorName);
            }
        );
        Assert.Equal(
            "Plan carefully",
            string.Concat(
                updates
                    .SelectMany(update => update.Contents)
                    .OfType<TextReasoningContent>()
                    .Select(content => content.Text)
            )
        );
        Assert.Equal(
            "Hello world",
            string.Concat(
                updates
                    .SelectMany(update => update.Contents)
                    .OfType<TextContent>()
                    .Select(content => content.Text)
            )
        );
        var functionCall = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>()
        );
        Assert.Equal("call-1", functionCall.CallId);
        Assert.Equal("Bash", functionCall.Name);
        Assert.Equal("pwd", functionCall.Arguments!["command"]?.ToString());
        Assert.Equal(ChatFinishReason.ToolCalls, updates[^1].FinishReason);
        Assert.Single(response.Messages);
        Assert.Equal("Hello world", response.Text);
    }

    [Fact]
    public void Map_CompleteAssistantWithoutPartialEvents_FallsBackToFullMessage()
    {
        // Arrange
        var mapper = new ClaudePartialMessageMapper();
        var message = ParseAssistantMessage();

        // Act
        var update = Assert.Single(mapper.Map(message));

        // Assert
        Assert.Equal("message-1", update.MessageId);
        Assert.Equal("Hello world", update.Text);
        Assert.Single(update.Contents.OfType<TextReasoningContent>());
        Assert.Single(update.Contents.OfType<FunctionCallContent>());
    }

    [Fact]
    public void Map_InterleavedParentStreams_KeepIndependentMessageIdentity()
    {
        // Arrange
        var mapper = new ClaudePartialMessageMapper();
        var parentStart = ParseStreamEvent(
            MessageStart("parent-start", "parent-message", "parent-model")
        );
        var childStart = ParseStreamEvent(
            MessageStart(
                "child-start",
                "child-message",
                "child-model",
                parentToolUseId: "agent-call"
            )
        );
        var parentDelta = ParseStreamEvent(
            ContentBlockDelta("parent-delta", 0, "text_delta", "text", "parent")
        );
        var childDelta = ParseStreamEvent(
            ContentBlockDelta(
                "child-delta",
                0,
                "text_delta",
                "text",
                "child",
                parentToolUseId: "agent-call"
            )
        );

        // Act
        _ = mapper.Map(parentStart).ToList();
        _ = mapper.Map(childStart).ToList();
        var parentUpdate = Assert.Single(mapper.Map(parentDelta));
        var childUpdate = Assert.Single(mapper.Map(childDelta));

        // Assert
        Assert.Equal("parent-message", parentUpdate.MessageId);
        Assert.Equal("parent-model", parentUpdate.AuthorName);
        Assert.Equal("child-message", childUpdate.MessageId);
        Assert.Equal("child-model", childUpdate.AuthorName);
        Assert.Equal(parentUpdate.ResponseId, childUpdate.ResponseId);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_MultipleToolRounds_PersistsEachCompletedAssistantMessage()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var request = new ChatMessage(ChatRole.User, "run the tool");
        var messages = ToolRoundMessages().Concat(FinalAnswerMessages()).ToArray();

        // Act
        var updates = await CollectAsync(
            agent.ProcessStreamingMessagesAsync(
                ToAsyncEnumerable(messages),
                session,
                [request],
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(2, provider.Calls.Count);
        Assert.Same(request, Assert.Single(provider.Calls[0].RequestMessages));
        Assert.Empty(provider.Calls[1].RequestMessages);
        Assert.Single(
            provider
                .Calls[0]
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
        );
        Assert.Single(
            provider
                .Calls[1]
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
        );
        Assert.Equal("done", provider.Calls[1].ResponseMessages.Last().Text);
        Assert.Equal(
            "done",
            string.Concat(
                updates
                    .SelectMany(update => update.Contents)
                    .OfType<TextContent>()
                    .Select(content => content.Text)
            )
        );
    }

    [Fact]
    public async Task ProcessNonStreamingMessagesAsync_MultipleToolRounds_PersistsEachCompletedAssistantMessage()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messages = ToolRoundMessages().Concat(FinalAnswerMessages()).ToArray();

        // Act
        var response = await agent.ProcessNonStreamingMessagesAsync(
            ToAsyncEnumerable(messages),
            session,
            [new ChatMessage(ChatRole.User, "run the tool")],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, provider.Calls.Count);
        Assert.Single(
            provider
                .Calls[0]
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
        );
        Assert.Single(
            provider
                .Calls[1]
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
        );
        Assert.Equal("done", response.Messages.Last().Text);
    }

    [Fact]
    public async Task ProcessNonStreamingMessagesAsync_ApiRetriesBeforeFailure_PersistOnceInOrder()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var request = new ChatMessage(ChatRole.User, "request");

        // Act
        var response = await agent.ProcessNonStreamingMessagesAsync(
            ToAsyncEnumerable([
                ApiRetrySystemMessage(attempt: 1),
                ApiRetrySystemMessage(attempt: 2),
                ErrorResultMessage(),
            ]),
            session,
            [request],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(3, provider.Calls.Count);
        Assert.Same(request, Assert.Single(provider.Calls[0].RequestMessages));
        Assert.All(provider.Calls.Skip(1), call => Assert.Empty(call.RequestMessages));
        Assert.All(
            provider.Calls,
            call =>
                Assert.Contains(
                    call.ResponseMessages.SelectMany(message => message.Contents),
                    content => content is ErrorContent
                )
        );
        Assert.Equal(
            3,
            response.Messages.Count(message => message.Contents.OfType<ErrorContent>().Any())
        );
        Assert.Equal(
            ["api-retry-1", "api-retry-2", "error-result"],
            provider
                .Calls.SelectMany(call => call.ResponseMessages)
                .Select(message => message.MessageId)
        );
    }

    [Fact]
    public async Task ProcessNonStreamingMessagesAsync_WithoutHistoryProvider_ReturnsCompleteResponse()
    {
        // Arrange
        using var agent = new ClaudeCodeAIAgent();
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );

        // Act
        var response = await agent.ProcessNonStreamingMessagesAsync(
            ToAsyncEnumerable([AssistantTextMessage("wrapper", "message-1", "answer")]),
            session,
            [new ChatMessage(ChatRole.User, "request")],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("answer", Assert.Single(response.Messages).Text);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_AssistantBeforeStopWithoutApiId_PersistsCompletedRound()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messages = ToolRoundMessagesAssistantBeforeStop()
            .Concat(FinalAnswerMessages())
            .ToArray();

        // Act
        _ = await CollectAsync(
            agent.ProcessStreamingMessagesAsync(
                ToAsyncEnumerable(messages),
                session,
                [new ChatMessage(ChatRole.User, "run the tool")],
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(2, provider.Calls.Count);
        Assert.Single(
            provider
                .Calls[0]
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
        );
        Assert.Equal("done", provider.Calls[1].ResponseMessages.Last().Text);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_CompletedRound_PersistsBeforeStreamEnds()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messageStream = new PausableMessageStream();
        await using var enumerator = agent
            .ProcessStreamingMessagesAsync(
                messageStream.ReadAllAsync(TestContext.Current.CancellationToken),
                session,
                [new ChatMessage(ChatRole.User, "run the tool")],
                TestContext.Current.CancellationToken
            )
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        foreach (var message in ToolRoundMessages())
        {
            messageStream.Emit(message);
        }

        // Act
        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());
        var pendingUpdate = enumerator.MoveNextAsync().AsTask();
        await provider.WaitForCallCountAsync(1, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(pendingUpdate.IsCompleted);
        Assert.Single(provider.Calls);

        messageStream.Complete();
        Assert.False(await pendingUpdate);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_ApiRetry_PersistsBeforeYield()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var request = new ChatMessage(ChatRole.User, "request");
        var messageStream = new PausableMessageStream();
        await using var enumerator = agent
            .ProcessStreamingMessagesAsync(
                messageStream.ReadAllAsync(TestContext.Current.CancellationToken),
                session,
                [request],
                TestContext.Current.CancellationToken
            )
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        messageStream.Emit(ApiRetrySystemMessage());

        // Act
        Assert.True(await enumerator.MoveNextAsync());

        // Assert
        var call = Assert.Single(provider.Calls);
        Assert.Same(request, Assert.Single(call.RequestMessages));
        var retryMessage = Assert.Single(call.ResponseMessages);
        Assert.Equal(ChatRole.System, retryMessage.Role);
        Assert.Equal("api_retry", retryMessage.AdditionalProperties!["subtype"]?.ToString());
        Assert.Contains(
            "Claude Code API retry 1/10",
            Assert.IsType<ErrorContent>(Assert.Single(retryMessage.Contents)).Message
        );

        messageStream.Complete();
        Assert.False(await enumerator.MoveNextAsync());
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_InterruptedSecondRound_PersistsOnlyCompletedFirstRound()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messages = ToolRoundMessages()
            .Concat([
                ToolResultMessage(),
                ParseStreamEvent(MessageStart("second-start", "message-2", "claude-sonnet")),
                ParseStreamEvent(
                    ContentBlockDelta("second-partial", 0, "text_delta", "text", "partial")
                ),
            ])
            .ToArray();
        var updates = new List<AgentResponseUpdate>();

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (
                var update in agent.ProcessStreamingMessagesAsync(
                    ToInterruptedAsyncEnumerable(messages),
                    session,
                    [new ChatMessage(ChatRole.User, "run the tool")],
                    TestContext.Current.CancellationToken
                )
            )
            {
                updates.Add(update);
            }
        });

        // Assert
        var call = Assert.Single(provider.Calls);
        Assert.Single(
            call.ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
        );
        Assert.DoesNotContain(
            call.ResponseMessages.SelectMany(message => message.Contents).OfType<TextContent>(),
            content => content.Text.Contains("partial", StringComparison.Ordinal)
        );
        Assert.Contains(
            updates.SelectMany(update => update.Contents).OfType<TextContent>(),
            content => content.Text == "partial"
        );
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_ErrorResultAfterTruncatedAssistant_PersistsContextAndDropsIncompleteAssistant()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messages = ToolRoundMessages()
            .Concat([
                ToolResultMessage(),
                ParseStreamEvent(MessageStart("second-start", "message-2", "claude-sonnet")),
                ParseStreamEvent(
                    ContentBlockDelta("second-partial", 0, "text_delta", "text", "partial")
                ),
                AssistantTextMessage("truncated-wrapper", "message-2", "partial"),
                new UserMessage { Id = "interrupt", Content = "[Request interrupted by user]" },
                ErrorResultMessage(),
            ])
            .ToArray();

        // Act
        var updates = await CollectAsync(
            agent.ProcessStreamingMessagesAsync(
                ToAsyncEnumerable(messages),
                session,
                [new ChatMessage(ChatRole.User, "run the tool")],
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(2, provider.Calls.Count);
        var completedCall = provider.Calls[0];
        var failedCall = provider.Calls[1];
        Assert.Single(
            completedCall
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
        );
        Assert.Empty(failedCall.RequestMessages);
        Assert.Single(
            failedCall
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
        );
        Assert.Contains(
            failedCall
                .ResponseMessages.SelectMany(message => message.Contents)
                .OfType<ErrorContent>(),
            content => content.AdditionalProperties?["isFatalError"] is true
        );
        Assert.DoesNotContain(
            provider
                .Calls.SelectMany(call => call.ResponseMessages)
                .SelectMany(message => message.Contents)
                .OfType<TextContent>(),
            content => content.Text.Contains("partial", StringComparison.Ordinal)
        );
        Assert.Contains(
            updates.SelectMany(update => update.Contents),
            content => content is ErrorContent
        );
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_InterleavedStreams_PersistsOnlyCompletedMessage()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );
        var messages = new IMessage[]
        {
            ParseStreamEvent(MessageStart("parent-start", "parent-message", "parent-model")),
            ParseStreamEvent(ContentBlockDelta("parent-text", 0, "text_delta", "text", "parent")),
            ParseStreamEvent(
                MessageStart("child-start", "child-message", "child-model", "agent-call")
            ),
            ParseStreamEvent(
                ContentBlockDelta("child-text", 0, "text_delta", "text", "child", "agent-call")
            ),
            ParseStreamEvent(MessageStop("child-stop", "agent-call")),
            AssistantTextMessage("child-wrapper", "child-message", "child", "agent-call"),
            ParseStreamEvent(MessageStop("parent-stop")),
            AssistantTextMessage("parent-wrapper", "parent-message", "parent"),
        };

        // Act
        _ = await CollectAsync(
            agent.ProcessStreamingMessagesAsync(
                ToAsyncEnumerable(messages),
                session,
                [new ChatMessage(ChatRole.User, "delegate")],
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal("child", Assert.Single(provider.Calls[0].ResponseMessages).Text);
        Assert.Equal("parent", Assert.Single(provider.Calls[1].ResponseMessages).Text);
    }

    [Fact]
    public async Task ProcessStreamingMessagesAsync_WithoutPartialEvents_PersistsAtRunCompletion()
    {
        // Arrange
        var provider = new RecordingChatHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions { ChatHistoryProvider = provider }
        );
        var session = Assert.IsType<ClaudeCodeAgentSession>(
            await agent.CreateSessionAsync(TestContext.Current.CancellationToken)
        );

        // Act
        var updates = await CollectAsync(
            agent.ProcessStreamingMessagesAsync(
                ToAsyncEnumerable([AssistantTextMessage("wrapper", "message-1", "fallback")]),
                session,
                [new ChatMessage(ChatRole.User, "request")],
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("fallback", Assert.Single(updates).Text);
        Assert.Equal(
            "fallback",
            Assert.Single(Assert.Single(provider.Calls).ResponseMessages).Text
        );
    }

    private static StreamEvent ParseStreamEvent(string json) =>
        Assert.IsType<StreamEvent>(MessageParser.ParseMessage(json));

    private static AssistantMessage ParseAssistantMessage() =>
        Assert.IsType<AssistantMessage>(
            MessageParser.ParseMessage(
                """
                {
                  "type": "assistant",
                  "uuid": "assistant-wrapper-1",
                  "session_id": "session-1",
                  "parent_tool_use_id": null,
                  "message": {
                    "id": "message-1",
                    "model": "claude-sonnet",
                    "content": [
                      { "type": "thinking", "thinking": "Plan carefully", "signature": "sig" },
                      { "type": "text", "text": "Hello world" },
                      {
                        "type": "tool_use",
                        "id": "call-1",
                        "name": "Bash",
                        "input": { "command": "pwd" }
                      }
                    ]
                  }
                }
                """
            )
        );

    private static IReadOnlyList<IMessage> ToolRoundMessages() =>
        [
            ParseStreamEvent(MessageStart("tool-start", "message-1", "claude-sonnet")),
            ParseStreamEvent(ToolBlockStart("tool-block-start", 0, "call-1", "Bash")),
            ParseStreamEvent(
                ContentBlockDelta(
                    "tool-input",
                    0,
                    "input_json_delta",
                    "partial_json",
                    "{\"command\":\"pwd\"}"
                )
            ),
            ParseStreamEvent(ContentBlockStop("tool-block-stop", 0)),
            ParseStreamEvent(MessageDelta("tool-message-delta", "tool_use")),
            ParseStreamEvent(MessageStop("tool-message-stop")),
            AssistantToolMessage("tool-wrapper", "message-1", "call-1", "Bash"),
        ];

    private static IReadOnlyList<IMessage> ToolRoundMessagesAssistantBeforeStop() =>
        [
            ParseStreamEvent(MessageStart("tool-start", "message-1", "claude-sonnet")),
            ParseStreamEvent(ToolBlockStart("tool-block-start", 0, "call-1", "Bash")),
            ParseStreamEvent(
                ContentBlockDelta(
                    "tool-input",
                    0,
                    "input_json_delta",
                    "partial_json",
                    "{\"command\":\"pwd\"}"
                )
            ),
            ParseStreamEvent(ContentBlockStop("tool-block-stop", 0)),
            ParseStreamEvent(MessageDelta("tool-message-delta", "tool_use")),
            AssistantToolMessage("tool-wrapper", null, "call-1", "Bash"),
            ParseStreamEvent(MessageStop("tool-message-stop")),
        ];

    private static IReadOnlyList<IMessage> FinalAnswerMessages() =>
        [
            ToolResultMessage(),
            ParseStreamEvent(MessageStart("answer-start", "message-2", "claude-sonnet")),
            ParseStreamEvent(ContentBlockDelta("answer-text", 0, "text_delta", "text", "done")),
            ParseStreamEvent(MessageDelta("answer-message-delta", "end_turn")),
            ParseStreamEvent(MessageStop("answer-message-stop")),
            AssistantTextMessage("answer-wrapper", "message-2", "done"),
        ];

    private static AssistantMessage AssistantToolMessage(
        string wrapperId,
        string? messageId,
        string callId,
        string toolName
    ) =>
        new()
        {
            Id = wrapperId,
            ApiMessageId = messageId,
            Model = "claude-sonnet",
            SessionId = "session-1",
            Content =
            [
                new ToolUseBlock
                {
                    Id = callId,
                    Name = toolName,
                    Input = new Dictionary<string, object> { ["command"] = "pwd" },
                },
            ],
        };

    private static AssistantMessage AssistantTextMessage(
        string wrapperId,
        string messageId,
        string text,
        string? parentToolUseId = null
    ) =>
        new()
        {
            Id = wrapperId,
            ApiMessageId = messageId,
            Model = "claude-sonnet",
            SessionId = "session-1",
            ParentToolUseId = parentToolUseId,
            Content = [new TextBlock { Text = text }],
        };

    private static UserMessage ToolResultMessage() =>
        new()
        {
            Id = "tool-result-1",
            Content = new List<IContentBlock>
            {
                new ToolResultBlock { ToolUseId = "call-1", Content = "pwd output" },
            },
        };

    private static ResultMessage ErrorResultMessage() =>
        new()
        {
            Id = "error-result",
            Subtype = "error_during_execution",
            DurationMs = 1,
            DurationApiMs = 1,
            IsError = true,
            NumTurns = 2,
            SessionId = "session-1",
            Result = "Request interrupted by user",
        };

    private static SystemMessage ApiRetrySystemMessage(int attempt = 1) =>
        new()
        {
            Id = $"api-retry-{attempt}",
            Subtype = "api_retry",
            SessionId = "session-1",
            Data = new Dictionary<string, object>
            {
                ["attempt"] = attempt,
                ["max_retries"] = 10,
                ["error"] = "rate_limit",
            },
        };

    private static string MessageStart(
        string eventId,
        string messageId,
        string model,
        string? parentToolUseId = null
    ) =>
        Envelope(
            eventId,
            new { type = "message_start", message = new { id = messageId, model } },
            parentToolUseId
        );

    private static string ContentBlockStart(string eventId, int index, string blockType) =>
        Envelope(
            eventId,
            new
            {
                type = "content_block_start",
                index,
                content_block = new Dictionary<string, object?>
                {
                    ["type"] = blockType,
                    [blockType] = string.Empty,
                },
            }
        );

    private static string ToolBlockStart(
        string eventId,
        int index,
        string callId,
        string toolName
    ) =>
        Envelope(
            eventId,
            new
            {
                type = "content_block_start",
                index,
                content_block = new
                {
                    type = "tool_use",
                    id = callId,
                    name = toolName,
                    input = new { },
                },
            }
        );

    private static string ContentBlockDelta(
        string eventId,
        int index,
        string deltaType,
        string valueProperty,
        string value,
        string? parentToolUseId = null
    ) =>
        Envelope(
            eventId,
            new
            {
                type = "content_block_delta",
                index,
                delta = new Dictionary<string, object?>
                {
                    ["type"] = deltaType,
                    [valueProperty] = value,
                },
            },
            parentToolUseId
        );

    private static string ContentBlockStop(string eventId, int index) =>
        Envelope(eventId, new { type = "content_block_stop", index });

    private static string MessageDelta(string eventId, string stopReason) =>
        Envelope(eventId, new { type = "message_delta", delta = new { stop_reason = stopReason } });

    private static string MessageStop(string eventId, string? parentToolUseId = null) =>
        Envelope(eventId, new { type = "message_stop" }, parentToolUseId);

    private static string Envelope(
        string eventId,
        object eventValue,
        string? parentToolUseId = null
    ) =>
        JsonSerializer.Serialize(
            new
            {
                type = "stream_event",
                uuid = eventId,
                session_id = "session-1",
                parent_tool_use_id = parentToolUseId,
                @event = eventValue,
            }
        );

    private static async IAsyncEnumerable<IMessage> ToAsyncEnumerable(
        IEnumerable<IMessage> messages
    )
    {
        foreach (var message in messages)
        {
            await Task.Yield();
            yield return message;
        }
    }

    private static async IAsyncEnumerable<IMessage> ToInterruptedAsyncEnumerable(
        IEnumerable<IMessage> messages
    )
    {
        foreach (var message in messages)
        {
            await Task.Yield();
            yield return message;
        }

        throw new OperationCanceledException("simulated interruption");
    }

    private static async Task<List<AgentResponseUpdate>> CollectAsync(
        IAsyncEnumerable<AgentResponseUpdate> updates
    )
    {
        var result = new List<AgentResponseUpdate>();
        await foreach (var update in updates)
        {
            result.Add(update);
        }

        return result;
    }

    private sealed class RecordingChatHistoryProvider : ChatHistoryProvider
    {
        private readonly SemaphoreSlim _callsChanged = new(0);

        public List<HistoryCall> Calls { get; } = [];

        public async Task WaitForCallCountAsync(int count, CancellationToken cancellationToken)
        {
            while (Calls.Count < count)
            {
                await _callsChanged.WaitAsync(cancellationToken);
            }
        }

        protected override ValueTask StoreChatHistoryAsync(
            InvokedContext context,
            CancellationToken cancellationToken = default
        )
        {
            Calls.Add(
                new HistoryCall(
                    context.RequestMessages.ToList(),
                    context.ResponseMessages?.ToList() ?? []
                )
            );
            _callsChanged.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PausableMessageStream
    {
        private readonly Channel<IMessage> _messages = Channel.CreateUnbounded<IMessage>();

        public void Emit(IMessage message) => Assert.True(_messages.Writer.TryWrite(message));

        public void Complete() => Assert.True(_messages.Writer.TryComplete());

        public IAsyncEnumerable<IMessage> ReadAllAsync(CancellationToken cancellationToken) =>
            _messages.Reader.ReadAllAsync(cancellationToken);
    }

    private sealed record HistoryCall(
        IReadOnlyList<ChatMessage> RequestMessages,
        IReadOnlyList<ChatMessage> ResponseMessages
    );
}
