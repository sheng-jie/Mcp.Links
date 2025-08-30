using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mcp.Links.Configuration;

public class McpClientConfigOptions
{
    /// <summary>
    /// Dictionary of MCP client configurations keyed by client ID.
    /// </summary>
    [JsonPropertyName("mcpClients")]
    [Required]
    public McpClientConfig[] McpClients { get; set; } = Array.Empty<McpClientConfig>();
}
