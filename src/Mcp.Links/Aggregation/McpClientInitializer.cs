using System.Collections.Concurrent;
using Mcp.Links.Configuration;
using Microsoft.Extensions.Logging;

namespace Mcp.Links.Aggregation;

internal static class McpClientInitializer
{

    public static async Task<List<McpClientWrapper>> InitializeClientsAsync(
        Dictionary<string, McpServerConfig> mcpServers,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var clientWrappers = new ConcurrentBag<McpClientWrapper>();

        // 优化：预先过滤启用的服务器，减少不必要的并发任务
        var enabledServers = mcpServers
            .Where(kv => kv.Value.Enabled.GetValueOrDefault(true))
            .ToList();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            enabledServers,
            parallelOptions,
            async (serverConfig, ct) =>
            {
                var serverId = serverConfig.Key;
                var config = serverConfig.Value;

                var clientWrapper = new McpClientWrapper(serverId, config, loggerFactory);
                await clientWrapper.InitializeAsync().ConfigureAwait(false);
                clientWrappers.Add(clientWrapper);
            }
        ).ConfigureAwait(false);

        return clientWrappers.ToList();
    }
}
