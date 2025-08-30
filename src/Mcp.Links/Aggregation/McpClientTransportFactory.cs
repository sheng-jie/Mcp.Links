using Mcp.Links.Configuration;
using ModelContextProtocol.Client;

namespace Mcp.Links.Aggregation;

public static class McpClientTransportFactory
    {
        public static IClientTransport Create(string name, McpServerConfig config)
        {
            if (config.Type == "stdio")
            {
                return new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = $"{name}-client",
                    Command = config.Command!,
                    Arguments = config.Args,
                    EnvironmentVariables = config.Env
                });
            }
            else if (config.Type == "sse" || config.Type == "http")
            {
                var options = new SseClientTransportOptions
                {
                    Name = $"{name}-client",
                    Endpoint = new Uri(config.Url!),
                    TransportMode = config.Type == "sse" ? HttpTransportMode.Sse : HttpTransportMode.AutoDetect,
                    AdditionalHeaders = config.Headers
                };
                
                return new SseClientTransport(options);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported MCP server type: {config.Type}");
            }
        }
    }
