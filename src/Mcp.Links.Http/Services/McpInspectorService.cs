using System.Diagnostics;
using System.Text.Json;
using System.Text.Encodings.Web;
using Mcp.Links.Aggregation;
using Mcp.Links.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Implementation of the MCP Inspector Service for testing and debugging MCP servers.
/// </summary>
public class McpInspectorService : IMcpInspectorService
{
    private readonly McpClientsFactory _mcpClientsFactory;
    private readonly IMcpServerService _mcpServerService;
    private readonly IOptionsMonitor<McpServerConfigOptions> _mcpServerConfigOptions;
    private readonly ILogger<McpInspectorService> _logger;

    public McpInspectorService(
        McpClientsFactory mcpClientsFactory,
        IMcpServerService mcpServerService,
        IOptionsMonitor<McpServerConfigOptions> mcpServerConfigOptions,
        ILogger<McpInspectorService> logger)
    {
        _mcpClientsFactory = mcpClientsFactory;
        _mcpServerService = mcpServerService;
        _mcpServerConfigOptions = mcpServerConfigOptions;
        _logger = logger;
    }

    public async Task<McpConnectionTestResult> TestConnectionAsync(string serverId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Testing connection to server '{ServerId}'", serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                return new McpConnectionTestResult
                {
                    IsConnected = false,
                    ErrorMessage = $"Client not found for server '{serverId}'",
                    ConnectionTime = stopwatch.Elapsed
                };
            }

            // Try to list tools to test connection
            var tools = await client.ListToolsAsync();
            
            stopwatch.Stop();
            
            return new McpConnectionTestResult
            {
                IsConnected = true,
                ConnectionTime = stopwatch.Elapsed,
                ServerVersion = "Connected",
                Capabilities = null
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to test connection to server '{ServerId}'", serverId);
            
            return new McpConnectionTestResult
            {
                IsConnected = false,
                ErrorMessage = ex.Message,
                ConnectionTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<McpInspectorTool[]> GetServerToolsAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Getting tools for server '{ServerId}'", serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var tools = await client.ListToolsAsync();
            
            return tools.Select(tool => new McpInspectorTool
            {
                Name = tool.ProtocolTool.Name,
                Description = tool.ProtocolTool.Description,
                InputSchema = tool.ProtocolTool.InputSchema
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tools for server '{ServerId}'", serverId);
            throw;
        }
    }

    public async Task<McpToolCallResult> CallToolAsync(string serverId, string toolName, Dictionary<string, object>? arguments = null)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Calling tool '{ToolName}' on server '{ServerId}'", toolName, serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var result = await client.CallToolAsync(toolName, arguments as IReadOnlyDictionary<string, object?>);
            
            stopwatch.Stop();
            
            return new McpToolCallResult
            {
                IsSuccess = true,
                Result = result,
                RawResponse = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to call tool '{ToolName}' on server '{ServerId}'", toolName, serverId);
            
            return new McpToolCallResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<McpInspectorResource[]> GetServerResourcesAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Getting resources for server '{ServerId}'", serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var resources = await client.ListResourcesAsync();
            
            return resources.Select(resource => new McpInspectorResource
            {
                Uri = resource.Uri,
                Name = resource.Name,
                Description = resource.Description,
                MimeType = resource.MimeType
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resources for server '{ServerId}'", serverId);
            throw;
        }
    }

    public async Task<McpResourceContent> ReadResourceAsync(string serverId, string resourceUri)
    {
        try
        {
            _logger.LogInformation("Reading resource '{ResourceUri}' from server '{ServerId}'", resourceUri, serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var resources = await client.ReadResourceAsync(resourceUri);
            var resource = resources.Contents?.FirstOrDefault();
            
            if (resource == null)
            {
                throw new InvalidOperationException($"Resource '{resourceUri}' not found");
            }

            var content = JsonSerializer.Serialize(resource, new JsonSerializerOptions { WriteIndented = true });
            
            return new McpResourceContent
            {
                Uri = resourceUri,
                Content = content,
                MimeType = resource.MimeType,
                Size = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read resource '{ResourceUri}' from server '{ServerId}'", resourceUri, serverId);
            throw;
        }
    }

    public async Task<McpInspectorPrompt[]> GetServerPromptsAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Getting prompts for server '{ServerId}'", serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var prompts = await client.ListPromptsAsync();
            
            return prompts.Select(prompt => new McpInspectorPrompt
            {
                Name = prompt.ProtocolPrompt.Name,
                Description = prompt.ProtocolPrompt.Description,
                Arguments = prompt.ProtocolPrompt.Arguments?.ToArray()
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prompts for server '{ServerId}'", serverId);
            throw;
        }
    }

    public async Task<McpPromptContent> GetPromptAsync(string serverId, string promptName, Dictionary<string, object>? arguments = null)
    {
        try
        {
            _logger.LogInformation("Getting prompt '{PromptName}' from server '{ServerId}'", promptName, serverId);
            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found for server '{serverId}'");
            }

            var prompt = await client.GetPromptAsync(promptName, arguments as IReadOnlyDictionary<string, object?>);
            
            return new McpPromptContent
            {
                Name = promptName,
                Messages = prompt.Messages?.ToArray() ?? Array.Empty<object>(),
                Description = prompt.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prompt '{PromptName}' from server '{ServerId}'", promptName, serverId);
            throw;
        }
    }

    public async Task<McpServerInfo> GetServerInfoAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Getting server info for '{ServerId}'", serverId);
            
            // Get basic server info from the service
            var serverInfo = await _mcpServerService.GetServerAsync(serverId);
            if (serverInfo == null)
            {
                throw new InvalidOperationException($"Server '{serverId}' not found");
            }

            return serverInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server info for '{ServerId}'", serverId);
            throw;
        }
    }

    public async Task<McpServerExport> ExportServerConfigAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Exporting configuration for server '{ServerId}'", serverId);
            
            var serverInfo = await _mcpServerService.GetServerAsync(serverId);
            if (serverInfo == null)
            {
                throw new InvalidOperationException($"Server '{serverId}' not found");
            }

            var currentOptions = _mcpServerConfigOptions.CurrentValue;
            if (!currentOptions.McpServers.TryGetValue(serverId, out var serverConfig))
            {
                throw new InvalidOperationException($"Server configuration not found for '{serverId}'");
            }

            // Create export configuration
            var exportConfig = new Dictionary<string, object>();
            
            if (serverConfig.Type == "stdio")
            {
                exportConfig["command"] = serverConfig.Command ?? "";
                if (serverConfig.Args?.Length > 0)
                {
                    exportConfig["args"] = serverConfig.Args;
                }
                if (serverConfig.Env?.Count > 0)
                {
                    exportConfig["env"] = serverConfig.Env;
                }
            }
            else
            {
                exportConfig["type"] = serverConfig.Type ?? "unknown";
                exportConfig["url"] = serverConfig.Url ?? "";
                if (serverConfig.Headers?.Count > 0)
                {
                    exportConfig["headers"] = serverConfig.Headers;
                }
            }

            return new McpServerExport
            {
                ServerId = serverId,
                Configuration = exportConfig,
                ConfigType = "server-entry"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration for server '{ServerId}'", serverId);
            throw;
        }
    }

    public async Task<string> ExportAllServersConfigAsync()
    {
        try
        {
            _logger.LogInformation("Exporting configuration for all servers");
            
            var mcpJsonContent = await _mcpServerService.GetMcpJsonContentAsync();
            return mcpJsonContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration for all servers");
            throw;
        }
    }
}
