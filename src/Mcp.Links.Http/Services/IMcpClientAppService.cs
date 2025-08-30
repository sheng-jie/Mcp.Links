using Mcp.Links.Configuration;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service interface for managing MCP client app configurations.
/// </summary>
public interface IMcpClientAppService
{
    /// <summary>
    /// Gets all MCP client app configurations.
    /// </summary>
    /// <returns>Array of MCP client app objects.</returns>
    Task<McpClientConfig[]> GetAllClientAppsAsync();

    /// <summary>
    /// Gets a specific MCP client app configuration by ID.
    /// </summary>
    /// <param name="appId">The app ID to retrieve.</param>
    /// <returns>The client app if found, null otherwise.</returns>
    Task<McpClientConfig?> GetClientAppAsync(string appId);

    /// <summary>
    /// Creates a new MCP client app configuration.
    /// </summary>
    /// <param name="clientApp">The client app configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task CreateClientAppAsync(McpClientConfig clientApp);

    /// <summary>
    /// Updates an existing MCP client app configuration.
    /// </summary>
    /// <param name="appId">The app ID to update.</param>
    /// <param name="clientApp">The updated client app configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task UpdateClientAppAsync(string appId, McpClientConfig clientApp);

    /// <summary>
    /// Deletes an MCP client app configuration.
    /// </summary>
    /// <param name="appId">The app ID to delete.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task DeleteClientAppAsync(string appId);

    /// <summary>
    /// Checks if a client app ID already exists.
    /// </summary>
    /// <param name="appId">The app ID to check.</param>
    /// <returns>True if the app ID exists, false otherwise.</returns>
    Task<bool> ClientAppExistsAsync(string appId);

    /// <summary>
    /// Validates a client app configuration.
    /// </summary>
    /// <param name="clientApp">The client app configuration to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool ValidateClientApp(McpClientConfig clientApp);

    /// <summary>
    /// Gets validation errors for a client app configuration.
    /// </summary>
    /// <param name="clientApp">The client app configuration to validate.</param>
    /// <returns>Collection of validation error messages.</returns>
    IEnumerable<string> GetValidationErrors(McpClientConfig clientApp);
}
