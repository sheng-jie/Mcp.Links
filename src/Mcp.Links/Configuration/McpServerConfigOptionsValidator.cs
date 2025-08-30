using Microsoft.Extensions.Options;

namespace Mcp.Links.Configuration;

public class McpServerConfigOptionsValidator : IValidateOptions<McpServerConfigOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerConfigOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("McpServerOptions cannot be null.");
        }

        if (options.McpServers == null || options.McpServers.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one MCP server configuration must be provided.");
        }

        // Validate each server configuration
        foreach (var (serverId, serverConfig) in options.McpServers)
        {
            if (string.IsNullOrEmpty(serverId))
            {
                return ValidateOptionsResult.Fail("Server ID cannot be null or empty");
            }

            if (serverConfig == null)
            {
                return ValidateOptionsResult.Fail($"Server configuration for '{serverId}' cannot be null");
            }

            if (!serverConfig.IsValid())
            {
                var serverErrors = serverConfig.GetValidationErrors()
                    .Select(error => $"Server '{serverId}': {error}");
                return ValidateOptionsResult.Fail($"Invalid configuration for MCP server '{serverId}': {string.Join(", ", serverErrors)}");
            }
        }

        return ValidateOptionsResult.Success;
    }
}