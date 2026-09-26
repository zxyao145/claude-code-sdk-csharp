using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeSdk;

/// <summary>
/// Core process manager for Claude CLI communication.
/// Handles subprocess lifecycle, message streaming, and JSON-RPC protocol.
/// </summary>
internal sealed class ClaudeProcess : IAsyncDisposable
{
    // Process gives stdout a 4KB buffer; 64KB reads large tool results in far fewer pipe reads.
    private const int StdoutBufferSize = 64 * 1024;
    private const int StderrTailChars = 64 * 1024;
    private static readonly byte[] NewLine = [(byte)'\n'];

    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private readonly string _cliPath;
    private readonly SemaphoreSlim _stdinLock = new(1, 1);
    private readonly ControlProtocolHandler _controlProtocol;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private Task<string>? _stderrDrain;
    private bool _disposed;

    public ClaudeProcess(ClaudeCodeOptions options, string? cliPath = null, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
        _cliPath = cliPath ?? FindClaudeCli();
        _controlProtocol = new ControlProtocolHandler(options.CanUseTool, WriteLineAsync, logger);
    }

    /// <summary>
    /// Start Claude CLI process and send initial prompt.
    /// </summary>
    public async Task StartAsync(
        object? prompt = null,
        CancellationToken cancellationToken = default
    )
    {
        if (_process != null)
            throw new CLIConnectionException("Already connected");

        var args = CommandUtil.BuildCommand(_options, true, "");
        _logger?.LogDebug("Starting Claude CLI: {CliPath} {Args}", _cliPath, string.Join(" ", args));

        _process = new Process { StartInfo = BuildStartInfo(_cliPath, args) };

        try
        {
            if (!_process.Start())
                throw new ProcessException("Failed to start Claude CLI process");

            _stdin = _process.StandardInput;
            _stdout = new StreamReader(
                _process.StandardOutput.BaseStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                StdoutBufferSize
            );
            _stderr = _process.StandardError;
            // An unread redirected pipe fills up and blocks the CLI's writes, which stalls stdout
            // too. Drain stderr for the whole process lifetime, keeping its tail for error reports.
            _stderrDrain = DrainAsync(_stderr, StderrTailChars, _logger);

            if (prompt != null)
                await SendInitialPromptAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting process");
            await CleanupProcessAsync();
            throw;
        }
        if (_process.HasExited)
        {
            await TryReadStderr(cancellationToken);
        }
    }

    /// <summary>
    /// Send messages to Claude.
    /// </summary>
    public async Task SendAsync(
        IEnumerable<Dictionary<string, object>> messages,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var message in messages)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("stdin WriteLine:{line}", JsonUtil.Serialize(message));
            await WriteJsonLineAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Receive messages from Claude as JSON dictionaries.
    /// Terminates at a result with no pending delegated agents, or at an error result.
    /// </summary>
    public async IAsyncEnumerable<IMessage> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (_stdout == null)
            throw new CLIConnectionException("Not connected");

        IMessage? last = null;
        await foreach (
            var message in ReadResponseAsync(_stdout, _controlProtocol, _logger, cancellationToken)
        )
        {
            last = message;
            yield return message;
        }

