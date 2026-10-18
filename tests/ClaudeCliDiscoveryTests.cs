using ClaudeCodeSdk.Exceptions;
using ClaudeCodeSdk.Types;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class ClaudeCliDiscoveryTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("claude-sdk-tests-");

    [Fact]
    public void FindClaudeCli_OnPath_PrefersFirstPathOverFallback()
    {
        var firstDir = _root.CreateSubdirectory("first").FullName;
        var secondDir = _root.CreateSubdirectory("second").FullName;
        var firstCli = Path.Combine(firstDir, "claude");
        var secondCli = Path.Combine(secondDir, "claude");
        var fallbackCli = Path.Combine(_root.FullName, "claude");
        File.WriteAllText(firstCli, "");
        File.WriteAllText(secondCli, "");
        File.WriteAllText(fallbackCli, "");

        var result = ClaudeProcess.FindClaudeCli(
            string.Join(Path.PathSeparator, firstDir, secondDir),
            [fallbackCli]
        );

        Assert.Equal(firstCli, result);
    }

    [Fact]
    public void FindClaudeCli_InFallbackDirectory_ReturnsPathWithoutNodeJs()
    {
        var pathDir = _root.CreateSubdirectory("path").FullName;
        var cliPath = Path.Combine(_root.CreateSubdirectory("fallback").FullName, "claude");
        File.WriteAllText(cliPath, "");

        var result = ClaudeProcess.FindClaudeCli(pathDir, [cliPath]);

        Assert.Equal(cliPath, result);
    }

    [Fact]
    public void DefaultFallbackLocations_IncludeHomebrewBin()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "Homebrew install locations only apply to Unix."
        );

        Assert.Contains("/opt/homebrew/bin/claude", ClaudeProcess.DefaultFallbackLocations());
    }

    [Fact]
    public void FindClaudeCli_NotFound_ReportsSearchPathsWithoutNodeJsRequirement()
    {
        var pathEnv = string.Join(
            Path.PathSeparator,
            _root.CreateSubdirectory("first").FullName,
            _root.CreateSubdirectory("second").FullName
        );
        var missingCli = Path.Combine(_root.FullName, "claude");
        var relativeCli = Path.GetRelativePath(
            Environment.CurrentDirectory,
            Path.Combine(_root.FullName, "missing", "claude")
        );

        var exception = Assert.Throws<CLINotFoundException>(() =>
            ClaudeProcess.FindClaudeCli(pathEnv, [missingCli, relativeCli])
        );

        Assert.Contains("Claude Code CLI not found", exception.Message);
        Assert.Contains($"PATH ({pathEnv})", exception.Message);
        Assert.Contains(missingCli, exception.Message);
        Assert.Contains(Path.GetFullPath(relativeCli), exception.Message);
        Assert.Contains("add its directory to PATH", exception.Message);
        Assert.DoesNotContain("Node.js", exception.Message);
    }

    [Fact]
    public void FindClaudeCli_OnWindows_FindsExeOnPathAndInFallbackDirectory()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows executable suffix lookup.");

        var cliPath = Path.Combine(_root.FullName, "claude");
        File.WriteAllText(cliPath + ".exe", "");

        Assert.Equal(cliPath + ".exe", ClaudeProcess.FindClaudeCli(_root.FullName, []));
        Assert.Equal(
            cliPath + ".exe",
            ClaudeProcess.FindClaudeCli(_root.CreateSubdirectory("empty").FullName, [cliPath])
        );
    }

    [Fact]
    public async Task Constructor_WithCliPath_SkipsDiscovery()
    {
        await using var process = new ClaudeProcess(
            new ClaudeCodeOptions(),
            cliPath: Path.Combine(_root.FullName, "nonexistent-claude")
        );
    }

    public void Dispose() => _root.Delete(recursive: true);
}
