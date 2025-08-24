using ClaudeCodeSdk.Internal.Transport;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ClaudeCodeSdk.Internal;

/// <summary>
/// Internal client for processing queries.
/// </summary>
internal class InternalClient
{
    private readonly ILogger? _logger;

    public InternalClient(ILogger? logger = null)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<IMessage> ProcessQueryAsync(
        object prompt,
        ClaudeCodeOptions options,
        ITransport? transport = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Use provided transport or create default subprocess transport
        transport ??= new SubprocessCliTransport(prompt, options, null, _logger);

        await using (transport)
        {
            await transport.ConnectAsync(cancellationToken);

            // Receive and parse all response messages
            await foreach (var data in transport.ReceiveMessagesAsync(cancellationToken))
            {
                yield return MessageParser.ParseMessage(data, _logger);
            }
        }
    }
}