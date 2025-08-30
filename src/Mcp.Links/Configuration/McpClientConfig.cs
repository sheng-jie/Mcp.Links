using System;

namespace Mcp.Links.Configuration;

public class McpClientConfig
{

    /// <summary>
    /// The unique identifier for the client app.
    /// </summary>
    public required string AppId { get; set; }


    /// <summary>
    /// The key for the client app.
    /// </summary>
    public required string AppKey { get; set; }

    /// <summary>
    /// The name of the client app.
    /// </summary>
    public required string Name { get; set; }


    /// <summary>
    /// The description of the client app.
    /// </summary>
    public string? Description { get; set; }


    /// <summary>
    /// Gets or sets the list of MCP server IDs associated with the client app.
    /// </summary>
    public required string[] McpServerIds { get; set; }
}
