using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClaudeCodeSdk.Examples;

internal static class MafExample
{
    public static async Task Main(string[] args)
    {
        await BasicExample();
        await MultiTurn();
    }

    public static async Task BasicExample()
    {
        var options = new ClaudeCodeAIAgentOptions();
        options.EnvironmentVariables = EnvUtil.CreateEnv();


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
        var options = new ClaudeCodeAIAgentOptions();
        options.EnvironmentVariables = EnvUtil.CreateEnv();


        Console.WriteLine("maf MultiTurn turn 1");
        await using var agent = new ClaudeCodeAIAgent(options);
        var thread = agent.GetNewThread();

        var response = await agent.RunAsync("Tell me a joke about a pirate.", thread);
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf BasicExample turn 1 end");


        Console.WriteLine("maf MultiTurn turn 2");
        response = await agent.RunAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.", thread);
        foreach (var item in response.Messages)
        {
            Console.WriteLine("response message item: " + item);
        }
        Console.WriteLine("maf MultiTurn turn 2 end");


        Console.WriteLine("maf MultiTurn end");
    }
}
