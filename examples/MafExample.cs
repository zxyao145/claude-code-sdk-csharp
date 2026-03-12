using ClaudeCodeSdk.MAF;
using Microsoft.Agents.AI;
using System.Text.Json;

namespace ClaudeCodeSdk.Examples;

internal static class MafExample
{
    public static async Task Main(string[] args)
    {
        await BasicExample();
        Console.WriteLine();
        await MultiTurn();
        Console.WriteLine();
        await MultiStreamingTurn();
    }

    public static async Task BasicExample()
    {
        var options = new ClaudeCodeAIAgentOptions
        {
            EnvironmentVariables = EnvUtil.CreateEnv(),
        };

        Console.WriteLine("maf BasicExample turn 1");
        await using var agent = new ClaudeCodeAIAgent(options);
        var response = await agent.RunAsync("Tell me a joke about a pirate.");
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf BasicExample turn 1 end");


        Console.WriteLine("maf BasicExample turn 2");
        response = await agent.RunAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.");
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf BasicExample turn 2 end");


        Console.WriteLine("maf BasicExample end");
    }


    public static async Task MultiTurn()
    {
        var options = new ClaudeCodeAIAgentOptions
        {
            EnvironmentVariables = EnvUtil.CreateEnv(),
            ChatHistoryProvider = new InMemoryChatHistoryProvider()
        };

        Console.WriteLine("maf MultiTurn turn 1");
        await using var agent = new ClaudeCodeAIAgent(options);
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("Tell me a joke about a pirate.", session);
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf MultiTurn turn 1 end");


        Console.WriteLine("maf MultiTurn turn 2");
        response = await agent.RunAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.", session);
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf MultiTurn turn 2 end");


        var msg = await agent.SerializeSessionAsync(session);
        // {"sessionId":"<Guid>","stateBag":{}}
        Console.WriteLine($"session messages: {msg}");
        Console.WriteLine("maf MultiTurn end");
    }

    public static async Task MultiStreamingTurn()
    {
        var options = new ClaudeCodeAIAgentOptions
        {
            EnvironmentVariables = EnvUtil.CreateEnv(),
            ChatHistoryProvider = new InMemoryChatHistoryProvider()
        };

        Console.WriteLine("maf MultiStreamingTurn turn 1");
        await using var agent = new ClaudeCodeAIAgent(options);
        var session = await agent.CreateSessionAsync();

        var response = agent.RunStreamingAsync("Tell me a joke about a pirate.", session);
        await foreach (var update in response)
        {
            if (update != null)
            {
                Console.Write(update.Text);
            }
        }
        Console.WriteLine();
        Console.WriteLine("maf MultiStreamingTurn turn 1 end");


        Console.WriteLine("maf MultiStreamingTurn turn 2");
        response = agent.RunStreamingAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.", session);
        await foreach (var update in response)
        {
            if (update != null)
            {
                Console.Write(update.Text);
            }
        }
        Console.WriteLine();
        Console.WriteLine("maf MultiStreamingTurn turn 2 end");


        var msg = await agent.SerializeSessionAsync(session);
        // {"sessionId":"<Guid>","stateBag":{}}
        Console.WriteLine($"session messages: {msg}");
        var deserializeSession = await agent.DeserializeSessionAsync(msg) as ClaudeCodeAgentSession;
        var j = JsonSerializer.Serialize(deserializeSession);
        Console.WriteLine("maf MultiStreamingTurn end");
    }
}
