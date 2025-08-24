using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;
using static System.Net.WebRequestMethods;

namespace ClaudeCodeSdk.Examples;

/// <summary>
/// Quick start examples for Claude Code SDK.
/// </summary>
public static class QuickStartExamples
{
    public static async Task Main(string[] args)
    {
        await BasicExample();
        await WithOptionsExample();
        await WithToolsExample();
        await InteractiveClientExample();
    }

    private static Dictionary<string, string?> CreateEnv()
    {
        return new Dictionary<string, string?>();
    }

    /// <summary>
    /// Basic example - simple question.
    /// </summary>
    public static async Task BasicExample()
    {
        Console.WriteLine("=== Basic Example ===");
        ClaudeCodeOptions options = new ClaudeCodeOptions();
        options.EnvironmentVariables = CreateEnv();

        await foreach (var message in ClaudeQuery.QueryAsync("What is 2 + 2?", options))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Example with custom options.
    /// </summary>
    public static async Task WithOptionsExample()
    {
        Console.WriteLine("=== With Options Example ===");

        var options = new ClaudeCodeOptions
        {
            SystemPrompt = "You are a helpful assistant that explains things simply.",
            MaxTurns = 1
        };
        options.EnvironmentVariables = CreateEnv();

        await foreach (var message in ClaudeQuery.QueryAsync(
            "Explain what C# is in one sentence.", 
            options))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Example using tools.
    /// </summary>
    public static async Task WithToolsExample()
    {
        Console.WriteLine("=== With Tools Example ===");

        var options = new ClaudeCodeOptions
        {
            AllowedTools = new[] { "Read", "Write" },
            SystemPrompt = "You are a helpful file assistant."
        };
        options.EnvironmentVariables = CreateEnv();

        await foreach (var message in ClaudeQuery.QueryAsync(
            "Create a file called hello.txt with 'Hello, World!' in it",
            options))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
            else if (message is ResultMessage resultMessage && resultMessage.TotalCostUsd > 0)
            {
                Console.WriteLine($"\nCost: ${resultMessage.TotalCostUsd:F4}");
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Example using interactive client.
    /// </summary>
    public static async Task InteractiveClientExample()
    {
        Console.WriteLine("=== Interactive Client Example ===");

        await using var client = new ClaudeSDKClient();
        
        // Connect and send initial message
        await client.ConnectAsync(environmentVariables: CreateEnv());
        await client.QueryAsync("Let's solve a math problem step by step");

        // Wait for ready response
        await foreach (var message in client.ReceiveResponseAsync())
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                        
                        // Check if Claude is ready for the next step
                        if (textBlock.Text.Contains("ready", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }
            }
            
            if (message is ResultMessage)
                break;
        }

        // Send follow-up question
        await client.QueryAsync("What's 15% of 80?");

        // Receive final response
        await foreach (var message in client.ReceiveResponseAsync())
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
            else if (message is ResultMessage resultMessage)
            {
                if (resultMessage.TotalCostUsd.HasValue)
                {
                    Console.WriteLine($"\nTotal Cost: ${resultMessage.TotalCostUsd:F4}");
                }
                break;
            }
        }

        Console.WriteLine();
    }
}