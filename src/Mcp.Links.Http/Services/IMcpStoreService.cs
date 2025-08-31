using Mcp.Links.Configuration;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service interface for managing MCP store operations including server installation.
/// </summary>
public interface IMcpStoreService
{
    /// <summary>
    /// Parses store item configuration and returns installation details.
    /// </summary>
    /// <param name="storeItem">The store item to parse.</param>
    /// <returns>Installation details including server type and configuration.</returns>
    Task<McpStoreInstallationInfo> ParseInstallationInfoAsync(McpStoreItem storeItem);

    /// <summary>
    /// Installs an MCP server from store configuration.
    /// </summary>
    /// <param name="storeItem">The store item to install.</param>
    /// <param name="customServerId">Optional custom server ID. If not provided, one will be generated.</param>
    /// <returns>The installed server ID.</returns>
    Task<string> InstallServerFromStoreAsync(McpStoreItem storeItem, string? customServerId = null);

    /// <summary>
    /// Validates if a store item can be installed.
    /// </summary>
    /// <param name="storeItem">The store item to validate.</param>
    /// <returns>Validation result with details.</returns>
    Task<McpStoreValidationResult> ValidateStoreItemAsync(McpStoreItem storeItem);

    /// <summary>
    /// Generates a unique server ID from a store item.
    /// </summary>
    /// <param name="storeItem">The store item to generate ID for.</param>
    /// <returns>A unique server ID.</returns>
    Task<string> GenerateServerIdAsync(McpStoreItem storeItem);
}

/// <summary>
/// Information about an MCP store item installation.
/// </summary>
public class McpStoreInstallationInfo
{
    public required string ServerType { get; set; } // "stdio", "http", "sse"
    public required string GeneratedServerId { get; set; }
    public McpServerConfig? ServerConfig { get; set; }
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public bool RequiresEnvironmentVariables { get; set; }
    public string[]? RequiredEnvironmentVars { get; set; }
    public string? InstallationNotes { get; set; }
}

/// <summary>
/// Validation result for MCP store items.
/// </summary>
public class McpStoreValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool RequiresManualConfiguration { get; set; }
    public string[]? MissingEnvironmentVars { get; set; }
}

/// <summary>
/// Data transfer object for MCP store items.
/// </summary>
public class McpStoreItem
{
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
}
