using System;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mcp.HelloWorld.Client;

public static class LoggingCase
{
    public static async Task RunAsync(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
        {
            Command = "npx",
            Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-everything"],
            Name = "Everything",
        }, loggerFactory: loggerFactory));

        await mcpClient.SetLoggingLevel(LogLevel.Information);

        mcpClient.RegisterNotificationHandler(NotificationMethods.LoggingMessageNotification,
            (notification, ct) =>
            {
                if (JsonSerializer.Deserialize<LoggingMessageNotificationParams>(notification.Params) is { } ln)
                {
                    Console.WriteLine($"[{ln.Level}] {ln.Logger} {ln.Data}");
                }
                else
                {
                    Console.WriteLine($"Received unexpected logging notification: {notification.Params}");
                }

                return default;
            });
    }
}
