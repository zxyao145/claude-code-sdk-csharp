using ClaudeCodeSdk.Exceptions;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class UnknownMessageTypeTests
{
    [Fact]
    public void ParseMessage_UnknownMessageType_ReturnsNullInsteadOfThrowing()
    {
        // A real message the CLI emits that the SDK has no case for. Before this was handled,
        // it threw MessageParseException and tore down ClaudeProcess.ReceiveAsync's loop,
        // ending the whole session.
        var json = """
            {"type":"rate_limit_event","rate_limit_info":{"status":"allowed_warning","rateLimitType":"seven_day","utilization":0.5},"uuid":"00000000-0000-0000-0000-000000000000","session_id":"00000000-0000-0000-0000-000000000000"}
            """;

        var message = MessageParser.ParseMessage(json);

        Assert.Null(message);
    }

    [Fact]
    public void ParseMessage_UnknownMessageType_DoesNotPreventParsingLaterKnownMessages()
    {
        // The regression that actually mattered: an unknown message arriving mid-stream must not
        // stop the result message that follows it from being read.
        var unknown = """{"type":"some_future_event_type","payload":{"anything":true}}""";
        var result = """
            {"type":"result","subtype":"success","is_error":false,"duration_ms":1,"duration_api_ms":1,"num_turns":1,"session_id":"00000000-0000-0000-0000-000000000000","total_cost_usd":0.01,"uuid":"00000000-0000-0000-0000-000000000000","result":"done"}
            """;

        Assert.Null(MessageParser.ParseMessage(unknown));

        var parsed = MessageParser.ParseMessage(result);

        var resultMessage = Assert.IsType<Types.ResultMessage>(parsed);
        Assert.False(resultMessage.IsError);
        Assert.Equal("done", resultMessage.Result);
    }

    [Fact]
    public void ParseMessage_MalformedMessages_StillThrow()
    {
        // Skipping unknown TYPES must not become "swallow everything" -- a message with no type
        // at all is still malformed and should still be reported.
        Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage("""{"no_type_field":true}""")
        );
    }
}
