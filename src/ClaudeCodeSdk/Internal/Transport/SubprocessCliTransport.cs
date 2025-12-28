using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeSdk.Internal.Transport;

/// <summary>
/// Transport implementation that communicates with Claude Code via subprocess.
/// </summary>
internal class SubprocessCliTransport : ITransport
{
    private readonly object _prompt;
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private readonly string _cliPath;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public SubprocessCliTransport(object prompt, ClaudeCodeOptions options, string? cliPath = null, ILogger? logger = null)
    {
        _prompt = prompt;
        _options = options;
        _logger = logger;
        _cliPath = cliPath ?? FindClaudeCli();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process != null)
            throw new CLIConnectionException("Already connected");

        var args = CommandUtil.BuildCommand(_options, true, "");

        _logger?.LogDebug("Starting Claude CLI process: {CliPath} {Args}", _cliPath, string.Join(" ", args));

        _process = new Process
        {
            StartInfo = CreateStartInfo(_cliPath, string.Join(" ", args)),
        };

        try
        {
            if (!_process.Start())
            {
                throw new ProcessException("Failed to start Claude CLI process");
            }

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;


            await SendInitialPromptAsync(cancellationToken);
        }
        catch(Exception ex) 
        {
            _logger?.LogError(ex, "Error during process start or initial prompt send");
            await DisposeProcessAsync();
            throw;
        }
    }


    private async Task SendInitialPromptAsync(CancellationToken cancellationToken)
    {
        if (_stdin == null)
            return;

        if (_prompt is string stringPrompt)
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

            var json = JsonSerializer.Serialize(message, JsonOptions);
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        else if (_prompt is IAsyncEnumerable<Dictionary<string, object>> asyncEnumerable)
        {
            await foreach (var message in asyncEnumerable.WithCancellation(cancellationToken))
            {
                var json = JsonSerializer.Serialize(message, JsonOptions);
                await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
            }
            await _stdin.FlushAsync(cancellationToken);
        }
    }


    public async Task SendRequestAsync(
        IEnumerable<Dictionary<string, object>> messages,
        Dictionary<string, object> metadata, 
        CancellationToken cancellationToken = default
        )
    {
        if (_stdin == null)
            throw new CLIConnectionException("Not connected");

        foreach (var message in messages)
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
        }

        await _stdin.FlushAsync(cancellationToken);
    }

    public async IAsyncEnumerable<Dictionary<string, object>> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Failed to parse JSON: {Line}", line);
                throw new CLIJsonDecodeException(line, ex);
            }

            if (data != null)
            {
                yield return data;
                
                // Check if this is a result message, which indicates the end of the conversation
                if (data.TryGetValue("type", out var typeValue) && 
                    typeValue?.ToString() == "result")
                {
                    break;
                }
            }
        }
    }

    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_process == null || _process.HasExited)
            throw new CLIConnectionException("Process not running");

        try
        {
            // Send SIGINT equivalent on Windows/Unix
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to interrupt process");
            throw new ProcessException("Failed to interrupt process", null, ex.Message);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisposeProcessAsync();
    }



    private async Task DisposeProcessAsync()
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
            catch
            {
                // Ignore errors during cleanup
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
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisposeProcessAsync();
            _disposed = true;
        }
    }

    private ProcessStartInfo CreateStartInfo(string fileName, string arguments)
    {
        var startInfo = CreateBaseStartInfo(fileName, arguments, _options.WorkingDirectory);

        var environmentVariables = _options.EnvironmentVariables ?? new Dictionary<string, string?>();
        environmentVariables["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            environmentVariables["ANTHROPIC_AUTH_TOKEN"] = _options.ApiKey;
        }
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            environmentVariables["ANTHROPIC_BASE_URL"] = _options.BaseUrl;
        }

        // Set environment variables
        foreach (var (key, value) in environmentVariables)
        {
            if (value is not null)
            {
                startInfo.Environment[key] = value;
            }
            else
            {
                startInfo.Environment.Remove(key);
            }
        }

        return startInfo;
    }

    private static ProcessStartInfo CreateBaseStartInfo(string fileName, 
        string arguments, 
        string? workingDirectory = null)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();
        var startInfo = new ProcessStartInfo
        {
            FileName = CommandUtil.GetOptimallyQualifiedTargetFilePath(fileName),
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        return startInfo;
    }

    private static string FindClaudeCli()
    {
        // 1. 尝试在 PATH 中找到 "claude"
        string? cli = Which("claude");
        if (cli != null)
        {
            return cli;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var locations = new List<string>
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
            {
                return path;
            }
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(path + ".exe"))
                {
                    return path + ".exe";
                }
            }
        }

        // 3. 检查 node 是否安装
        bool nodeInstalled = Which("node") != null;

        if (!nodeInstalled)
        {
            string errorMsg =
                "Claude Code requires Node.js, which is not installed.\n\n" +
                "Install Node.js from: https://nodejs.org/\n\n" +
                "After installing Node.js, install Claude Code:\n" +
                "  npm install -g @anthropic-ai/claude-code";
            throw new CLINotFoundException(errorMsg);
        }

        // 4. 都没找到
        throw new CLINotFoundException(
            "Claude Code not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n\n" +
            "If already installed locally, try:\n" +
            "  export PATH=\"$HOME/node_modules/.bin:$PATH\"\n\n" +
            "Or specify the path when creating transport:\n" +
            "  SubprocessCLITransport(..., cli_path='/path/to/claude')"
        );

        //foreach (var candidate in locations)
        //{
        //    try
        //    {
        //        var testProcess = new Process
        //        {
        //            StartInfo = CreateBaseStartInfo(candidate, "--version")
        //        };
        //        var npmBin = Environment.ExpandEnvironmentVariables(@"%AppData%\npm");
        //        testProcess.StartInfo.EnvironmentVariables["PATH"] += ";" + npmBin;

        //        var env = testProcess.StartInfo.EnvironmentVariables["PATH"];

        //        testProcess.Start();
        //        testProcess.WaitForExit();

        //        if (testProcess.ExitCode == 0)
        //        {
        //            return candidate;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        System.Console.WriteLine($"Error testing candidate '{candidate}': {e.Message}");
        //        // Try next candidate
        //    }
        //}

    }

    private static string? Which(string command)
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] paths = pathEnv.Split(Path.PathSeparator);

        foreach (string p in paths)
        {
            string fullPath = Path.Combine(p, command);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            // Windows 上可能是 exe
            if (OperatingSystem.IsWindows())
            {
                string fullExe = fullPath + ".exe";
                if (File.Exists(fullExe))
                {
                    return fullExe;
                }
            }
        }
        return null;
    }
}