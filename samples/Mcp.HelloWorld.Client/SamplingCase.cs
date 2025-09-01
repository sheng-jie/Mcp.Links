using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace Mcp.HelloWorld.Client;

public static class SamplingCase
{
    public static async Task RunAsync(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apikey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");

        var azureClient = new AzureOpenAIClient(
            endpoint: new Uri(endpoint),
            credential: new ApiKeyCredential(apikey)
        );

        // Connect to an MCP server
        Console.WriteLine("Connecting client to MCP 'everything' server");


        // Create a sampling client.
        using IChatClient samplingClient = azureClient.GetChatClient(configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini").AsIChatClient();

        var mcpClient = await McpClientFactory.CreateAsync(
            new StdioClientTransport(new()
            {
                Command = "npx",
                Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-everything"],
                Name = "Everything",
            }),
            clientOptions: new()
            {
                Capabilities = new()
                {
                    Sampling = new() { SamplingHandler = samplingClient.CreateSamplingHandler() }
                },
            },
            loggerFactory: loggerFactory);


        // Get all available tools
        Console.WriteLine("Tools available:");
        var tools = await mcpClient.ListToolsAsync();

        tools.ToList().ForEach(Console.WriteLine);

        var result = await mcpClient.CallToolAsync("sampleLLM", new Dictionary<string, object?>() { { "prompt", "What is the meaning of life?" } } as IReadOnlyDictionary<string, object?>);

        Console.WriteLine($"sampleLLM tool result: {result.Content.FirstOrDefault()?.ToAIContent()?.ToString()}");

        await ChatWithToolsAsync(configuration, azureClient, tools);
    }

    private static async Task ChatWithToolsAsync(IConfiguration configuration, AzureOpenAIClient azureClient, IList<McpClientTool> tools)
    {
        IChatClient chatClient = azureClient.GetChatClient(configuration["AzureOpenAI:ChatDeploymentName"] ?? "gpt-4.1").AsIChatClient();
        var client = new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();

        // Have a conversation, making all tools available to the LLM.
        List<ChatMessage> messages = [];
        while (true)
        {
            Console.Write("Q: ");
            messages.Add(new(ChatRole.User, Console.ReadLine()));

            List<ChatResponseUpdate> updates = [];
            await foreach (var update in client.GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
            {
                Console.Write(update);
                updates.Add(update);
            }
            Console.WriteLine();

            messages.AddMessages(updates);
        }
    }
}
