using System.Collections.Concurrent;
using Mcp.Links.Aggregation;
using Mcp.Links.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


[Obsolete("This class is obsolete and will be removed in a future version.")]
internal sealed class AggregatedMcpServerHostedService(McpClientsFactory mcpClientsFactory,
    ILoggerFactory loggerFactory,
    IHostApplicationLifetime? lifetime = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var logger = loggerFactory.CreateLogger<AggregatedMcpServerHostedService>();

        try
        {
            logger?.LogInformation("Starting Aggregated MCP Server...");

            var clientWrappers = await mcpClientsFactory.GetOrCreateClientsAsync(stoppingToken).ConfigureAwait(false);

            var factory = new AggregatedMcpServerFactory(clientWrappers);
            var server = factory.CreateAggregatedServer();

            logger?.LogInformation("Aggregated MCP Server started successfully.");
            await server.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to start Aggregated MCP Server.");
            throw; // Re-throw the exception to ensure the host stops
        }
        finally
        {
            lifetime?.StopApplication();
        }
    }
}