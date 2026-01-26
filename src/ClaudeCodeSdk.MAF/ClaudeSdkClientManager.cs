using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.Logging;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Manages the lifecycle of ClaudeSdkClient instances for different sessions.
/// Automatically disposes old clients when switching to a different session.
/// Thread-safe with proper async resource management.
/// </summary>
internal sealed class ClaudeSdkClientManager : IAsyncDisposable
{
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private ClaudeSdkClient? _client;
    private Guid? _currentSessionId;
    private bool _disposed;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the ClaudeSdkClientManager.
    /// </summary>
    /// <param name="options">The ClaudeCode options for client creation.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ClaudeSdkClientManager(ClaudeCodeOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask<ClaudeSdkClient> GetClientAsync(ClaudeCodeAgentThread claudeCodeAgent, CancellationToken cancellationToken = default)
    {
        return await GetClientAsync(claudeCodeAgent.SessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets or creates a ClaudeSdkClient for the specified session ID.
    /// If the session ID differs from the current one, the old client is disposed and a new one is created.
    /// </summary>
    /// <param name="sessionId">The session ID to associate with the client.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A connected ClaudeSdkClient instance for the specified session.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the manager has been disposed.</exception>
    public async ValueTask<ClaudeSdkClient> GetClientAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check if we need to create a new client for this session
            if (_currentSessionId != sessionId || _client == null)
            {
                // Dispose old client if it exists
                if (_client != null)
                {
                    await _client.DisposeAsync();
                    _client = null;
                }

                // Create new client for this session
                _currentSessionId = sessionId;
                _client = new ClaudeSdkClient(_options, _logger);
                await _client.ConnectAsync(cancellationToken: cancellationToken);
            }

            return _client;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets the current session ID if a client is active, or null otherwise.
    /// </summary>
    public Guid? CurrentSessionId => _currentSessionId;

    /// <summary>
    /// Gets whether a client is currently active and connected.
    /// </summary>
    public bool IsConnected => _client != null && _currentSessionId.HasValue;

    /// <summary>
    /// Disposes the manager and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _lock.WaitAsync();
        try
        {
            if (_client != null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            _currentSessionId = null;
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
            _disposed = true;
        }
    }
}
