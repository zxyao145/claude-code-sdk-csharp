using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;

namespace ClaudeCodeSdk.Examples;

/// <summary>
/// Streaming examples for Claude Code SDK.
/// </summary>
public static class StreamingExamples
{
    public static async Task Main(string[] args)
    {
        //await StreamingModeExample();
        await InteractiveStreamingExample();
    }

    /// <summary>
    /// Example of streaming mode with multiple prompts.
    /// </summary>
    public static async Task StreamingModeExample()
    {
        Console.WriteLine("=== Streaming Mode Example ===");

        var options = new ClaudeCodeOptions();
        options.EnvironmentVariables = EnvUtil.CreateEnv();

        // Create async enumerable of messages
        var messages = CreateMessageStream();

        await foreach (var message in ClaudeQuery.QueryAsync(messages))
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
                Console.WriteLine($"Session completed. Turns: {resultMessage.NumTurns}");
                if (resultMessage.TotalCostUsd.HasValue)
                {
                    Console.WriteLine($"Cost: ${resultMessage.TotalCostUsd:F4}");
                }
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Example of interactive streaming with user input.
    /// </summary>
    public static async Task InteractiveStreamingExample()
    {
        Console.WriteLine("=== Interactive Streaming Example ===");
        Console.WriteLine("Type 'exit' to quit");

        var options = new ClaudeCodeOptions
        {
            //SystemPrompt = "You are a helpful assistant. Keep your responses concise.",
            MaxTurns = 10,
            WorkingDirectory = "D:\\source\\repos\\claude-code-sdk-csharp"
        };
        options.EnvironmentVariables = EnvUtil.CreateEnv();

        await using var client = new ClaudeSdkClient(options);
        await client.ConnectAsync();

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                break;

            await client.QueryAsync(input);

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
                        else if (block is ToolUseBlock toolUse)
                        {
                            Console.WriteLine($"[Using tool: {toolUse.Name}]");
                        }
                    }
                }
                else if (message is ResultMessage)
                {
                    break;
                }
            }
        }

        Console.WriteLine("Goodbye!");
    }

    private static async IAsyncEnumerable<Dictionary<string, object>> CreateMessageStream()
    {
        yield return new Dictionary<string, object>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = "Hello, how are you?"
            },
            ["parent_tool_use_id"] = null!,
            ["session_id"] = "streaming-example"
        };

        // Simulate some delay
        await Task.Delay(100);

        yield return new Dictionary<string, object>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = "Can you help me with a math problem?"
            },
            ["parent_tool_use_id"] = null!,
            ["session_id"] = "streaming-example"
        };
    }
}