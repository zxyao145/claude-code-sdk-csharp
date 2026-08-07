using ClaudeCodeSdk.MAF;

using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MafAgentMetadataTests
{
    [Fact]
    public void Name_DefaultAgent_ReturnsClaudeCode()
    {
        using var agent = new ClaudeCodeAIAgent();

        Assert.Equal("ClaudeCode", agent.Name);
    }
}
