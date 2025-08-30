using System;
using System.Collections.Concurrent;
using Mcp.Links.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mcp.Links.Aggregation;

[Obsolete("This class is obsolete and will be removed in a future version.")]
public class AggregatedMcpServerFactory
{
    private readonly IEnumerable<McpClientWrapper> _clientWrappers;

    public AggregatedMcpServerFactory(IEnumerable<McpClientWrapper> clientWrappers)
    {
        _clientWrappers = clientWrappers;
    }

    public IMcpServer CreateAggregatedServer()
    {
        McpServerOptions options = new()
        {
            ServerInfo = new Implementation
            {
                Name = "Aggregated MCP Server",
                Version = "1.0.0",
                Title = "An aggregated MCP server that combines multiple client functionalities.",
            },
            ProtocolVersion = "2025-06-18",
            Capabilities = new ServerCapabilities
            {
                Tools = ConfigureTools(),
                Resources = ConfigureResources(),
                Prompts = ConfigurePrompts(),
                // Logging = ConfigureLogging(),
                // Completions = ConfigureCompletions(),
            },
            ServerInstructions = "This server aggregates multiple MCP clients. Use the tools provided to interact with the underlying clients.",
        };

        var stdioTransport = new StdioServerTransport("AggregatedMcpServer");
        IMcpServer server = McpServerFactory.Create(stdioTransport, options);

        return server;
    }

    private CompletionsCapability ConfigureCompletions()
    {
        throw new NotImplementedException();
    }

    private LoggingCapability ConfigureLogging()
    {

        return new LoggingCapability
        {
            SetLoggingLevelHandler = async (request, cancellationToken) =>
            {
                if (request.Params?.Level is null)
                {
                    throw new McpException("Missing required argument 'level'", McpErrorCode.InvalidParams);
                }

                // _minimumLoggingLevel = request.Params.Level;

                return new EmptyResult();
            }
        };
    }

    private PromptsCapability ConfigurePrompts()
    {

        return new PromptsCapability
        {
            ListPromptsHandler = async (request, cancellation) =>
            {
                var prompts = new List<Prompt>();
                foreach (var clientWrapper in _clientWrappers)
                {
                    var clientPrompts = await clientWrapper.McpClient.ListPromptsAsync(cancellationToken: cancellation);
                    prompts.AddRange(clientPrompts.Select(p => p.ProtocolPrompt));
                }

                return new ListPromptsResult
                {
                    Prompts = prompts
                };
            }
        };
    }

    private ResourcesCapability ConfigureResources()
    {
        return new ResourcesCapability
        {
            ListResourcesHandler = async (request, cancellation) =>
            {
                var resources = new List<Resource>();
                foreach (var clientWrapper in _clientWrappers)
                {
                    var clientResources = await clientWrapper.McpClient.ListResourcesAsync(cancellationToken: cancellation);
                    resources.AddRange(clientResources.Select(r => r.ProtocolResource));
                }

                return new ListResourcesResult
                {
                    Resources = resources
                };
            }
        };
    }

    private ToolsCapability ConfigureTools()
    {
        return new ToolsCapability
        {
            ListToolsHandler = ListAggregatedToolsAsync,
            CallToolHandler = CallAggregatedToolAsync
        };
    }

    private async ValueTask<ListToolsResult> ListAggregatedToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellation)
    {
        var toolLists = await Task.WhenAll(
            _clientWrappers.Select(async clientWrapper =>
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

    private async ValueTask<CallToolResult> CallAggregatedToolAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellation)
    {
        var splitToolNames = request.Params?.Name.Split('.');
        if (splitToolNames == null || splitToolNames.Length != 2)
        {
            throw new ArgumentException("Tool name must be in the format 'ClientName.ToolName'.", nameof(request));
        }

        var targetClientName = splitToolNames[0];
        var toolName = splitToolNames[1];
        var clientWrapper = _clientWrappers.FirstOrDefault(cw => cw.Name == targetClientName);

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
