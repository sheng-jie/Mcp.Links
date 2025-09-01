using System;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mcp.HelloWorld.Client;

/// <summary>
/// Enhanced Elicitation Case demonstrating MCP elicitation capabilities.
/// 
/// Improvements based on Node.js MCP server implementation:
/// - Better error handling with try-catch blocks
/// - User-friendly prompts with emoji indicators
/// - Support for skipping optional fields
/// - Input validation with retry logic
/// - Confirmation step before submitting data
/// - Proper handling of action types (accept, decline, cancel)
/// - Enhanced user feedback with detailed messages
/// - Graceful degradation when elicitation tool is not available
/// </summary>
public static class ElicitationCase
{
    public static async Task RunAsync(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var mcpClient = await McpClientFactory.CreateAsync(
            clientTransport: new StdioClientTransport(new()
            {
                Command = "npx",
                Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-everything"],
                Name = "Everything",
            }), clientOptions: new()
            {
                Capabilities = new()
                {
                    Elicitation = new()
                    {
                        ElicitationHandler = HandleElicitationAsync
                    }
                }
            },
            loggerFactory: loggerFactory);

        try
        {
            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine("🔧 Available tools:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"   • {tool.Name}: {tool.Description}");
            }

            // Look for the elicitation tool (should be named "startElicitation" based on the Node.js implementation)
            var elicitationTool = tools.FirstOrDefault(t => t.Name == "startElicitation");


            Console.WriteLine("\n🎯 Testing Elicitation Feature");
            Console.WriteLine("=====================================");
            Console.WriteLine("This will demonstrate how the MCP client can collect user input");
            Console.WriteLine("through the elicitation protocol when requested by a server tool.\n");

            Console.WriteLine("📞 Calling elicitation tool to demonstrate user input collection...\n");

            // Debug: Show the tool we're about to call
            Console.WriteLine($"🔍 Debug: About to call tool '{elicitationTool.Name}'");
            Console.WriteLine($"   Description: {elicitationTool.Description}");

            Console.WriteLine("🔄 Attempting to call elicitation tool with empty arguments...");
            CallToolResult result = await mcpClient.CallToolAsync("startElicitation", new Dictionary<string, object?>());

            Console.WriteLine("\n✅ Elicitation Result");
            Console.WriteLine("====================");
            foreach (var block in result.Content)
            {
                if (block is TextContentBlock textBlock)
                {
                    Console.WriteLine(textBlock.Text);
                }
                else
                {
                    Console.WriteLine($"📄 Received content block of type: {block.GetType().Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred during the elicitation demonstration: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("\n🏁 Elicitation demonstration completed.");
        }
    }


    private static ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
    {
        // Bail out if the requestParams is null or if the requested schema has no properties
        if (requestParams?.RequestedSchema?.Properties == null)
        {
            Console.WriteLine("❌ No elicitation schema provided or schema has no properties.");
            return ValueTask.FromResult(new ElicitResult
            {
                Action = "cancel"
            });
        }

        try
        {
            // Display the elicitation message if provided
            if (!string.IsNullOrEmpty(requestParams.Message))
            {
                Console.WriteLine("\n" + requestParams.Message);
            }
            else
            {
                Console.WriteLine("\n📝 Please provide the following information:");
            }

            var content = new Dictionary<string, JsonElement>();
            var propertyCount = requestParams.RequestedSchema.Properties.Count;
            var currentProperty = 0;

            // Loop through requestParams.RequestedSchema.Properties dictionary requesting values for each property
            foreach (var property in requestParams.RequestedSchema.Properties)
            {
                currentProperty++;
                Console.WriteLine($"\n[{currentProperty}/{propertyCount}] Processing: {property.Key}");

                try
                {
                    if (property.Value is ElicitRequestParams.BooleanSchema booleanSchema)
                    {
                        var result = GetBooleanInput(property.Key, booleanSchema);
                        if (result.HasValue)
                        {
                            content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.Value));
                            Console.WriteLine($"✅ Set {property.Key} = {result.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Skipped {property.Key}");
                        }
                    }
                    else if (property.Value is ElicitRequestParams.NumberSchema numberSchema)
                    {
                        var result = GetNumberInput(property.Key, numberSchema);
                        if (result.HasValue)
                        {
                            content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.Value));
                            Console.WriteLine($"✅ Set {property.Key} = {result.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Skipped {property.Key}");
                        }
                    }
                    else if (property.Value is ElicitRequestParams.StringSchema stringSchema)
                    {
                        var result = GetStringInput(property.Key, stringSchema);
                        if (!string.IsNullOrEmpty(result))
                        {
                            content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result));
                            Console.WriteLine($"✅ Set {property.Key} = \"{result}\"");
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Skipped {property.Key}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Unsupported schema type for property '{property.Key}': {property.Value?.GetType().Name}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\n❌ User cancelled input.");
                    return ValueTask.FromResult(new ElicitResult
                    {
                        Action = "cancel"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing property '{property.Key}': {ex.Message}");
                }
            }

            // Show summary of collected data
            Console.WriteLine($"\n📊 Summary: Collected {content.Count} out of {propertyCount} possible values");
            if (content.Count > 0)
            {
                Console.WriteLine("📝 Collected data:");
                foreach (var (key, value) in content)
                {
                    Console.WriteLine($"   • {key}: {value}");
                }
            }

            // Check if user wants to proceed
            Console.WriteLine("\n🤔 What would you like to do?");
            Console.WriteLine("   [y] Submit this information");
            Console.WriteLine("   [n] Decline to submit");
            Console.WriteLine("   [c] Cancel the operation");
            Console.Write("Your choice (y/n/c): ");

            var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();

            return confirmation switch
            {
                "y" or "yes" => ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = content
                }),
                "c" or "cancel" => ValueTask.FromResult(new ElicitResult
                {
                    Action = "cancel"
                }),
                _ => ValueTask.FromResult(new ElicitResult
                {
                    Action = "decline"
                })
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error during elicitation: {ex.Message}");
            return ValueTask.FromResult(new ElicitResult
            {
                Action = "cancel"
            });
        }
    }

