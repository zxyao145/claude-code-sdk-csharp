using System.Text.Json;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class StructuredOutputTests
{
    [Theory]
    [InlineData("{\"answer\":\"hello\",\"nested\":{\"value\":1}}")]
    [InlineData("[1,2]")]
    [InlineData("\"hello\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public void ParseMessage_StructuredOutput_PreservesJson(string output)
    {
        // Arrange
        var json = ResultJson(output);

        // Act
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(json));
        var serialized = JsonSerializer.SerializeToElement(message);

        // Assert
        Assert.True(
            JsonElement.DeepEquals(
                JsonElement.Parse(output),
                serialized.GetProperty("structured_output")
            )
        );
    }

    [Theory]
    [InlineData(false, "{\"answer\":\"hello\"}")]
    [InlineData(true, "{\"answer\":\"hello\"}")]
    [InlineData(false, "\"hello\"")]
    [InlineData(true, "\"hello\"")]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    public async Task ProcessMessages_StructuredOutput_PrefersJsonOverResultText(
        bool streaming,
        string output
    )
    {
        // Arrange
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(ResultJson(output)));

        // Act
        var (contents, _) = await ProcessAsync(message, streaming, schemaEnabled: true);

        // Assert
        Assert.Equal(output, Assert.Single(contents.OfType<TextContent>()).Text);
        Assert.Empty(contents.OfType<ErrorContent>());
        Assert.Single(contents.OfType<UsageContent>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessMessages_SchemaEnabledWithoutStructuredOutput_ReturnsFatalError(
        bool streaming
    )
    {
        // Arrange
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(ResultJson(null)));

        // Act
        var (contents, _) = await ProcessAsync(message, streaming, schemaEnabled: true);

        // Assert
        var error = Assert.Single(contents.OfType<ErrorContent>());
        Assert.True(Assert.IsType<bool>(error.AdditionalProperties!["isFatalError"]));
        Assert.Empty(contents.OfType<TextContent>());
        Assert.Single(contents.OfType<UsageContent>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessMessages_WithoutSchema_ExposesPlainTextAsResultProperty(bool streaming)
    {
        // Arrange
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(ResultJson(null)));

        // Act
        var (contents, properties) = await ProcessAsync(message, streaming, schemaEnabled: false);

        // Assert: "plain text" is the last assistant message's text, already carried by that
        // message, so the final result text is exposed via AdditionalProperties, not a TextContent.
        Assert.Empty(contents.OfType<TextContent>());
        Assert.Empty(contents.OfType<ErrorContent>());
        var resultProperties = Assert.Single(properties);
        Assert.Equal("plain text", resultProperties?["result"]);
        Assert.Equal(false, resultProperties?["isError"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessMessages_ErrorResult_DoesNotExposeStructuredOutputAsSuccess(
        bool streaming
    )
    {
        // Arrange
        var message = Assert.IsType<ResultMessage>(
            MessageParser.ParseMessage(ResultJson("{}"))
        ) with
        {
            IsError = true,
            Subtype = "error_max_structured_output_retries",
            Result = "schema validation failed",
        };

        // Act
        var (contents, _) = await ProcessAsync(message, streaming, schemaEnabled: true);

        // Assert
        Assert.Equal(
            "schema validation failed",
            Assert.Single(contents.OfType<ErrorContent>()).Message
        );
        Assert.Empty(contents.OfType<TextContent>());
    }

    private static string ResultJson(string? output) =>
        $$"""
            {
              "type":"result","uuid":"result-1","subtype":"success",
              "duration_ms":10,"duration_api_ms":5,"is_error":false,
              "num_turns":1,"session_id":"session-1","result":"plain text",
              "usage":{"input_tokens":10,"output_tokens":2}
              {{(output == null ? "" : ",\"structured_output\":" + output)}}
            }
            """;

    private static async Task<(
        List<AIContent> Contents,
        List<AdditionalPropertiesDictionary?> Properties
    )> ProcessAsync(ResultMessage message, bool streaming, bool schemaEnabled)
    {
        // The constructor is lazy; these internal message-processing paths never create a CLI client.
        using var agent = new ClaudeCodeAIAgent(
            new ClaudeCodeAIAgentOptions
            {
                ExtraArgs = schemaEnabled
                    ? new Dictionary<string, string?> { ["json-schema"] = "{}" }
                    : [],
            }
        );
        var session = new ClaudeCodeAgentSession();
        if (!streaming)
        {
            var response = await agent.ProcessNonStreamingMessagesAsync(
                Messages(message),
                session,
                []
            );
            return (
                response.Messages.SelectMany(item => item.Contents).ToList(),
                response.Messages.Select(item => item.AdditionalProperties).ToList()
            );
        }

        var contents = new List<AIContent>();
        var properties = new List<AdditionalPropertiesDictionary?>();
        await foreach (
            var update in agent.ProcessStreamingMessagesAsync(Messages(message), session, [])
        )
        {
            contents.AddRange(update.Contents);
            properties.Add(update.AdditionalProperties);
        }
        return (contents, properties);
    }

    private static async IAsyncEnumerable<IMessage> Messages(IMessage message)
    {
        await Task.CompletedTask;
        yield return message;
    }
}
