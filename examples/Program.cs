using ClaudeCodeSdk.Examples;

Console.WriteLine("Claude Code SDK for .NET - Examples");
Console.WriteLine("===================================");

try
{

    // Run quick start examples
    await QuickStartExamples.Main(args);
    
    // Run streaming examples  
    await StreamingExamples.Main(args);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
    Environment.Exit(1);
}