using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ClaudeCodeSdk.Utils;

internal class CommandUtil
{
    /// <summary>
    /// copy from CliWrap
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    // System.Diagnostics.Process already resolves the full path by itself, but it naively assumes that the file
    // is an executable if the extension is omitted. On Windows, BAT and CMD files may also be valid targets.
    // In practice, it means that Process.Start("foo") will work if it's an EXE file, but will fail if it's a
    // BAT or CMD file, even if it's on the PATH. If the extension is specified, it will work in both cases.
    public static string GetOptimallyQualifiedTargetFilePath(string fileName)
    {
        // Currently, we only need this workaround for script files on Windows, so short-circuit
        // if we are on a different platform.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return fileName;
        }

        // Don't do anything for fully qualified paths or paths that already have an extension specified.
        // System.Diagnostics.Process knows how to handle those without our help.
        // Note that IsPathRooted(...) doesn't check if the path is absolute, as it also returns true for
        // strings like 'c:foo.txt' (which is relative to the current directory on drive C), but it's good
        // enough for our purposes and the alternative is only available on .NET Standard 2.1+.
        if (
            Path.IsPathRooted(fileName)
            || !string.IsNullOrWhiteSpace(Path.GetExtension(fileName))
        )
        {
            return fileName;
        }

        static IEnumerable<string> GetProbeDirectoryPaths()
        {
            // Implementation reference:
            // https://github.com/dotnet/runtime/blob/9a50493f9f1125fda5e2212b9d6718bc7cdbc5c0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Unix.cs#L686-L728
            // MIT License, .NET Foundation

            // Executable directory
            if (!string.IsNullOrWhiteSpace(EnvironmentEx.ProcessPath))
            {
                var processDirPath = Path.GetDirectoryName(EnvironmentEx.ProcessPath);
                if (!string.IsNullOrWhiteSpace(processDirPath))
                    yield return processDirPath;
            }

            // Working directory
            yield return Directory.GetCurrentDirectory();

            // Directories on the PATH
            if (Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) is { } paths)
            {
                foreach (var path in paths)
                    yield return path;
            }
        }

        return (
                from probeDirPath in GetProbeDirectoryPaths()
                where Directory.Exists(probeDirPath)
                select Path.Combine(probeDirPath, fileName) into baseFilePath
                from extension in new[] { "exe", "cmd", "bat" }
                select Path.ChangeExtension(baseFilePath, extension)
            ).FirstOrDefault(File.Exists) ?? fileName;
    }



    public static List<string> BuildCommand(
        ClaudeCodeOptions options,
        bool isStreaming,
        string prompt)
    {
        var cmd = new List<string> { "--output-format", "stream-json", "--verbose" };

        if (!string.IsNullOrEmpty(options.SystemPrompt))
            cmd.AddRange(new[] { "--system-prompt", options.SystemPrompt });

        if (!string.IsNullOrEmpty(options.AppendSystemPrompt))
            cmd.AddRange(new[] { "--append-system-prompt", options.AppendSystemPrompt });

        if (options.AllowedTools != null && options.AllowedTools.Count > 0)
            cmd.AddRange(new[] { "--allowedTools", string.Join(",", options.AllowedTools) });

        if (options.MaxTurns.HasValue)
            cmd.AddRange(new[] { "--max-turns", options.MaxTurns.Value.ToString() });

        if (options.DisallowedTools != null && options.DisallowedTools.Count > 0)
            cmd.AddRange(new[] { "--disallowedTools", string.Join(",", options.DisallowedTools) });

        if (!string.IsNullOrEmpty(options.Model))
            cmd.AddRange(new[] { "--model", options.Model });

        if (!string.IsNullOrEmpty(options.PermissionPromptToolName))
            cmd.AddRange(new[] { "--permission-prompt-tool", options.PermissionPromptToolName });

        if (options.PermissionMode != null)
            cmd.AddRange(new[] { "--permission-mode", options.PermissionMode.ToString() ?? "" });

        if (options.ContinueConversation)
            cmd.Add("--continue");

        if (!string.IsNullOrEmpty(options.Resume))
            cmd.AddRange(new[] { "--resume", options.Resume });

        if (!string.IsNullOrEmpty(options.Settings))
            cmd.AddRange(new[] { "--settings", options.Settings });

        if (options.AddDirs != null)
        {
            foreach (var dir in options.AddDirs)
                cmd.AddRange(new[] { "--add-dir", dir.ToString() });
        }

        //if (options.McpServers != null)
        //{
        //    if (options.McpServers is Dictionary<string, object> dict)
        //    {
        //        var json = JsonSerializer.Serialize(new { mcpServers = dict });
        //        cmd.AddRange(new[] { "--mcp-config", json });
        //    }
        //    else
        //    {
        //        cmd.AddRange(new[] { "--mcp-config", options.McpServers.ToString() });
        //    }
        //}

        if (options.ExtraArgs != null)
        {
            foreach (var kvp in options.ExtraArgs)
            {
                if (kvp.Value == null)
                    cmd.Add($"--{kvp.Key}");
                else
                    cmd.AddRange(new[] { $"--{kvp.Key}", kvp.Value.ToString() });
            }
        }

        if (isStreaming)
        {
            cmd.AddRange(new[] { "--input-format", "stream-json" });
        }
        else
        {
            cmd.AddRange(new[] { "--print", prompt });
        }

        return cmd;
    }

}
internal static class EnvironmentEx
{
    private static readonly Lazy<string?> ProcessPathLazy = new(() =>
    {
        using var process = Process.GetCurrentProcess();
        return process.MainModule?.FileName;
    });

    public static string? ProcessPath => ProcessPathLazy.Value;
}