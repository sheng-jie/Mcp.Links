using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mcp.Links.Aggregation;

namespace Mcp.Links.Http;

public class McpClientStartupService : IHostedService
{
    private readonly McpClientsFactory _mcpClientsFactory;
    private readonly ILogger<McpClientStartupService> _logger;

    public McpClientStartupService(McpClientsFactory mcpClientsFactory, ILogger<McpClientStartupService> logger)
    {
        _mcpClientsFactory = mcpClientsFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing MCP clients on startup...");
            await _mcpClientsFactory.GetOrCreateClientsAsync(cancellationToken);
            _logger.LogInformation("MCP clients initialized successfully on startup.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MCP clients on startup");
            // You can choose to throw here if client initialization is critical
            // throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MCP client startup service...");
        return Task.CompletedTask;
    }
}