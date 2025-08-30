using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mcp.Links.Configuration;

/// <summary>
/// Represents the root configuration for MCP servers from mcp.json file.
/// </summary>
public class McpServerConfigOptions
{
    /// <summary>
    /// Dictionary of MCP server configurations keyed by server ID.
    /// </summary>
    [JsonPropertyName("mcpServers")]
    [Required]
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = new();
}
