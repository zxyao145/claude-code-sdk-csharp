using System;
using System.Collections;

namespace ClaudeCodeSdk.Exceptions;

/// <summary>
/// Base exception for all Claude SDK errors.
/// </summary>
public class ClaudeSDKException : Exception
{
    public ClaudeSDKException() : base() { }
    public ClaudeSDKException(string message) : base(message) { }
    public ClaudeSDKException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when unable to connect to Claude Code.
/// </summary>
public class CLIConnectionException : ClaudeSDKException
{
    public CLIConnectionException() : base() { }
    public CLIConnectionException(string message) : base(message) { }
    public CLIConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when Claude Code is not found or not installed.
/// </summary>
public class CLINotFoundException : CLIConnectionException
{
    public string? CliPath { get; }

    public CLINotFoundException(string message = "Claude Code not found", string? cliPath = null) 
        : base(cliPath != null ? $"{message}: {cliPath}" : message)
    {
        CliPath = cliPath;
    }
}

/// <summary>
/// Raised when the CLI process fails.
/// </summary>
public class ProcessException : ClaudeSDKException
{
    public int? ExitCode { get; }
    public string? StandardError { get; }

    public ProcessException(string message, int? exitCode = null, string? stderr = null)
        : base(BuildMessage(message, exitCode, stderr))
    {
        ExitCode = exitCode;
        StandardError = stderr;
    }

    private static string BuildMessage(string message, int? exitCode, string? stderr)
    {
        if (exitCode.HasValue)
        {
            message = $"{message} (exit code: {exitCode})";
        }
        
        if (!string.IsNullOrEmpty(stderr))
        {
            message = $"{message}\nError output: {stderr}";
        }

        return message;
    }
}

/// <summary>
/// Raised when unable to decode JSON from CLI output.
/// </summary>
public class CLIJsonDecodeException : ClaudeSDKException
{
    public string Line { get; }
    public Exception OriginalError { get; }

    public CLIJsonDecodeException(string line, Exception originalError)
        : base($"Failed to decode JSON: {(line.Length > 100 ? line[..100] + "..." : line)}")
    {
        Line = line;
        OriginalError = originalError;
    }
}

/// <summary>
/// Raised when unable to parse a message from CLI output.
/// </summary>
public class MessageParseException : ClaudeSDKException
{
    public object? ExpData { get; }

    public MessageParseException(string message, object? data = null) : base(message)
    {
        this.ExpData = data;
    }
}