        // A final result leaves the CLI running for the next turn; any other end is stdout EOF.
        if (last is not ResultMessage { IsIntermediate: false })
            await WaitForExitAfterEofAsync(cancellationToken);
        await TryReadStderr(cancellationToken);
    }

    internal static async IAsyncEnumerable<IMessage> ReadResponseAsync(
        TextReader stdout,
        ControlProtocolHandler controlProtocol,
        ILogger? logger = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var pendingAgents = new HashSet<string>(StringComparer.Ordinal);
        var deferredResult = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await stdout.ReadLineAsync(cancellationToken);
            logger?.LogDebug("stdout ReadLine from process stdout:{line}", line);

            if (line == null)
            {
                if (deferredResult || pendingAgents.Count > 0)
                    throw new ProcessException(
                        "Claude Code exited before background agents produced a final result."
                    );
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse once: the control protocol and the message parser share this element.
            if (MessageParser.ParseLine(line, logger) is not { } jsonLine)
                continue;

            if (controlProtocol.TryHandle(jsonLine, cancellationToken))
                continue;

            var msg = MessageParser.ParseMessage(jsonLine, logger);
            if (msg == null)
                continue;

            if (msg is SystemMessage system)
                TrackAgentTask(system, pendingAgents);

            // Forward every result, but a parent turn's result does not end the run
            // while delegated agents are still pending.
            if (msg is ResultMessage result)
            {
                deferredResult = !result.IsError && pendingAgents.Count > 0;
                msg = result with { IsIntermediate = deferredResult };
            }

            yield return msg;

            if (msg is ResultMessage && !deferredResult)
                yield break;
        }
    }

    private static void TrackAgentTask(SystemMessage message, HashSet<string> pendingAgents)
    {
        if (
            !message.Data.TryGetValue("task_id", out var idValue)
            || idValue is not JsonElement { ValueKind: JsonValueKind.String } idElement
            || idElement.GetString() is not { Length: > 0 } taskId
        )
            return;

        switch (message.Subtype)
        {
            case "task_started":
                // Shells, monitors, teammates and remote agents may run indefinitely.
                // Match the bounded task types waited on by the official Python SDK.
                if (
                    message.Data.TryGetValue("task_type", out var typeValue)
                    && typeValue is JsonElement { ValueKind: JsonValueKind.String } typeElement
                    && typeElement.GetString() is "local_agent" or "local_workflow"
                )
                    pendingAgents.Add(taskId);
                break;
            case "task_notification":
                pendingAgents.Remove(taskId);
                break;
            case "task_updated":
                if (
                    message.Data.TryGetValue("patch", out var patchValue)
                    && patchValue is JsonElement { ValueKind: JsonValueKind.Object } patch
                    && patch.TryGetProperty("status", out var status)
                    && status.ValueKind == JsonValueKind.String
                    && status.GetString() is "completed" or "failed" or "stopped" or "killed"
                )
                    pendingAgents.Remove(taskId);
                break;
        }
    }

    private async Task WaitForExitAfterEofAsync(CancellationToken cancellationToken)
    {
        if (_process == null)
            return;

        // Stdout closes as the CLI exits, but the exit is observed asynchronously. Wait briefly so
        // TryReadStderr sees it and reports the failure; a CLI that keeps running is left alone.
        try
        {
            await _process
                .WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger?.LogDebug("Claude CLI closed stdout but has not exited.");
        }
    }

    private async Task TryReadStderr(CancellationToken cancellationToken = default)
    {
        if (_stderrDrain != null)
        {
            // The drain completes at EOF. For long-lived processes we should only report stderr
            // when the process has exited, otherwise this can block normal multi-turn flows.
            if (_process?.HasExited != true)
            {
                _logger?.LogDebug("Skipping stderr check because process is still running.");
                return;
            }

            var error = await _stderrDrain.WaitAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(error))
            {
                _logger?.LogDebug("No error output from process stderr.");
            }
            else
            {
                _logger?.LogError("Error output from process stderr: {error}", error);
                var exitCode = _process?.ExitCode ?? -1;
                await CleanupProcessAsync();
                if (error.Contains($"Error: Session ID") && error.Contains($"is already in use."))
                {
                    throw new SessionIdDuplicateException(_options.SessionId?.ToString());
                }
                throw new ProcessException("Error from Claude CLI process", exitCode, error);
            }
        }
    }

    /// <summary>
    /// Kill the CLI process immediately.
    /// </summary>
    public async Task InterruptAsync()
    {
        if (_process == null || _process.HasExited)
            throw new CLIConnectionException("Process not running");

        try
        {
            await TerminateProcessAsync(_process);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to interrupt process");
            throw new ProcessException("Failed to interrupt process", null, ex.Message);
        }
    }

    private async Task SendInitialPromptAsync(object prompt, CancellationToken cancellationToken)
    {
        if (_stdin == null)
            return;

        switch (prompt)
        {
            case string stringPrompt:
                {
                    var message = new Dictionary<string, object>
                    {
                        ["type"] = "user",
                        ["message"] = new Dictionary<string, object>
                        {
                            ["role"] = "user",
                            ["content"] = stringPrompt,
                        },
                        ["parent_tool_use_id"] = null!,
                        ["session_id"] = "default",
                    };

                    await WriteJsonLineAsync(message, cancellationToken);
                    break;
                }

            case IAsyncEnumerable<Dictionary<string, object>> asyncEnumerable:
                {
                    await foreach (var message in asyncEnumerable.WithCancellation(cancellationToken))
                    {
                        await WriteJsonLineAsync(message, cancellationToken);
                    }
                    break;
                }
        }
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        await _stdinLock.WaitAsync(cancellationToken);
        try
        {
            if (_stdin == null)
                throw new CLIConnectionException("Not connected");

            await _stdin.WriteLineAsync(line.AsMemory(), cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        finally
        {
            _stdinLock.Release();
        }
    }

    private async Task WriteJsonLineAsync(
        Dictionary<string, object> message,
        CancellationToken cancellationToken
    )
    {
        await _stdinLock.WaitAsync(cancellationToken);
        try
        {
            if (_stdin == null)
                throw new CLIConnectionException("Not connected");

            // Bypass the writer so a large prompt (e.g. a base64 image) never becomes a string.
            // Mixing is safe: the writer flushes after every line and wrote its preamble at start.
            await WriteJsonLineToAsync(_stdin.BaseStream, message, cancellationToken);
        }
        finally
        {
            _stdinLock.Release();
        }
    }

    /// <summary>
    /// Write a message as one JSON line: the UTF-8 bytes of <see cref="JsonUtil.Serialize"/> plus '\n'.
    /// </summary>
    internal static async Task WriteJsonLineToAsync(
        Stream stream,
        Dictionary<string, object> message,
        CancellationToken cancellationToken
    )
    {
        await JsonSerializer.SerializeAsync(
            stream,
            message,
            JsonUtil.CAMELCASE_OPTIONS,
            cancellationToken
        );
        await stream.WriteAsync(NewLine, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Read a stream to EOF, keeping at most its last <paramref name="maxChars"/> characters.
    /// </summary>
    internal static async Task<string> DrainAsync(
        TextReader reader,
        int maxChars,
        ILogger? logger = null
    )
    {
        var tail = new StringBuilder();
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                tail.Append(buffer, 0, read);
                if (tail.Length > maxChars)
                    tail.Remove(0, tail.Length - maxChars);
            }
        }
        catch (Exception ex)
        {
            // Cleanup disposes the reader, which may interrupt a pending read.
            logger?.LogDebug(ex, "Stopped draining stderr.");
        }

        return tail.ToString();
    }

    private ProcessStartInfo BuildStartInfo(string fileName, IReadOnlyList<string> arguments)
    {
        var workingDir = _options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var startInfo = new ProcessStartInfo
        {
            FileName = CommandUtil.GetOptimallyQualifiedTargetFilePath(fileName),
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // ArgumentList, not Arguments: .NET quotes each entry for the platform. Joining them into
        // one string leaves any value containing a space or a newline to be re-split by the
        // receiving process, so a multi-word --system-prompt arrived as its first word and the rest
        // became stray positional arguments.
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Then apply custom environment variables from options (overrides system vars)
        if (_options.EnvironmentVariables != null)
        {
            foreach (var (key, value) in _options.EnvironmentVariables)
            {
                if (value != null)
                    startInfo.EnvironmentVariables[key] = value;
                else
                    startInfo.EnvironmentVariables.Remove(key);
            }
        }

        // Finally, set SDK-specific variables (highest priority)
        startInfo.EnvironmentVariables["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            startInfo.EnvironmentVariables["ANTHROPIC_AUTH_TOKEN"] = _options.ApiKey;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            startInfo.EnvironmentVariables["ANTHROPIC_BASE_URL"] = _options.BaseUrl;

        return startInfo;
    }

    private static string FindClaudeCli()
    {
        // Try PATH first
        var cli = Which("claude");
        if (cli != null)
            return cli;

        // Try common installation locations
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var isWindows = OperatingSystem.IsWindows();

        var locations = new[]
        {
            "claude",
            Path.Combine(home, ".npm-global", "bin", "claude"),
            Path.Combine("/usr/local/bin", "claude"),
            Path.Combine(home, ".local", "bin", "claude"),
            Path.Combine(home, "node_modules", ".bin", "claude"),
            Path.Combine(home, ".yarn", "bin", "claude"),
        };

        foreach (var path in locations)
        {
            if (File.Exists(path))
                return path;

            if (isWindows && File.Exists(path + ".exe"))
                return path + ".exe";
        }

        // Check if Node.js is installed
        if (Which("node") == null)
        {
            throw new CLINotFoundException(
                "Claude Code requires Node.js, which is not installed.\n\n"
                    + "Install Node.js from: https://nodejs.org/\n\n"
                    + "After installing Node.js, install Claude Code:\n"
                    + "  npm install -g @anthropic-ai/claude-code"
            );
        }

        // CLI not found
        throw new CLINotFoundException(
            "Claude Code not found. Install with:\n"
                + "  npm install -g @anthropic-ai/claude-code\n\n"
                + "If already installed locally, try:\n"
                + "  export PATH=\"$HOME/node_modules/.bin:$PATH\""
        );
    }

    private static string? Which(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);
        var isWindows = OperatingSystem.IsWindows();

        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, command);
            if (File.Exists(fullPath))
                return fullPath;

            if (isWindows)
            {
                var fullExe = fullPath + ".exe";
                if (File.Exists(fullExe))
                    return fullExe;
            }
        }
        return null;
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    private async Task CleanupProcessAsync()
    {
        _controlProtocol.CancelAll();
        if (_process != null)
        {
            try
            {
                await TerminateProcessAsync(_process);
            }
            catch
            { /* Ignore cleanup errors */
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        _stdin?.Dispose();
        _stdin = null;

        _stdout?.Dispose();
        _stdout = null;

        _stderr?.Dispose();
        _stderr = null;
        _stderrDrain = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await CleanupProcessAsync();
            _disposed = true;
        }
    }
}
