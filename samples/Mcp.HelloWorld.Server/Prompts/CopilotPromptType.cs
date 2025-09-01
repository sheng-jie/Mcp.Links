using ModelContextProtocol.Server;
using System.ComponentModel;


namespace Mcp.HelloWorld.Server.Prompts;

[McpServerPromptType]
public class CopilotPromptType
{
    [McpServerPrompt(Name = "dotnet-design-pattern-review"), Description("A prompt for reviewing .NET design patterns")]
    public static string DotNetDesignPatternReviewPrompt() => GetPrompt("dotnet-design-pattern-review");

    [McpServerPrompt(Name = "containerize-aspnetcore"), Description("A prompt for containerizing ASP.NET Core applications with Docker")]
    public static string ContainerizeAspNetCorePrompt() => GetPrompt("containerize-aspnetcore");

    [McpServerPrompt(Name = "dotnet-best-practices"), Description("A prompt for ensuring .NET/C# code follows best practices")]
    public static string DotNetBestPracticesPrompt() => GetPrompt("dotnet-best-practices");

    [McpServerPrompt(Name = "csharp-async"), Description("A prompt for C# async programming best practices")]
    public static string CSharpAsyncPrompt() => GetPrompt("csharp-async");

    [McpServerPrompt(Name = "create-readme"), Description("A prompt for creating comprehensive README.md files for projects")]
    public static string CreateReadmePrompt() => GetPrompt("create-readme");

    [McpServerPrompt(Name = "create-agentsmd"), Description("A prompt for creating high-quality AGENTS.md files following the agents.md format")]
    public static string CreateAgentsMdPrompt() => GetPrompt("create-agentsmd");

    private static string GetPrompt(string promptName)
    {
        var fileName = $"{promptName}.prompt.md";
        var filePath = Path.Combine("Prompts", fileName);
        
        if (!File.Exists(filePath))
        {
            // Fallback to current directory
            filePath = fileName;
        }
        
        return File.ReadAllText(filePath);
    }
}
