using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mcp.HelloWorld.Server.Tools;

[McpServerToolType]
public sealed class AddTool
{
    [McpServerTool, Description("Adds two numbers")]
    public static string Add(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        var sum = a + b;
        return $"The sum of {a} and {b} is {sum}.";
    }
}
