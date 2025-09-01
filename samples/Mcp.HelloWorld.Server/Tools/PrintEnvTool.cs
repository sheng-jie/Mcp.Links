using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace Mcp.HelloWorld.Server.Tools;

[McpServerToolType]
public sealed class PrintEnvTool
{
    [McpServerTool, Description("Prints all environment variables, helpful for debugging MCP server configuration")]
    public static string PrintEnv()
    {
        var environmentVariables = Environment.GetEnvironmentVariables();
        var envDict = new Dictionary<string, string?>();
        
        foreach (System.Collections.DictionaryEntry entry in environmentVariables)
        {
            envDict[entry.Key?.ToString() ?? ""] = entry.Value?.ToString();
        }
        
        return JsonSerializer.Serialize(envDict, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
}
