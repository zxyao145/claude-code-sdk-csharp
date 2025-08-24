using ClaudeCodeSdk.Types;

namespace ClaudeCodeSdk.Internal.Transport;

/// <summary>
/// Interface for transport implementations.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Connect to Claude Code.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a request to Claude Code.
    /// </summary>
    Task SendRequestAsync(IEnumerable<Dictionary<string, object>> messages, Dictionary<string, object> metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive messages from Claude Code.
    /// </summary>
    IAsyncEnumerable<Dictionary<string, object>> ReceiveMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send interrupt signal.
    /// </summary>
    Task InterruptAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from Claude Code.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}