    private static bool? GetBooleanInput(string propertyName, ElicitRequestParams.BooleanSchema schema)
    {
        while (true)
        {
            Console.Write($"{schema.Description ?? propertyName} (true/false/yes/no, or 'skip' to skip): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(input) || input == "skip")
            {
                return null;
            }

            if (bool.TryParse(input, out var boolResult))
            {
                return boolResult;
            }

            switch (input)
            {
                case "yes" or "y":
                    return true;
                case "no" or "n":
                    return false;
                default:
                    Console.WriteLine("❌ Invalid input. Please enter true/false, yes/no, or 'skip'.");
                    continue;
            }
        }
    }

    private static double? GetNumberInput(string propertyName, ElicitRequestParams.NumberSchema schema)
    {
        while (true)
        {
            var prompt = $"{schema.Description ?? propertyName}";
            if (schema.Minimum.HasValue && schema.Maximum.HasValue)
            {
                prompt += $" ({schema.Minimum}-{schema.Maximum})";
            }
            prompt += " (or 'skip' to skip): ";

            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) || input.ToLowerInvariant() == "skip")
            {
                return null;
            }

            if (double.TryParse(input, out var numberResult))
            {
                // Validate range if specified
                if (schema.Minimum.HasValue && numberResult < schema.Minimum.Value)
                {
                    Console.WriteLine($"❌ Number must be at least {schema.Minimum.Value}");
                    continue;
                }
                if (schema.Maximum.HasValue && numberResult > schema.Maximum.Value)
                {
                    Console.WriteLine($"❌ Number must be at most {schema.Maximum.Value}");
                    continue;
                }

                return numberResult;
            }

            Console.WriteLine("❌ Invalid number format. Please enter a valid number or 'skip'.");
        }
    }

    private static string? GetStringInput(string propertyName, ElicitRequestParams.StringSchema schema)
    {
        var prompt = $"{schema.Description ?? propertyName} (or 'skip' to skip): ";

        Console.Write(prompt);
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input) || input.ToLowerInvariant() == "skip")
        {
            return null;
        }

        return input;
    }
}
