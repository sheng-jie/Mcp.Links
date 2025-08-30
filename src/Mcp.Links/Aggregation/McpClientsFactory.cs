using Mcp.Links.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using System.Collections.Concurrent;

namespace Mcp.Links.Aggregation;

public sealed class McpClientsFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientsFactory> _logger;

    private readonly ConcurrentDictionary<string, McpClientWrapper> _clientWrappers = new();
    private readonly object _initializationLock = new();
    private readonly SemaphoreSlim InitializeSemaphore = new(1, 1);
    private volatile bool _isInitialized = false;

    private readonly IOptionsMonitor<McpServerConfigOptions> _mcpServerConfigOptions;

    public McpClientsFactory(IOptionsMonitor<McpServerConfigOptions> options, ILoggerFactory loggerFactory)
    {
        _mcpServerConfigOptions = options;

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpClientsFactory>();
    }
    public async Task<List<McpClientWrapper>> GetOrCreateClientsAsync(CancellationToken cancellationToken = default)
    {
        var currentOptions = _mcpServerConfigOptions.CurrentValue;
        if (currentOptions.McpServers is null || !currentOptions.McpServers.Any())
        {
            throw new InvalidOperationException("No MCP servers configured.");
        }
        
        if (_isInitialized)
        {
            // 如果已经初始化过客户端，直接返回现有的客户端列表
            return _clientWrappers.Values.ToList();
        }

        // Use a more robust approach to prevent concurrent initialization
        await InitializeSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the semaphore
            if (_isInitialized)
            {
                return _clientWrappers.Values.ToList();
            }

            // Initialize clients
            var clients = await McpClientInitializer.InitializeClientsAsync(
                currentOptions.McpServers, _loggerFactory, cancellationToken);

            // Add clients to the collection
            foreach (var client in clients)
            {
                _clientWrappers.TryAdd(client.Name, client);
            }
            
            _isInitialized = true;
            return _clientWrappers.Values.ToList();
        }
        finally
        {
            InitializeSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets a specific MCP client by server ID.
    /// </summary>
    /// <param name="serverId">The server ID to get the client for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The MCP client if found and initialized, null otherwise.</returns>
    public async Task<IMcpClient?> GetMcpClientAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            return null;

        var clientWrappers = await GetOrCreateClientsAsync(cancellationToken);
        if (_clientWrappers.TryGetValue(serverId, out var clientWrapper))
        {
            return clientWrapper.McpClient;
        }

        return null;
    }

    /// <summary>
    /// Adds a new MCP client for the specified server.
    /// </summary>
    /// <param name="serverId">The server ID to add a client for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddClientAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            throw new ArgumentException("Server ID cannot be null or empty", nameof(serverId));

        var currentOptions = _mcpServerConfigOptions.CurrentValue;
        if (!currentOptions.McpServers.TryGetValue(serverId, out var serverConfig))
        {
            throw new ArgumentException($"Server '{serverId}' not found in configuration");
        }

        // Check if server is enabled
        if (!serverConfig.Enabled.GetValueOrDefault(true))
        {
            _logger.LogInformation("Skipping disabled server '{ServerId}'", serverId);
            return;
        }

        lock (_initializationLock)
        {
            // Check if client already exists
            if (_clientWrappers.ContainsKey(serverId))
            {
                _logger.LogWarning("Client for server '{ServerId}' already exists", serverId);
                return;
            }
        }

        try
        {
            _logger.LogInformation("Adding MCP client for server '{ServerId}'", serverId);
            
            var clientWrapper = new McpClientWrapper(serverId, serverConfig, _loggerFactory);
            await clientWrapper.InitializeAsync();

            lock (_initializationLock)
            {
                _clientWrappers.TryAdd(serverId, clientWrapper);
            }

            _logger.LogInformation("Successfully added MCP client for server '{ServerId}'", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP client for server '{ServerId}'", serverId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing MCP client for the specified server.
    /// </summary>
    /// <param name="serverId">The server ID to update the client for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateClientAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            throw new ArgumentException("Server ID cannot be null or empty", nameof(serverId));

        var currentOptions = _mcpServerConfigOptions.CurrentValue;
        if (!currentOptions.McpServers.TryGetValue(serverId, out var serverConfig))
        {
            throw new ArgumentException($"Server '{serverId}' not found in configuration");
        }

        _logger.LogInformation("Updating MCP client for server '{ServerId}'", serverId);

        // Remove the existing client first
        await RemoveClientAsync(serverId);

        // Add the new client if the server is enabled
        if (serverConfig.Enabled.GetValueOrDefault(true))
        {
            await AddClientAsync(serverId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Server '{ServerId}' is disabled, client not recreated", serverId);
        }
    }

    /// <summary>
    /// Removes an MCP client for the specified server.
    /// </summary>
    /// <param name="serverId">The server ID to remove the client for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveClientAsync(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            throw new ArgumentException("Server ID cannot be null or empty", nameof(serverId));

        if (_clientWrappers.TryRemove(serverId, out var clientToRemove))
        {
            _logger.LogInformation("Removing MCP client for server '{ServerId}'", serverId);
            DisposeClient(clientToRemove);
            _logger.LogInformation("Successfully removed MCP client for server '{ServerId}'", serverId);
        }
        else
        {
            _logger.LogWarning("No MCP client found for server '{ServerId}' to remove", serverId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Reinitializes all MCP clients with the latest configuration.
    /// This method should be called when the MCP server configuration changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReinitializeClientsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reinitializing MCP clients due to configuration change...");

        lock (_initializationLock)
        {
            // Dispose existing clients first
            DisposeExistingClients();

            // Clear the collection and reset initialization flag
            _clientWrappers.Clear();
            _isInitialized = false;
        }

        // Reinitialize with the new configuration
        await GetOrCreateClientsAsync(cancellationToken);
        
        _logger.LogInformation("MCP clients reinitialized successfully.");
    }

    /// <summary>
    /// Disposes all existing MCP clients to free up resources.
    /// </summary>
    private void DisposeExistingClients()
    {
        foreach (var kvp in _clientWrappers)
        {
            DisposeClient(kvp.Value);
        }
    }

    /// <summary>
    /// Disposes a single MCP client to free up resources.
    /// </summary>
    /// <param name="clientWrapper">The client wrapper to dispose.</param>
    private void DisposeClient(McpClientWrapper clientWrapper)
    {
        try
        {
            // Dispose the client if it implements IDisposable
            if (clientWrapper.McpClient is IDisposable disposableClient)
            {
                disposableClient.Dispose();
            }
            
            // Try to dispose the wrapper itself if it implements IDisposable
            if (clientWrapper is IDisposable disposableWrapper)
            {
                disposableWrapper.Dispose();
            }
            
            _logger.LogDebug("Disposed MCP client '{ClientName}'", clientWrapper.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing MCP client '{ClientName}'", clientWrapper.Name);
        }
    }

}
