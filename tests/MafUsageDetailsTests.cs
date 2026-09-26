using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MafUsageDetailsTests
{
    // Claude Code caches almost the whole prompt, so `input_tokens` is only the uncached
    // remainder. Measured on a real pilot session: input_tokens 8, the rest of the prompt
    // arrived as cache reads/creations.
    private static ResultMessage Result(Usage usage) =>
        new()
        {
            Subtype = "success",
            DurationMs = 1,
            DurationApiMs = 1,
            IsError = false,
            NumTurns = 1,
            SessionId = "00000000-0000-0000-0000-000000000000",
            Usage = usage,
        };

    [Fact]
    public void ToUsageDetails_InputTokenCount_IncludesCachedInput()
    {
        var usage = new Usage
        {
            InputTokens = 8,
            CacheCreationInputTokens = 1_200,
            CacheReadInputTokens = 30_000,
            OutputTokens = 500,
        };

        var details = Result(usage).ToUsageDetails()!;

        // Microsoft.Extensions.AI: "Cached input tokens should be counted as part of InputTokenCount."
        Assert.Equal(31_208, details.InputTokenCount);
        Assert.Equal(500, details.OutputTokenCount);
        Assert.Equal(31_708, details.TotalTokenCount);
    }

    [Fact]
    public void ToUsageDetails_CachedInputTokenCount_IsCacheReads()
    {
        var usage = new Usage
        {
            InputTokens = 8,
            CacheCreationInputTokens = 1_200,
            CacheReadInputTokens = 30_000,
            OutputTokens = 500,
        };

        var details = Result(usage).ToUsageDetails()!;

        // Microsoft.Extensions.AI: CachedInputTokenCount is "the number of input tokens that were read from a cache".
        Assert.Equal(30_000, details.CachedInputTokenCount);
        Assert.Equal(30_000, details.AdditionalCounts!["cacheReadInputTokens"]);
        Assert.Equal(1_200, details.AdditionalCounts!["cacheCreationInputTokens"]);
    }
}
