using System.Text.Json;
using System.Threading.Channels;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class BackgroundAgentCompletionTests
{
    [Fact]
    public async Task ReadResponseAsync_BackgroundAgentsOutliveFirstResult_ForwardsEveryResultAndWaits()
    {
        // Arrange
        using var reader = new StringReader(
            string.Join(
                '\n',
                Started("a"),
                Started("b"),
                Result("placeholder"),
                Finished("a"),
                Result("still waiting"),
                Finished("b"),
                Result("final"),
                Result("next request")
            )
        );

        // Act
        var messages = await ReadAsync(reader, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new[] { "placeholder", "still waiting", "final" },
            messages.OfType<ResultMessage>().Select(result => result.Result)
        );
        Assert.Equal(4, messages.OfType<SystemMessage>().Count());
        Assert.Contains(
            "next request",
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReadResponseAsync_BackgroundAgentRunning_YieldsIntermediateResultImmediately()
    {
        // Arrange
        using var reader = new PendingReader();
        reader.Lines.Writer.TryWrite(Started("a"));
        reader.Lines.Writer.TryWrite(Result("placeholder"));
        var handler = new ControlProtocolHandler(null, (_, _) => Task.CompletedTask);
        await using var messages = ClaudeProcess
            .ReadResponseAsync(
                reader,
                handler,
                cancellationToken: TestContext.Current.CancellationToken
            )
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act / Assert: the intermediate result is visible before any completion notification.
        Assert.True(await messages.MoveNextAsync());
        Assert.IsType<SystemMessage>(messages.Current);
        Assert.True(
            await messages
                .MoveNextAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
        );
        var intermediate = Assert.IsType<ResultMessage>(messages.Current);
        Assert.Equal("placeholder", intermediate.Result);
        Assert.True(intermediate.IsIntermediate);

        reader.Lines.Writer.TryWrite(Finished("a"));
        reader.Lines.Writer.TryWrite(Result("final"));
        Assert.True(await messages.MoveNextAsync());
        Assert.True(await messages.MoveNextAsync());
        var final = Assert.IsType<ResultMessage>(messages.Current);
        Assert.Equal("final", final.Result);
        Assert.False(final.IsIntermediate);
        Assert.False(await messages.MoveNextAsync());
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("stopped")]
    [InlineData("killed")]
    public async Task ReadResponseAsync_TerminalPatchWithoutNotification_Completes(string status)
    {
        // Arrange
        using var reader = new StringReader(
            string.Join(
                '\n',
                Started("a"),
                Started("a"),
                Result("placeholder"),
                SystemFrame("task_updated", new { task_id = "a", patch = new { status } }),
                Result("final")
            )
        );

        // Act
        var messages = await ReadAsync(reader, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new[] { "placeholder", "final" },
            messages.OfType<ResultMessage>().Select(result => result.Result)
        );
    }

    [Theory]
    [InlineData("local_bash")]
    [InlineData("in_process_teammate")]
    [InlineData("remote_agent")]
    public async Task ReadResponseAsync_LongLivedTask_DoesNotHoldResponseOpen(string taskType)
    {
        // Arrange
        using var reader = new StringReader(
            string.Join('\n', Started("a", taskType), Result("final"), Result("next"))
        );

        // Act
        var messages = await ReadAsync(reader, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("final", Assert.Single(messages.OfType<ResultMessage>()).Result);
        Assert.Contains("next", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadResponseAsync_ErrorWithActiveAgent_ReturnsErrorWithoutWaiting()
    {
        // Arrange
        using var reader = new StringReader(
            string.Join('\n', Started("a"), Result("budget exceeded", true), Result("next"))
        );

        // Act
        var messages = await ReadAsync(reader, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Assert.Single(messages.OfType<ResultMessage>()).IsError);
        Assert.Contains("next", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadResponseAsync_EofWithUnfinishedAgent_DoesNotReportSuccess()
    {
        // Arrange
        using var reader = new StringReader(string.Join('\n', Started("a"), Result("placeholder")));

        // Act / Assert
        await Assert.ThrowsAsync<Exceptions.ProcessException>(() =>
            ReadAsync(reader, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReadResponseAsync_CancelWhileAgentRuns_PropagatesCancellation()
    {
        // Arrange
        using var reader = new PendingReader();
        reader.Lines.Writer.TryWrite(Started("a"));
        reader.Lines.Writer.TryWrite(Result("placeholder"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );

        // Act
        var response = ReadAsync(reader, cancellation.Token);
        await reader.Waiting.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        Assert.False(response.IsCompleted);
        cancellation.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
    }

    [Fact]
    public async Task ReadResponseAsync_NotificationBeforeFollowup_DoesNotCompleteUntilParentResult()
    {
        // Arrange
        using var reader = new PendingReader();
        reader.Lines.Writer.TryWrite(Started("a", "local_workflow"));
        reader.Lines.Writer.TryWrite(Result("placeholder"));
        reader.Lines.Writer.TryWrite(Finished("a"));

        // Act
        var response = ReadAsync(reader, TestContext.Current.CancellationToken);
        await reader.Waiting.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        Assert.False(response.IsCompleted);
        reader.Lines.Writer.TryWrite(Result("final"));
        var messages = await response.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { "placeholder", "final" },
            messages.OfType<ResultMessage>().Select(result => result.Result)
        );
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProcessMessages_BackgroundAgent_ForwardsIntermediateAndFinalResults(
        bool streaming,
        bool placeholderHasSchema
    )
    {
        // Arrange: processing only; the lazy SDK constructor does not start or probe a CLI.
        var history = new RecordingHistoryProvider();
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions
            {
                ChatHistoryProvider = history,
                ExtraArgs = new Dictionary<string, string?> { ["json-schema"] = "{}" },
            }
        );
        var placeholder = JsonSerializer.Deserialize<Dictionary<string, object>>(
            Result("placeholder")
        )!;
        if (placeholderHasSchema)
            placeholder["structured_output"] = new { answer = "placeholder" };
        var final = JsonSerializer.Deserialize<Dictionary<string, object>>(Result("done"))!;
        final["structured_output"] = new { answer = "final" };
        using var reader = new StringReader(
            string.Join(
                '\n',
                Started("a"),
                JsonSerializer.Serialize(placeholder),
                Finished("a"),
                JsonSerializer.Serialize(final)
            )
        );
        var handler = new ControlProtocolHandler(null, (_, _) => Task.CompletedTask);
        var messages = ClaudeProcess.ReadResponseAsync(
            reader,
            handler,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var session = new ClaudeCodeAgentSession();
        var contents = new List<AIContent>();
        var properties = new List<AdditionalPropertiesDictionary?>();

        // Act
        if (streaming)
        {
            await foreach (
                var update in agent.ProcessStreamingMessagesAsync(
                    messages,
                    session,
                    [],
                    TestContext.Current.CancellationToken
                )
            )
                if (update.Role == ChatRole.Assistant)
                {
                    contents.AddRange(update.Contents);
                    properties.Add(update.AdditionalProperties);
                }
        }
        else
        {
            var response = await agent.ProcessNonStreamingMessagesAsync(
                messages,
                session,
                [],
                TestContext.Current.CancellationToken
            );
            properties.AddRange(
                response
                    .Messages.Where(message => message.Role == ChatRole.Assistant)
                    .Select(message => message.AdditionalProperties)
            );
            contents.AddRange(
                response
                    .Messages.Where(message => message.Role == ChatRole.Assistant)
                    .SelectMany(message => message.Contents)
            );
        }

        // Assert
        Assert.Equal(new[] { "assistant", "result" }, properties.Select(value => value?["type"]));
        Assert.Equal(
            new[] { true, false },
            properties.Select(value => Assert.IsType<bool>(value?["isIntermediateResult"]))
        );
        var stored = history.Messages.Where(message => message.Role == ChatRole.Assistant).ToList();
        Assert.Equal(
            new[] { "assistant", "result" },
            stored.Select(message => message.AdditionalProperties?["type"])
        );
        Assert.Equal(
            contents.OfType<TextContent>().Select(content => content.Text),
            stored.Select(message => message.Text)
        );
        Assert.Empty(contents.OfType<ErrorContent>());
        Assert.Equal(
            new[]
            {
                placeholderHasSchema ? "{\"answer\":\"placeholder\"}" : "placeholder",
                "{\"answer\":\"final\"}",
            },
            contents.OfType<TextContent>().Select(content => content.Text)
        );
    }

    private sealed class RecordingHistoryProvider : ChatHistoryProvider
    {
        public List<ChatMessage> Messages { get; } = [];

        protected override ValueTask StoreChatHistoryAsync(
            InvokedContext context,
            CancellationToken cancellationToken = default
        )
        {
            Messages.AddRange(context.ResponseMessages ?? []);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PendingReader : TextReader
    {
        public Channel<string> Lines { get; } = Channel.CreateUnbounded<string>();
        public TaskCompletionSource Waiting { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            if (Lines.Reader.TryRead(out var line))
                return line;
            Waiting.TrySetResult();
            return await Lines.Reader.ReadAsync(cancellationToken);
        }
    }

    internal static async Task<List<IMessage>> ReadAsync(
        TextReader reader,
        CancellationToken cancellationToken = default
    )
    {
        var handler = new ControlProtocolHandler(null, (_, _) => Task.CompletedTask);
        var messages = new List<IMessage>();
        await foreach (
            var message in ClaudeProcess.ReadResponseAsync(
                reader,
                handler,
                cancellationToken: cancellationToken
            )
        )
            messages.Add(message);
        return messages;
    }

    private static string Started(string id, string taskType = "local_agent") =>
        SystemFrame("task_started", new { task_id = id, task_type = taskType });

    private static string Finished(string id) =>
        SystemFrame("task_notification", new { task_id = id, status = "completed" });

    private static string SystemFrame(string subtype, object fields)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(fields)
        )!;
        data["type"] = "system";
        data["subtype"] = subtype;
        data["session_id"] = "session";
        data["uuid"] = Guid.NewGuid().ToString();
        return JsonSerializer.Serialize(data);
    }

    private static string Result(string text, bool isError = false) =>
        JsonSerializer.Serialize(
            new
            {
                type = "result",
                subtype = isError ? "error_during_execution" : "success",
                is_error = isError,
                duration_ms = 1,
                duration_api_ms = 1,
                num_turns = 1,
                session_id = "session",
                uuid = Guid.NewGuid().ToString(),
                result = text,
            }
        );
}
