using Mcp.Links.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Mcp.Links.Aggregation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Client;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service implementation for managing MCP server configurations.
/// </summary>
public class McpServerService : IMcpServerService
{
    private readonly IOptionsMonitor<McpServerConfigOptions> _mcpOptions;
    private readonly ILogger<McpServerService> _logger;
    private readonly McpClientsFactory _mcpClientsFactory;

    public McpServerService(
        IOptionsMonitor<McpServerConfigOptions> mcpOptions,
        ILogger<McpServerService> logger,
        McpClientsFactory mcpClientsFactory)
    {
        _mcpOptions = mcpOptions;
        _logger = logger;
        _mcpClientsFactory = mcpClientsFactory;
    }

    public Task<McpServerInfo[]> GetAllServersAsync()
    {
        try
        {
            var options = _mcpOptions.CurrentValue;
            
            var serverData = options.McpServers.Select(kvp => new McpServerInfo
            {
                ServerId = kvp.Key,
                Type = kvp.Value.Type ?? "stdio",
                Enabled = kvp.Value.Enabled ?? true,
                Command = kvp.Value.Command,
                Url = kvp.Value.Url,
                Args = kvp.Value.Args,
                EnvCount = kvp.Value.Env?.Count ?? 0,
                Environment = kvp.Value.Env,
                HeadersCount = kvp.Value.Headers?.Count ?? 0,
                Headers = kvp.Value.Headers,
                IsValid = kvp.Value.IsValid(),
                ValidationErrors = kvp.Value.GetValidationErrors().ToArray(),
                Configuration = GetConfigurationSummary(kvp.Value)
            }).ToArray();

            return Task.FromResult(serverData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MCP servers");
            throw;
        }
    }

    public Task<McpServerInfo?> GetServerAsync(string serverId)
    {
        try
        {
            var options = _mcpOptions.CurrentValue;
            
            if (!options.McpServers.TryGetValue(serverId, out var serverConfig))
            {
                return Task.FromResult<McpServerInfo?>(null);
            }

            var serverInfo = new McpServerInfo
            {
                ServerId = serverId,
                Type = serverConfig.Type ?? "stdio",
                Enabled = serverConfig.Enabled ?? true,
                Command = serverConfig.Command,
                Url = serverConfig.Url,
                Args = serverConfig.Args,
                EnvCount = serverConfig.Env?.Count ?? 0,
                Environment = serverConfig.Env,
                HeadersCount = serverConfig.Headers?.Count ?? 0,
                Headers = serverConfig.Headers,
                IsValid = serverConfig.IsValid(),
                ValidationErrors = serverConfig.GetValidationErrors().ToArray(),
                Configuration = GetConfigurationSummary(serverConfig)
            };

            return Task.FromResult<McpServerInfo?>(serverInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MCP server {ServerId}", serverId);
            throw;
        }
    }

    public async Task CreateServerAsync(string serverId, McpServerConfig serverConfig)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID is required", nameof(serverId));

            if (serverConfig == null)
                throw new ArgumentNullException(nameof(serverConfig));

            // Check if server ID already exists
            if (await ServerExistsAsync(serverId))
                throw new ArgumentException($"Server with ID '{serverId}' already exists");

            // Validate the configuration
            if (!ValidateServerConfig(serverConfig))
            {
                var errors = GetValidationErrors(serverConfig);
                throw new ArgumentException($"Invalid configuration: {string.Join(", ", errors)}");
            }

            await UpdateConfigurationFileAsync(serverId, serverConfig);
            
            // Add only the new MCP client for this server
            _logger.LogInformation("Adding MCP client for new server {ServerId}", serverId);
            await _mcpClientsFactory.AddClientAsync(serverId);
            
            _logger.LogInformation("Created MCP server {ServerId}", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MCP server {ServerId}", serverId);
            throw;
        }
    }

    public async Task UpdateServerAsync(string serverId, McpServerConfig serverConfig)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID is required", nameof(serverId));

            if (serverConfig == null)
                throw new ArgumentNullException(nameof(serverConfig));

            // Check if server exists
            if (!await ServerExistsAsync(serverId))
                throw new ArgumentException($"Server with ID '{serverId}' not found");

            // Validate the configuration
            if (!ValidateServerConfig(serverConfig))
            {
                var errors = GetValidationErrors(serverConfig);
                throw new ArgumentException($"Invalid configuration: {string.Join(", ", errors)}");
            }

            await UpdateConfigurationFileAsync(serverId, serverConfig);
            
            // Update only the specific MCP client for this server
            _logger.LogInformation("Updating MCP client for server {ServerId}", serverId);
            await _mcpClientsFactory.UpdateClientAsync(serverId);
            
            _logger.LogInformation("Updated MCP server {ServerId}", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating MCP server {ServerId}", serverId);
            throw;
        }
    }

