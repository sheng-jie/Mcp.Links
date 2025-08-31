using Mcp.Links.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service implementation for managing MCP store operations including server installation.
/// </summary>
public class McpStoreService : IMcpStoreService
{
    private readonly IMcpServerService _mcpServerService;
    private readonly ILogger<McpStoreService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpStoreService(IMcpServerService mcpServerService, ILogger<McpStoreService> logger)
    {
        _mcpServerService = mcpServerService;
        _logger = logger;
    }

    public async Task<McpStoreInstallationInfo> ParseInstallationInfoAsync(McpStoreItem storeItem)
    {
        try
        {
            _logger.LogInformation("Parsing installation info for store item: {Title}", storeItem.Title);

            if (string.IsNullOrWhiteSpace(storeItem.Config))
            {
                throw new ArgumentException("Store item has no configuration");
            }

            // Parse the JSON configuration
            var configJson = JsonDocument.Parse(storeItem.Config);
            
            if (!configJson.RootElement.TryGetProperty("mcpServers", out var mcpServersElement))
            {
                throw new ArgumentException("Configuration does not contain 'mcpServers' property");
            }

            // Get the first server configuration (most store items have a single server)
            var firstServerProperty = mcpServersElement.EnumerateObject().FirstOrDefault();
            if (firstServerProperty.Value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException("No server configurations found in mcpServers");
            }

            var serverName = firstServerProperty.Name;
            var serverConfigElement = firstServerProperty.Value;

            // Generate server ID
            var generatedServerId = await GenerateServerIdAsync(storeItem);

            // Determine server type and extract configuration
            var installationInfo = new McpStoreInstallationInfo
            {
                GeneratedServerId = generatedServerId,
                ServerType = "unknown" // Will be set below based on configuration
            };

            // Check if it's a command-based (stdio) server
            if (serverConfigElement.TryGetProperty("command", out var commandElement))
            {
                installationInfo.ServerType = "stdio";
                installationInfo.Command = commandElement.GetString();

                // Extract args if present
                if (serverConfigElement.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array)
                {
                    installationInfo.Args = argsElement.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!)
                        .ToArray();
                }

                // Extract environment variables if present
                if (serverConfigElement.TryGetProperty("env", out var envElement) && envElement.ValueKind == JsonValueKind.Object)
                {
                    installationInfo.Environment = new Dictionary<string, string>();
                    var requiredEnvVars = new List<string>();
                    
                    foreach (var envVar in envElement.EnumerateObject())
                    {
                        var value = envVar.Value.GetString() ?? "";
                        installationInfo.Environment[envVar.Name] = value;
                        
                        // Check if this is a placeholder that needs user input
                        if (IsPlaceholderValue(value))
                        {
                            requiredEnvVars.Add(envVar.Name);
                        }
                    }

                    if (requiredEnvVars.Any())
                    {
                        installationInfo.RequiresEnvironmentVariables = true;
                        installationInfo.RequiredEnvironmentVars = requiredEnvVars.ToArray();
                    }
                }

                // Create server configuration
                installationInfo.ServerConfig = new McpServerConfig
                {
                    Type = "stdio",
                    Command = installationInfo.Command,
                    Args = installationInfo.Args,
                    Env = installationInfo.Environment?.ToDictionary(kv => kv.Key, kv => (string?)kv.Value),
                    Enabled = true
                };
            }
            // Check if it's a URL-based (HTTP/SSE) server
            else if (serverConfigElement.TryGetProperty("url", out var urlElement))
            {
                var url = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new ArgumentException("URL cannot be empty");
                }

                // Determine if it's HTTP or SSE based on URL or explicit type
                var serverType = "http"; // Default to http
                if (serverConfigElement.TryGetProperty("type", out var typeElement))
                {
                    serverType = typeElement.GetString() ?? "http";
                }
                else if (url.Contains("/sse"))
                {
                    serverType = "sse";
                }

                installationInfo.ServerType = serverType;
                installationInfo.Url = url;

                // Check for placeholders in URL that need replacement
                if (IsPlaceholderValue(url))
                {
                    installationInfo.RequiresEnvironmentVariables = true;
                    installationInfo.RequiredEnvironmentVars = ExtractPlaceholdersFromUrl(url);
                }

                // Extract headers if present
                if (serverConfigElement.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == JsonValueKind.Object)
                {
                    installationInfo.Headers = new Dictionary<string, string>();
                    foreach (var header in headersElement.EnumerateObject())
                    {
                        installationInfo.Headers[header.Name] = header.Value.GetString() ?? "";
                    }
                }

                // Create server configuration
                installationInfo.ServerConfig = new McpServerConfig
                {
                    Type = serverType,
                    Url = installationInfo.Url,
                    Headers = installationInfo.Headers,
                    Enabled = true
                };
            }
            else
            {
                throw new ArgumentException("Server configuration must have either 'command' or 'url' property");
            }

            // Add installation notes
            installationInfo.InstallationNotes = GenerateInstallationNotes(installationInfo);

            _logger.LogInformation("Successfully parsed installation info for {Title} as {ServerType} server", 
                storeItem.Title, installationInfo.ServerType);

