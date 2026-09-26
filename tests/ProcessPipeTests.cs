using System.Text;
using ClaudeCodeSdk.Exceptions;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Xunit;

namespace ClaudeCodeSdk.Tests;

/// <summary>
/// Pipe handling between the SDK and the CLI subprocess.
/// </summary>
public class ProcessPipeTests
{
    private const string ResultLine =
        """{"type":"result","subtype":"success","is_error":false,"duration_ms":1,"duration_api_ms":1,"num_turns":1,"session_id":"session","uuid":"result-1","result":"done"}""";

    [Fact]
    public async Task DrainAsync_LongInput_KeepsOnlyTail()
    {
        // Arrange
        using var reader = new StringReader(new string('a', 10_000) + "tail");

        // Act
        var tail = await ClaudeProcess.DrainAsync(reader, maxChars: 10);

        // Assert
        Assert.Equal("aaaaaatail", tail);
    }

    [Fact]
    public async Task DrainAsync_ShortInput_KeepsEverything()
    {
        // Arrange
        using var reader = new StringReader("Error: boom");

        // Act
        var tail = await ClaudeProcess.DrainAsync(reader, maxChars: 1024);

        // Assert
        Assert.Equal("Error: boom", tail);
    }

    [Fact]
    public async Task ReceiveAsync_LargeStderrWhileRunning_DoesNotStall()
    {
        // An unread pipe fills up (64KB on Linux/macOS) and blocks the CLI's writes. Before
        // stderr was drained while the process ran, the result line below never arrived.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake CLI is a POSIX shell script.");

        // Arrange
        // Sleeping keeps the process alive, so the SDK does not report the stderr at exit.
        var cli = CreateFakeCli(
            $"""
            head -c 200000 /dev/zero | tr '\0' 'x' >&2
            echo '{ResultLine}'
            sleep 5
            """
        );
        try
        {
            await using var process = new ClaudeProcess(new ClaudeCodeOptions(), cli);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken
            );
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            // Act
            await process.StartAsync(cancellationToken: timeout.Token);
            var messages = new List<IMessage>();
            await foreach (var message in process.ReceiveAsync(timeout.Token))
                messages.Add(message);

            // Assert
            Assert.Equal("done", Assert.IsType<ResultMessage>(Assert.Single(messages)).Result);
        }
        finally
        {
            File.Delete(cli);
        }
    }

    [Fact]
    public async Task ReceiveAsync_CliExitsWithSessionError_ReportsStderr()
    {
        // Stdout reaches EOF as the CLI exits, but the exit itself is observed asynchronously.
        // The failure on stderr must still be reported rather than ending the stream silently.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake CLI is a POSIX shell script.");

        // Arrange
        var cli = CreateFakeCli(
            """
            echo "Error: Session ID abc is already in use." >&2
            exit 1
            """
        );
        try
        {
            await using var process = new ClaudeProcess(new ClaudeCodeOptions(), cli);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken
            );
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            // Act
            // A fast exit can already be seen by StartAsync, so the report may come from either.
            var receive = async () =>
            {
                await process.StartAsync(cancellationToken: timeout.Token);
                await foreach (var _ in process.ReceiveAsync(timeout.Token)) { }
            };

            // Assert
            await Assert.ThrowsAsync<SessionIdDuplicateException>(receive);
        }
        finally
        {
            File.Delete(cli);
        }
    }

    [Fact]
    public async Task WriteJsonLineToAsync_ImageBytes_MatchesSerializedBase64Line()
    {
        // Arrange
        byte[] image = [1, 2, 3, 250];
        var expected = JsonUtil.Serialize(ImagePrompt(Convert.ToBase64String(image))) + "\n";
        using var stream = new MemoryStream();

        // Act
        await ClaudeProcess.WriteJsonLineToAsync(
            stream,
            ImagePrompt((ReadOnlyMemory<byte>)image),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static Dictionary<string, object> ImagePrompt(object data) =>
        new()
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["type"] = "image",
                        ["source"] = new Dictionary<string, object>
                        {
                            ["type"] = "base64",
                            ["media_type"] = "image/png",
                            ["data"] = data,
                        },
                    },
                    new() { ["type"] = "text", ["text"] = "描述 <this> & \"that\"" },
                },
            },
            ["parent_tool_use_id"] = null!,
            ["session_id"] = "session-1",
        };

    private static string CreateFakeCli(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fake-claude-{Guid.NewGuid():N}.sh");
        // Raw strings take the source file's line endings; a CR would break every sh command.
        File.WriteAllText(path, "#!/bin/sh\n" + script.ReplaceLineEndings("\n") + "\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
        return path;
    }
}
