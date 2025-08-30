using Mcp.Links.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service implementation for managing MCP client app configurations.
/// </summary>
public class McpClientAppService : IMcpClientAppService
{
    private readonly ILogger<McpClientAppService> _logger;
    private readonly IOptionsMonitor<McpClientConfigOptions> _clientConfigOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _clientAppsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpClientAppService(ILogger<McpClientAppService> logger, IOptionsMonitor<McpClientConfigOptions> clientConfigOptions, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _clientConfigOptions = clientConfigOptions;
        _httpContextAccessor = httpContextAccessor;
        
        // Use a client-apps.json file in the same directory as the main app
        _clientAppsFilePath = Path.Combine(AppContext.BaseDirectory, "client-apps.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<McpClientConfig[]> GetAllClientAppsAsync()
    {
        try
        {
            // Always read from file-based approach for dynamic data
            if (!File.Exists(_clientAppsFilePath))
            {
                _logger.LogInformation("Client apps file not found at {FilePath}, checking configuration fallback", _clientAppsFilePath);
                
                // Fallback to configuration options only if file doesn't exist
                var configOptions = _clientConfigOptions.CurrentValue;
                if (configOptions?.McpClients != null && configOptions.McpClients.Length > 0)
                {
                    _logger.LogDebug("Loaded {Count} client apps from configuration as fallback", configOptions.McpClients.Length);
                    // Save the configuration data to file for consistency
                    await SaveClientAppsAsync(configOptions.McpClients);
                    return configOptions.McpClients;
                }
                
                _logger.LogInformation("No client apps found in configuration, returning empty array");
                return Array.Empty<McpClientConfig>();
            }

            var json = await File.ReadAllTextAsync(_clientAppsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<McpClientConfig>();
            }

            var wrapper = JsonSerializer.Deserialize<McpClientConfigOptions>(json, _jsonOptions);
            var result = wrapper?.McpClients ?? Array.Empty<McpClientConfig>();
            
            _logger.LogDebug("Loaded {Count} client apps from file {FilePath}", result.Length, _clientAppsFilePath);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load client apps");
            throw new InvalidOperationException($"Failed to load client apps: {ex.Message}", ex);
        }
    }

    public async Task<McpClientConfig?> GetClientAppAsync(string appId)
    {
        var clientApps = await GetAllClientAppsAsync();
        return clientApps.FirstOrDefault(app => app.AppId == appId);
    }

    public async Task CreateClientAppAsync(McpClientConfig clientApp)
    {
        if (!ValidateClientApp(clientApp))
        {
            var errors = GetValidationErrors(clientApp);
            throw new ArgumentException($"Client app validation failed: {string.Join(", ", errors)}");
        }

        if (await ClientAppExistsAsync(clientApp.AppId))
        {
            throw new ArgumentException($"Client app with ID '{clientApp.AppId}' already exists");
        }

        var clientApps = (await GetAllClientAppsAsync()).ToList();
        clientApps.Add(clientApp);
        
        await SaveClientAppsAsync(clientApps.ToArray());
        _logger.LogInformation("Created client app with ID: {AppId}", clientApp.AppId);
    }

    public async Task UpdateClientAppAsync(string appId, McpClientConfig clientApp)
    {
        if (!ValidateClientApp(clientApp))
        {
            var errors = GetValidationErrors(clientApp);
            throw new ArgumentException($"Client app validation failed: {string.Join(", ", errors)}");
        }

        var clientApps = (await GetAllClientAppsAsync()).ToList();
        var existingIndex = clientApps.FindIndex(app => app.AppId == appId);
        
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Client app with ID '{appId}' not found");
        }

        // Ensure the app ID matches
        clientApp.AppId = appId;
        clientApps[existingIndex] = clientApp;
        
        await SaveClientAppsAsync(clientApps.ToArray());
        _logger.LogInformation("Updated client app with ID: {AppId}", appId);
    }

    public async Task DeleteClientAppAsync(string appId)
    {
        var clientApps = (await GetAllClientAppsAsync()).ToList();
        var existingIndex = clientApps.FindIndex(app => app.AppId == appId);
        
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Client app with ID '{appId}' not found");
        }

        clientApps.RemoveAt(existingIndex);
        await SaveClientAppsAsync(clientApps.ToArray());
        _logger.LogInformation("Deleted client app with ID: {AppId}", appId);
    }

    public async Task<bool> ClientAppExistsAsync(string appId)
    {
        var clientApps = await GetAllClientAppsAsync();
        return clientApps.Any(app => app.AppId == appId);
    }

    public bool ValidateClientApp(McpClientConfig clientApp)
    {
        return !GetValidationErrors(clientApp).Any();
    }

    public IEnumerable<string> GetValidationErrors(McpClientConfig clientApp)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(clientApp.AppId))
            errors.Add("App ID is required");

        if (string.IsNullOrWhiteSpace(clientApp.AppKey))
            errors.Add("App Key is required");

        if (string.IsNullOrWhiteSpace(clientApp.Name))
            errors.Add("Name is required");

        if (clientApp.McpServerIds == null)
            errors.Add("MCP Server IDs cannot be null");

        return errors;
    }

    public async Task<string> GenerateClientConfigurationAsync(string appId, string serverId)
    {
        try
        {
            _logger.LogInformation("Generating client configuration for app '{AppId}' and server '{ServerId}'", appId, serverId);

            // Get the client app
            var clientApp = await GetClientAppAsync(appId);
            if (clientApp == null)
            {
                throw new ArgumentException($"Client app with ID '{appId}' not found");
            }

            // Check if the server is associated with this client app
            if (clientApp.McpServerIds == null || !clientApp.McpServerIds.Contains(serverId))
            {
                throw new ArgumentException($"Server '{serverId}' is not associated with client app '{appId}'");
            }

            // Get the current request URL to build the proper MCP endpoint
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request != null 
                ? $"{request.Scheme}://{request.Host}" 
                : "http://localhost:5146"; // Fallback for non-HTTP contexts

            // Create the configuration entry for aggregated MCP server
            // All clients use the same "mcp-links" configuration with different App ID and App Key
            var configuration = new
            {
                mcpServers = new Dictionary<string, object>
                {
                    ["mcp-links"] = new
                    {
                        url = $"{baseUrl}/mcp",
                        type = "http",
                        headers = new Dictionary<string, string>
                        {
                            ["X-AppId"] = appId,
                            ["X-AppKey"] = clientApp.AppKey
                        }
                    }
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var configJson = JsonSerializer.Serialize(configuration, jsonOptions);
            
            _logger.LogDebug("Generated client configuration for app '{AppId}' and server '{ServerId}'", appId, serverId);
            return configJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate client configuration for app '{AppId}' and server '{ServerId}'", appId, serverId);
            throw;
        }
    }

    private async Task SaveClientAppsAsync(McpClientConfig[] clientApps)
    {
        try
        {
            var wrapper = new McpClientConfigOptions { McpClients = clientApps };
            var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_clientAppsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(_clientAppsFilePath, json);
            _logger.LogDebug("Saved client apps to {FilePath}", _clientAppsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save client apps to {FilePath}", _clientAppsFilePath);
            throw new InvalidOperationException($"Failed to save client apps: {ex.Message}", ex);
        }
    }
}
