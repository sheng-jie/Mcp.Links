using ModelContextProtocol.Protocol;

namespace Mcp.Links.Http.Services;

/// <summary>
/// Service interface for MCP Inspector functionality.
/// Provides methods for connecting to and interacting with MCP servers for testing and debugging.
/// </summary>
public interface IMcpInspectorService
{
    /// <summary>
    /// Tests connection to a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to test.</param>
    /// <returns>Connection test result.</returns>
    Task<McpConnectionTestResult> TestConnectionAsync(string serverId);

    /// <summary>
    /// Gets all available tools from a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get tools from.</param>
    /// <returns>List of available tools.</returns>
    Task<McpInspectorTool[]> GetServerToolsAsync(string serverId);

    /// <summary>
    /// Calls a specific tool on an MCP server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="toolName">The name of the tool to call.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The tool call result.</returns>
    Task<McpToolCallResult> CallToolAsync(string serverId, string toolName, Dictionary<string, object>? arguments = null);

    /// <summary>
    /// Gets all available resources from a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get resources from.</param>
    /// <returns>List of available resources.</returns>
    Task<McpInspectorResource[]> GetServerResourcesAsync(string serverId);

    /// <summary>
    /// Reads a specific resource from an MCP server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="resourceUri">The URI of the resource to read.</param>
    /// <returns>The resource content.</returns>
    Task<McpResourceContent> ReadResourceAsync(string serverId, string resourceUri);

    /// <summary>
    /// Gets all available prompts from a specific MCP server.
    /// </summary>
    /// <param name="serverId">The server ID to get prompts from.</param>
    /// <returns>List of available prompts.</returns>
    Task<McpInspectorPrompt[]> GetServerPromptsAsync(string serverId);

    /// <summary>
    /// Gets a specific prompt from an MCP server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="promptName">The name of the prompt to get.</param>
    /// <param name="arguments">The prompt arguments.</param>
    /// <returns>The prompt content.</returns>
    Task<McpPromptContent> GetPromptAsync(string serverId, string promptName, Dictionary<string, object>? arguments = null);

    /// <summary>
    /// Gets server information and capabilities.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <returns>Server information.</returns>
    Task<McpServerInfo> GetServerInfoAsync(string serverId);

    /// <summary>
    /// Exports a server configuration for use in other MCP clients.
    /// </summary>
    /// <param name="serverId">The server ID to export.</param>
    /// <returns>The exported configuration.</returns>
    Task<McpServerExport> ExportServerConfigAsync(string serverId);

    /// <summary>
    /// Exports all servers configuration as a complete mcp.json file.
    /// </summary>
    /// <returns>The complete mcp.json configuration.</returns>
    Task<string> ExportAllServersConfigAsync();
}

/// <summary>
/// Result of a connection test to an MCP server.
/// </summary>
public class McpConnectionTestResult
{
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ConnectionTime { get; set; }
    public string? ServerVersion { get; set; }
    public ServerCapabilities? Capabilities { get; set; }
}

/// <summary>
/// Extended tool information for the inspector.
/// </summary>
public class McpInspectorTool
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public object? InputSchema { get; set; }
    public bool IsParameterless => InputSchema == null || 
        (InputSchema is System.Text.Json.JsonElement elem && elem.ValueKind == System.Text.Json.JsonValueKind.Object && !elem.EnumerateObject().Any());
}

/// <summary>
/// Result of a tool call.
/// </summary>
public class McpToolCallResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Result { get; set; }
    public string? RawResponse { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Extended resource information for the inspector.
/// </summary>
public class McpInspectorResource
{
    public required string Uri { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? MimeType { get; set; }
}

/// <summary>
/// Content of a resource.
/// </summary>
public class McpResourceContent
{
    public required string Uri { get; set; }
    public required string Content { get; set; }
    public string? MimeType { get; set; }
    public long? Size { get; set; }
}

/// <summary>
/// Extended prompt information for the inspector.
/// </summary>
public class McpInspectorPrompt
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public object[]? Arguments { get; set; }
    public bool HasArguments => Arguments != null && Arguments.Length > 0;
}

/// <summary>
/// Content of a prompt.
/// </summary>
public class McpPromptContent
{
    public required string Name { get; set; }
    public required object[] Messages { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Server export configuration.
/// </summary>
public class McpServerExport
{
    public required string ServerId { get; set; }
    public required object Configuration { get; set; }
    public required string ConfigType { get; set; } // "server-entry" or "complete-file"
}