            return installationInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing installation info for store item: {Title}", storeItem.Title);
            throw;
        }
    }

    public async Task<string> InstallServerFromStoreAsync(McpStoreItem storeItem, string? customServerId = null)
    {
        try
        {
            _logger.LogInformation("Installing server from store: {Title}", storeItem.Title);

            // Parse installation info
            var installationInfo = await ParseInstallationInfoAsync(storeItem);
            
            // Validate the installation
            var validationResult = await ValidateStoreItemAsync(storeItem);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Cannot install server: {string.Join(", ", validationResult.Errors)}");
            }

            // Use custom server ID if provided, otherwise use generated one
            var serverId = customServerId ?? installationInfo.GeneratedServerId;

            // Ensure server ID is unique
            if (await _mcpServerService.ServerExistsAsync(serverId))
            {
                // If custom ID conflicts, throw error. If generated ID conflicts, regenerate.
                if (!string.IsNullOrEmpty(customServerId))
                {
                    throw new ArgumentException($"Server ID '{serverId}' already exists");
                }
                
                // Regenerate with a suffix
                var baseId = serverId;
                var counter = 1;
                do
                {
                    serverId = $"{baseId}-{counter}";
                    counter++;
                } while (await _mcpServerService.ServerExistsAsync(serverId));
            }

            // Install the server
            if (installationInfo.ServerConfig == null)
            {
                throw new InvalidOperationException("Server configuration is null");
            }

            await _mcpServerService.CreateServerAsync(serverId, installationInfo.ServerConfig);

            _logger.LogInformation("Successfully installed server {ServerId} from store item {Title}", 
                serverId, storeItem.Title);

            return serverId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing server from store: {Title}", storeItem.Title);
            throw;
        }
    }

    public async Task<McpStoreValidationResult> ValidateStoreItemAsync(McpStoreItem storeItem)
    {
        var result = new McpStoreValidationResult();

        try
        {
            if (string.IsNullOrWhiteSpace(storeItem.Title))
            {
                result.Errors.Add("Store item must have a title");
            }

            if (string.IsNullOrWhiteSpace(storeItem.Config))
            {
                result.Errors.Add("Store item must have a configuration");
                result.IsValid = false;
                return result;
            }

            // Try to parse the installation info
            var installationInfo = await ParseInstallationInfoAsync(storeItem);

            // Validate server configuration
            if (installationInfo.ServerConfig == null)
            {
                result.Errors.Add("Failed to parse server configuration");
            }
            else if (!_mcpServerService.ValidateServerConfig(installationInfo.ServerConfig))
            {
                var validationErrors = _mcpServerService.GetValidationErrors(installationInfo.ServerConfig);
                result.Errors.AddRange(validationErrors);
            }

            // Check for required environment variables
            if (installationInfo.RequiresEnvironmentVariables)
            {
                result.RequiresManualConfiguration = true;
                result.MissingEnvironmentVars = installationInfo.RequiredEnvironmentVars;
                result.Warnings.Add("This server requires environment variables to be configured");
            }

            // For stdio servers, warn about command availability
            if (installationInfo.ServerType == "stdio")
            {
                if (string.IsNullOrWhiteSpace(installationInfo.Command))
                {
                    result.Errors.Add("Stdio server must have a command");
                }
                else
                {
                    result.Warnings.Add($"Ensure that '{installationInfo.Command}' is available in your system PATH");
                }
            }

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating store item: {Title}", storeItem.Title);
            result.Errors.Add($"Validation failed: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    public async Task<string> GenerateServerIdAsync(McpStoreItem storeItem)
    {
        // Generate a server ID based on the title
        var baseId = GenerateBaseServerId(storeItem.Title);
        
        // Ensure uniqueness
        var serverId = baseId;
        var counter = 1;
        while (await _mcpServerService.ServerExistsAsync(serverId))
        {
            serverId = $"{baseId}-{counter}";
            counter++;
        }

        return serverId;
    }

    private static string GenerateBaseServerId(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "mcp-server";
        }

        // Convert to lowercase and replace non-alphanumeric characters with hyphens
        var id = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-');

        // Ensure it starts with a letter and is not empty
        if (string.IsNullOrEmpty(id) || !char.IsLetter(id[0]))
        {
            id = "mcp-" + id;
        }

        // Limit length
        if (id.Length > 50)
        {
            id = id.Substring(0, 50).TrimEnd('-');
        }

        return id;
    }

    private static bool IsPlaceholderValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Common patterns for placeholder values
        return value.Contains("<") && value.Contains(">") ||
               value.Contains("{") && value.Contains("}") ||
               value.Contains("YOUR_") ||
               value.Contains("your_") ||
               value.Equals("api_key", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("your-api-key", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("xxx", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ExtractPlaceholdersFromUrl(string url)
    {
        var placeholders = new List<string>();
        
        // Extract placeholders in format {placeholder}
        var matches = Regex.Matches(url, @"\{([^}]+)\}");
        foreach (Match match in matches)
        {
            placeholders.Add(match.Groups[1].Value);
        }

        return placeholders.ToArray();
    }

    private static string GenerateInstallationNotes(McpStoreInstallationInfo info)
    {
        var notes = new List<string>();

        if (info.ServerType == "stdio")
        {
            notes.Add($"This is a command-line MCP server that runs: {info.Command}");
            
            if (info.Args?.Any() == true)
            {
                notes.Add($"Command arguments: {string.Join(" ", info.Args)}");
            }

            if (info.RequiresEnvironmentVariables)
            {
                notes.Add("⚠️ This server requires environment variables to be configured before it can run properly.");
                notes.Add($"Required variables: {string.Join(", ", info.RequiredEnvironmentVars ?? Array.Empty<string>())}");
            }

            notes.Add("Make sure the command is installed and available in your system PATH.");
        }
        else if (info.ServerType == "http" || info.ServerType == "sse")
        {
            notes.Add($"This is an HTTP-based MCP server connecting to: {info.Url}");
            
            if (info.RequiresEnvironmentVariables)
            {
                notes.Add("⚠️ This server requires API keys or authentication to be configured.");
                notes.Add("You may need to sign up for an API key from the service provider.");
            }

            if (info.Headers?.Any() == true)
            {
                notes.Add("This server uses custom HTTP headers for authentication or configuration.");
            }
        }

        return string.Join("\n", notes);
    }
}
