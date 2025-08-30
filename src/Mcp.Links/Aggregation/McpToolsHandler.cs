using Mcp.Links.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mcp.Links.Aggregation;

public sealed class McpToolsHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    private readonly IOptionsMonitor<McpClientConfigOptions> _clientConfigOptions;
    private readonly McpClientsFactory _mcpClientsFactory;
    private readonly ILogger<McpToolsHandler> _logger;

    public McpToolsHandler(IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<McpClientConfigOptions> clientConfigOptions,
        McpClientsFactory mcpClientsFactory, ILogger<McpToolsHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientConfigOptions = clientConfigOptions;
        _mcpClientsFactory = mcpClientsFactory;
        _logger = logger;
    }


    /// <summary>
    /// Retrieves a list of <see cref="McpClientWrapper"/> instances associated with the specified application ID.
    /// </summary>
    /// <param name="appId">The application ID to filter clients by.</param>
    /// <param name="cancellation">A cancellation token for the asynchronous operation.</param>
    /// <returns>A list of <see cref="McpClientWrapper"/> objects matching the application ID.</returns>
    private async Task<List<McpClientWrapper>> GetClientWrappersByAppId(string appId, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(appId))
            return new List<McpClientWrapper>();

        var clientWrappersTask = _mcpClientsFactory.GetOrCreateClientsAsync(cancellation);
        var clientInfo = _clientConfigOptions.CurrentValue.McpClients?
            .FirstOrDefault(c => string.Equals(c.AppId, appId, StringComparison.OrdinalIgnoreCase));

        if (clientInfo?.McpServerIds == null || clientInfo.McpServerIds.Length == 0)
            return new List<McpClientWrapper>();

        var serverIdSet = new HashSet<string>(clientInfo.McpServerIds, StringComparer.OrdinalIgnoreCase);
        var clientWrappers = await clientWrappersTask.ConfigureAwait(false);

        return clientWrappers
            .Where(cw => serverIdSet.Contains(cw.Name))
            .ToList();
    }

    public async ValueTask<ListToolsResult> ListAggregatedToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellation)
    {
        var appId = _httpContextAccessor.HttpContext?.Request.Headers["X-AppId"].FirstOrDefault();

        var clientWrappers = await GetClientWrappersByAppId(appId ?? string.Empty, cancellation).ConfigureAwait(false);
        var toolLists = await Task.WhenAll(
            clientWrappers.Select(async clientWrapper =>
            {
                var clientTools = await clientWrapper.McpClient.ListToolsAsync(cancellationToken: cancellation);
                return clientTools.Select(tool =>
                {
                    var protocolTool = tool.ProtocolTool;
                    protocolTool.Name = $"{clientWrapper.Name}.{protocolTool.Name}";
                    return protocolTool;
                });
            })
        ).ConfigureAwait(false);

        return new ListToolsResult
        {
            Tools = toolLists.SelectMany(t => t).ToList()
        };
    }

    public async ValueTask<CallToolResult> CallAggregatedToolAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellation)
    {
        var clientWrappers = await _mcpClientsFactory.GetOrCreateClientsAsync(cancellation).ConfigureAwait(false);

        var splitToolNames = request.Params?.Name.Split('.');
        if (splitToolNames == null || splitToolNames.Length != 2)
        {
            throw new ArgumentException("Tool name must be in the format 'ClientName.ToolName'.", nameof(request));
        }

        var targetClientName = splitToolNames[0];
        var toolName = splitToolNames[1];
        var clientWrapper = clientWrappers.FirstOrDefault(cw => cw.Name == targetClientName);

        if (clientWrapper == null)
        {
            throw new InvalidOperationException($"No client found for tool '{toolName}'");
        }

        var convertedArguments = request.Params?.Arguments?
            .ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);

        return await clientWrapper.McpClient.CallToolAsync(
            toolName: toolName,
            arguments: convertedArguments,
            cancellationToken: cancellation);
    }
}
