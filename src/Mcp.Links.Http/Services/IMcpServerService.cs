using Mcp.Links.Configuration;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service interface for managing MCP server configurations.
/// </summary>
public interface IMcpServerService
{
    /// <summary>
    /// Gets all MCP server configurations.
    /// </summary>
    /// <returns>Array of MCP server information objects.</returns>
    Task<McpServerInfo[]> GetAllServersAsync();

    /// <summary>
    /// Gets a specific MCP server configuration by ID.
    /// </summary>
    /// <param name="serverId">The server ID to retrieve.</param>
    /// <returns>The server information if found, null otherwise.</returns>
    Task<McpServerInfo?> GetServerAsync(string serverId);

    /// <summary>
    /// Creates a new MCP server configuration.
    /// </summary>
    /// <param name="serverId">The unique server ID.</param>
    /// <param name="serverConfig">The server configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task CreateServerAsync(string serverId, McpServerConfig serverConfig);

    /// <summary>
    /// Updates an existing MCP server configuration.
    /// </summary>
    /// <param name="serverId">The server ID to update.</param>
    /// <param name="serverConfig">The updated server configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task UpdateServerAsync(string serverId, McpServerConfig serverConfig);

    /// <summary>
    /// Deletes an MCP server configuration.
    /// </summary>
    /// <param name="serverId">The server ID to delete.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task DeleteServerAsync(string serverId);

    /// <summary>
    /// Toggles the enabled status of an MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to toggle.</param>
    /// <param name="enabled">The new enabled status.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task ToggleServerStatusAsync(string serverId, bool enabled);

    /// <summary>
    /// Validates a server configuration.
    /// </summary>
    /// <param name="serverConfig">The server configuration to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool ValidateServerConfig(McpServerConfig serverConfig);

    /// <summary>
    /// Gets validation errors for a server configuration.
    /// </summary>
    /// <param name="serverConfig">The server configuration to validate.</param>
    /// <returns>Collection of validation error messages.</returns>
    IEnumerable<string> GetValidationErrors(McpServerConfig serverConfig);

    /// <summary>
    /// Checks if a server ID already exists.
    /// </summary>
    /// <param name="serverId">The server ID to check.</param>
    /// <returns>True if the server ID exists, false otherwise.</returns>
    Task<bool> ServerExistsAsync(string serverId);

    /// <summary>
    /// Gets tools for a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get tools for.</param>
    /// <returns>List of tools for the specified server.</returns>
    Task<McpToolInfo[]> GetServerToolsAsync(string serverId);

    /// <summary>
    /// Gets prompts for a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get prompts for.</param>
    /// <returns>List of prompts for the specified server.</returns>
    Task<McpPromptInfo[]> GetServerPromptsAsync(string serverId);

    /// <summary>
    /// Gets resources for a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get resources for.</param>
    /// <returns>List of resources for the specified server.</returns>
    Task<McpResourceInfo[]> GetServerResourcesAsync(string serverId);

    /// <summary>
    /// Gets the raw mcp.json configuration file content.
    /// </summary>
    /// <returns>The raw JSON content of the mcp.json file.</returns>
    Task<string> GetMcpJsonContentAsync();
}

/// <summary>
/// Data transfer object for MCP server information.
/// </summary>
public class McpServerInfo
{
    public required string ServerId { get; set; }
    public required string Type { get; set; }
    public bool Enabled { get; set; }
    public string? Command { get; set; }
    public string? Url { get; set; }
    public string[]? Args { get; set; }
    public int EnvCount { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public int HeadersCount { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public bool IsValid { get; set; }
    public string[] ValidationErrors { get; set; } = Array.Empty<string>();
    public required string Configuration { get; set; }
}

/// <summary>
/// Data transfer object for MCP tool information.
/// </summary>
public class McpToolInfo
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public object? InputSchema { get; set; }
}

/// <summary>
/// Data transfer object for MCP prompt information.
/// </summary>
public class McpPromptInfo
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public object[]? Arguments { get; set; }
}

/// <summary>
/// Data transfer object for MCP resource information.
/// </summary>
public class McpResourceInfo
{
    public required string Uri { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? MimeType { get; set; }
}
