using System;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mcp.HelloWorld.Client;

public static class ProgressCase
{
    public static async Task RunAsync(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
        {
            Command = "npx",
            Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-everything"],
            Name = "Everything",
        }, loggerFactory: loggerFactory));

        Console.WriteLine("=== Progress Case ===");

        var tools = await mcpClient.ListToolsAsync();
        var longRunningTool = tools.FirstOrDefault(t => t.Name == "longRunningOperation");
        if (longRunningTool is null)
        {
            Console.WriteLine("❌ LongRunningTool not found.");
            return;
        }

        Console.WriteLine($"🔍 Debug: About to call tool '{longRunningTool.Name}'");
        Console.WriteLine($"   Description: {longRunningTool.Description}");

        var progressUpdates = 0;

        // // <snippet_RegisterProgressHandler >
        // mcpClient.RegisterNotificationHandler(NotificationMethods.ProgressNotification, (notification, ct) =>
        // {
        //     if (JsonSerializer.Deserialize<ProgressNotificationParams>(notification.Params) is { } progress)
        //     {
        //         progressUpdates++;
        //         Console.WriteLine($"📊 Progress: {progress.Progress}");
        //     }
        //     else
        //     {
        //         Console.WriteLine($"Received unexpected progress notification: {notification.Params}");
        //     }

        //     return default;
        // });
        // // </snippet_RegisterProgressHandler >

        try
        {
            var progressHandler = new Progress<ProgressNotificationValue>(value =>
            {
                progressUpdates++;
                Console.WriteLine($"📊 Progress: {value.Progress} of {value.Total} - {value.Message}");
            });

            Console.WriteLine("🔄 Calling LongRunningTool with duration=10 and steps=5...");
            var result = await mcpClient.CallToolAsync(
               toolName: "longRunningOperation",
               arguments: new Dictionary<string, object?>
               {
                   { "duration", 10 },
                   { "steps", 5 },
               },
               progress: progressHandler);

            Console.WriteLine("\n✅ LongRunningTool Result");
            Console.WriteLine("========================");
            Console.WriteLine(result.Content.FirstOrDefault()?.ToAIContent()?.ToString());
            Console.WriteLine($"\nTotal Progress Updates Received: {progressUpdates}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error calling LongRunningTool: {ex.Message}");
        }
    }
}
