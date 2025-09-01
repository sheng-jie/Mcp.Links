

using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

// Build configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
var apikey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");

var azureClient = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new ApiKeyCredential(apikey)
);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Connect to an MCP server
Console.WriteLine("Connecting client to MCP 'everything' server");


// Create a sampling client.
IChatClient samplingClient = azureClient.GetChatClient(configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini").AsIChatClient();


var mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "npx",
        Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-everything"],
        Name = "Everything",
    }),
    clientOptions: new()
    {
        Capabilities = new() { Sampling = new() { SamplingHandler = samplingClient.CreateSamplingHandler() } },
    },
    loggerFactory: loggerFactory);

// Get all available tools
Console.WriteLine("Tools available:");
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"  {tool}");
}
var sampleLLMTool = tools.FirstOrDefault(t => t.Name == "sampleLLM");
if (sampleLLMTool != null)
{
    var result = await sampleLLMTool.CallAsync(new Dictionary<string, object?>() { { "prompt", "What is the meaning of life?" } } as IReadOnlyDictionary<string, object?>);
    // Process the result
    Console.WriteLine($"sampleLLM tool result: {result.Content.FirstOrDefault()?.ToAIContent()?.ToString()}");
}
else
{
    Console.WriteLine("sampleLLM tool not found.");
}

Console.WriteLine();

IChatClient chatClient = azureClient.GetChatClient(configuration["AzureOpenAI:ChatDeploymentName"] ?? "gpt-4.1").AsIChatClient();

// Create an IChatClient that can use the tools.

using var client = new ChatClientBuilder(chatClient)
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

