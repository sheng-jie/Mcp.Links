using System.Text.Json.Serialization;

namespace Mcp.Links.Configuration;

/// <summary>
/// Configuration for a single MCP server instance.
/// </summary>
public class McpServerConfig
{

    /// <summary>
    /// if the server is enabled. Defaults to true.
    /// Set to false to disable the server without removing it from the configuration.
    /// </summary>
    public bool? Enabled { get; set; } = true;

    /// <summary>
    /// Type of the MCP server connection. Can be "command", "sse", or null for command type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }



    private string? _command;
    /// <summary>
    /// Command to execute for command-type servers.
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command 
    {
        get => _command;
        set
        {
            _command = value;
            if (!string.IsNullOrEmpty(value))
            {
                Type = "stdio"; // Default to stdio if command is provided
            }
        }
    }

    
    private string? _url;
    /// <summary>
    /// URL for SSE-type servers.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url 
    {
        get => _url;
        set
        {
            _url = value;
            if (!string.IsNullOrEmpty(value))
            {
                if (string.IsNullOrEmpty(Type))
                    Type = "sse"; // Default to sse if url is provided and type is not set
            }
        }
    }


    /// <summary>
    /// Arguments to pass to the command for command-type servers.
    /// </summary>
    [JsonPropertyName("args")]
    public string[]? Args { get; set; }

    /// <summary>
    /// Environment variables to set for command-type servers.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string?>? Env { get; set; }

    /// <summary>
    /// HTTP headers to send with requests for SSE/HTTP-type servers.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }


    /// <summary>
    /// Validates the configuration based on the server type.
    /// </summary>
    /// <returns>True if the configuration is valid, otherwise false.</returns>
    public bool IsValid()
    {
        if(string.IsNullOrEmpty(Command) && string.IsNullOrEmpty(Url))
        {
            // If both Command and Url are null or empty, it's invalid
            return false;
        }

        return Type switch
        {
            "stdio" => !string.IsNullOrEmpty(Command),        
            "http" or "sse" => !string.IsNullOrEmpty(Url) && Uri.TryCreate(Url, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == "http" || uri.Scheme == "https"),
            _ => false
        };
    }

    /// <summary>
    /// Gets validation error messages for invalid configurations.
    /// </summary>
    /// <returns>Collection of validation error messages.</returns>
    public IEnumerable<string> GetValidationErrors()
    {
        var errors = new List<string>();

        switch (Type)
        {
            case "stdio":
                if (string.IsNullOrEmpty(Command))
                    errors.Add("Command is required for stdio-type servers");
                break;
            case "http":
            case "sse":
                if (string.IsNullOrEmpty(Url))
                    errors.Add("URL is required for SSE-type servers");
                else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || 
                         (uri.Scheme != "http" && uri.Scheme != "https"))
                    errors.Add("URL must be a valid HTTP or HTTPS URL for SSE-type servers");
                break;

            default:
                errors.Add($"Unsupported server type: {Type}");
                break;
        }

        return errors;
    }
}
