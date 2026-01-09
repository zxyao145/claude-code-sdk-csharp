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
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private readonly string _cliPath;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private bool _disposed;

    public ClaudeProcess(ClaudeCodeOptions options, string? cliPath = null, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
        _cliPath = cliPath ?? FindClaudeCli();
    }

    /// <summary>
    /// Start Claude CLI process and send initial prompt.
    /// </summary>
    public async Task StartAsync(object? prompt = null, CancellationToken cancellationToken = default)
    {
        if (_process != null)
            throw new CLIConnectionException("Already connected");

        var args = CommandUtil.BuildCommand(_options, true, "");
        _logger?.LogDebug("Starting Claude CLI: {CliPath} {Args}", _cliPath, string.Join(" ", args));

        _process = new Process { StartInfo = BuildStartInfo(_cliPath, string.Join(" ", args)) };

        try
        {
            if (!_process.Start())
                throw new ProcessException("Failed to start Claude CLI process");

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;

            if (prompt != null)
                await SendInitialPromptAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting process");
            await CleanupProcessAsync();
            throw;
        }
    }

    /// <summary>
    /// Send messages to Claude.
    /// </summary>
    public async Task SendAsync(IEnumerable<Dictionary<string, object>> messages, CancellationToken cancellationToken = default)
    {
        if (_stdin == null)
            throw new CLIConnectionException("Not connected");

        foreach (var message in messages)
        {
            var json = JsonUtil.Serialize(message);
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
        }

        await _stdin.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Receive messages from Claude as JSON dictionaries.
    /// Automatically terminates when receiving "result" type message.
    /// </summary>
    public async IAsyncEnumerable<Dictionary<string, object>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_stdout == null)
            throw new CLIConnectionException("Not connected");

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _stdout.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            Dictionary<string, object>? data;
            try
            {
                data = JsonUtil.Deserialize<Dictionary<string, object>>(line);
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "JSON parse error: {Line}", line);
                throw new CLIJsonDecodeException(line, ex);
            }

            if (data != null)
            {
                yield return data;

                // Auto-terminate on result message
                if (data.TryGetValue("type", out var typeValue) && typeValue?.ToString() == "result")
                    break;
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
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to interrupt process");
            throw new ProcessException("Failed to interrupt process", null, ex.Message);
        }
    }

    private async Task SendInitialPromptAsync(object prompt, CancellationToken cancellationToken)
    {
        if (_stdin == null) return;

        if (prompt is string stringPrompt)
        {
            var message = new Dictionary<string, object>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = stringPrompt
                },
                ["parent_tool_use_id"] = null!,
                ["session_id"] = "default"
            };

            var json = JsonUtil.Serialize(message);
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        else if (prompt is IAsyncEnumerable<Dictionary<string, object>> asyncEnumerable)
        {
            await foreach (var message in asyncEnumerable.WithCancellation(cancellationToken))
            {
                var json = JsonUtil.Serialize(message);
                await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
            }
            await _stdin.FlushAsync(cancellationToken);
        }
    }

    private ProcessStartInfo BuildStartInfo(string fileName, string arguments)
    {
        var workingDir = _options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var startInfo = new ProcessStartInfo
        {
            FileName = CommandUtil.GetOptimallyQualifiedTargetFilePath(fileName),
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Set environment variables
        var env = _options.EnvironmentVariables ?? new Dictionary<string, string?>();
        env["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            env["ANTHROPIC_AUTH_TOKEN"] = _options.ApiKey;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            env["ANTHROPIC_BASE_URL"] = _options.BaseUrl;

        foreach (var (key, value) in env)
        {
            if (value != null)
                startInfo.Environment[key] = value;
            else
                startInfo.Environment.Remove(key);
        }

        return startInfo;
    }

    private static string FindClaudeCli()
    {
        // Try PATH first
        string? cli = Which("claude");
        if (cli != null) return cli;

        // Try common installation locations
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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
            if (File.Exists(path)) return path;

            if (OperatingSystem.IsWindows() && File.Exists(path + ".exe"))
                return path + ".exe";
        }

        // Check if Node.js is installed
        if (Which("node") == null)
        {
            throw new CLINotFoundException(
                "Claude Code requires Node.js, which is not installed.\n\n" +
                "Install Node.js from: https://nodejs.org/\n\n" +
                "After installing Node.js, install Claude Code:\n" +
                "  npm install -g @anthropic-ai/claude-code");
        }

        // CLI not found
        throw new CLINotFoundException(
            "Claude Code not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n\n" +
            "If already installed locally, try:\n" +
            "  export PATH=\"$HOME/node_modules/.bin:$PATH\"");
    }

    private static string? Which(string command)
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] paths = pathEnv.Split(Path.PathSeparator);

        foreach (string p in paths)
        {
            string fullPath = Path.Combine(p, command);
            if (File.Exists(fullPath)) return fullPath;

            if (OperatingSystem.IsWindows())
            {
                string fullExe = fullPath + ".exe";
                if (File.Exists(fullExe)) return fullExe;
            }
        }
        return null;
    }

    private async Task CleanupProcessAsync()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync();
                }
            }
            catch { /* Ignore cleanup errors */ }
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