    public async Task DeleteServerAsync(string serverId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID is required", nameof(serverId));

            // Check if server exists
            if (!await ServerExistsAsync(serverId))
                throw new ArgumentException($"Server with ID '{serverId}' not found");

            await RemoveServerFromConfigurationAsync(serverId);
            
            // Remove only the specific MCP client for this server
            _logger.LogInformation("Removing MCP client for deleted server {ServerId}", serverId);
            await _mcpClientsFactory.RemoveClientAsync(serverId);
            
            _logger.LogInformation("Deleted MCP server {ServerId}", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting MCP server {ServerId}", serverId);
            throw;
        }
    }

    public async Task ToggleServerStatusAsync(string serverId, bool enabled)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID is required", nameof(serverId));

            // Check if server exists
            if (!await ServerExistsAsync(serverId))
                throw new ArgumentException($"Server with ID '{serverId}' not found");

            await UpdateServerStatusAsync(serverId, enabled);
            
            // Update only the specific MCP client for this server (enable/disable)
            _logger.LogInformation("Updating MCP client status for server {ServerId} to {Enabled}", serverId, enabled);
            await _mcpClientsFactory.UpdateClientAsync(serverId);
            
            _logger.LogInformation("Toggled MCP server {ServerId} status to {Enabled}", serverId, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling MCP server {ServerId} status", serverId);
            throw;
        }
    }

    public bool ValidateServerConfig(McpServerConfig serverConfig)
    {
        return serverConfig?.IsValid() ?? false;
    }

    public IEnumerable<string> GetValidationErrors(McpServerConfig serverConfig)
    {
        return serverConfig?.GetValidationErrors() ?? new[] { "Server configuration is null" };
    }

    public Task<bool> ServerExistsAsync(string serverId)
    {
        try
        {
            var options = _mcpOptions.CurrentValue;
            return Task.FromResult(options.McpServers.ContainsKey(serverId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if server {ServerId} exists", serverId);
            throw;
        }
    }

    private string GetConfigurationSummary(McpServerConfig config)
    {
        if (!string.IsNullOrEmpty(config.Command))
        {
            var args = config.Args?.Length > 0 ? $" {string.Join(" ", config.Args)}" : "";
            return $"{config.Command}{args}";
        }
        
        if (!string.IsNullOrEmpty(config.Url))
        {
            return config.Url;
        }
        
        return "Not configured";
    }

    private async Task UpdateServerStatusAsync(string serverId, bool enabled)
    {
        try
        {
            // Get the path to the mcp.json file
            var mcpFilePath = GetMcpFilePath();
            
            // Read the current configuration
            var currentOptions = _mcpOptions.CurrentValue;
            
            // Update the server status
            if (currentOptions.McpServers.ContainsKey(serverId))
            {
                currentOptions.McpServers[serverId].Enabled = enabled;
                
                // Serialize and write back to file
                var json = JsonSerializer.Serialize(currentOptions, GetJsonSerializerOptions());
                
                await File.WriteAllTextAsync(mcpFilePath, json);
            }
            else
            {
                throw new ArgumentException($"Server '{serverId}' not found in configuration");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update server status in configuration file: {ex.Message}", ex);
        }
    }

    private async Task UpdateConfigurationFileAsync(string serverId, McpServerConfig serverConfig)
    {
        try
        {
            // Get the path to the mcp.json file
            var mcpFilePath = GetMcpFilePath();
            
            // Read the current configuration
            var currentOptions = _mcpOptions.CurrentValue;
            
            // Add or update the server
            currentOptions.McpServers[serverId] = serverConfig;
            
            // Serialize and write back to file
            var json = JsonSerializer.Serialize(currentOptions, GetJsonSerializerOptions());
            
            await File.WriteAllTextAsync(mcpFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update configuration file: {ex.Message}", ex);
        }
    }

    private async Task RemoveServerFromConfigurationAsync(string serverId)
    {
        try
        {
            // Get the path to the mcp.json file
            var mcpFilePath = GetMcpFilePath();
            
            // Read the current configuration
            var currentOptions = _mcpOptions.CurrentValue;
            
            // Remove the server
            if (currentOptions.McpServers.ContainsKey(serverId))
            {
                currentOptions.McpServers.Remove(serverId);
                
                // Serialize and write back to file
                var json = JsonSerializer.Serialize(currentOptions, GetJsonSerializerOptions());
                
                await File.WriteAllTextAsync(mcpFilePath, json);
            }
            else
            {
                throw new ArgumentException($"Server '{serverId}' not found in configuration");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to remove server from configuration file: {ex.Message}", ex);
        }
    }

    private string GetMcpFilePath()
    {
        // Try to get the mcp file path from command line args first
        var args = Environment.GetCommandLineArgs();
        var mcpFilePath = args.FirstOrDefault(a => a.StartsWith("--mcp-file="))?.Split('=')[1];
        
        if (!string.IsNullOrEmpty(mcpFilePath) && File.Exists(mcpFilePath))
        {
            return mcpFilePath;
        }
        
        // Fallback to default location
        return Path.Combine(AppContext.BaseDirectory, "mcp.json");
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<McpToolInfo[]> GetServerToolsAsync(string serverId)
    {
        try
        {
            if (!await ServerExistsAsync(serverId))
                return Array.Empty<McpToolInfo>();

            
            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);

            if (client == null || client.ServerCapabilities.Tools == null)
                return Array.Empty<McpToolInfo>();

            var tools = await client.ListToolsAsync(cancellationToken: CancellationToken.None);
            return tools.Select(tool => new McpToolInfo
            {
                Name = tool.ProtocolTool.Name,
                Description = tool.ProtocolTool.Description,
                InputSchema = tool.ProtocolTool.InputSchema
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tools for MCP server {ServerId}", serverId);
            return Array.Empty<McpToolInfo>();
        }
    }

    public async Task<McpPromptInfo[]> GetServerPromptsAsync(string serverId)
    {
        try
        {
            if (!await ServerExistsAsync(serverId))
                return Array.Empty<McpPromptInfo>();

            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null || client.ServerCapabilities.Prompts == null)
                return Array.Empty<McpPromptInfo>();

            var prompts = await client.ListPromptsAsync(cancellationToken: CancellationToken.None);
            return prompts.Select(prompt => new McpPromptInfo
            {
                Name = prompt.ProtocolPrompt.Name,
                Description = prompt.ProtocolPrompt.Description,
                Arguments = prompt.ProtocolPrompt.Arguments?.ToArray()
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompts for MCP server {ServerId}", serverId);
            return Array.Empty<McpPromptInfo>();
        }
    }

    public async Task<McpResourceInfo[]> GetServerResourcesAsync(string serverId)
    {
        try
        {
            if (!await ServerExistsAsync(serverId))
                return Array.Empty<McpResourceInfo>();

            var client = await _mcpClientsFactory.GetMcpClientAsync(serverId);
            if (client == null || client.ServerCapabilities.Resources == null)
                return Array.Empty<McpResourceInfo>();

            var resources = await client.ListResourcesAsync(cancellationToken: CancellationToken.None);
            return resources.Select(resource => new McpResourceInfo
            {
                Uri = resource.ProtocolResource.Uri,
                Name = resource.ProtocolResource.Name,
                Description = resource.ProtocolResource.Description,
                MimeType = resource.ProtocolResource.MimeType
            }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resources for MCP server {ServerId}", serverId);
            return Array.Empty<McpResourceInfo>();
        }
    }

    public async Task<string> GetMcpJsonContentAsync()
    {
        try
        {
            var mcpFilePath = GetMcpFilePath();
            if (File.Exists(mcpFilePath))
            {
                return await File.ReadAllTextAsync(mcpFilePath);
            }
            
            _logger.LogWarning("MCP configuration file not found at {McpFilePath}", mcpFilePath);
            return "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading MCP configuration file");
            throw;
        }
    }
}
