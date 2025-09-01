using Mcp.HelloWorld.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Build configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();


using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// await LoggingCase.RunAsync(configuration, loggerFactory);
// await SamplingCase.RunAsync(configuration, loggerFactory);

await ElicitationCase.RunAsync(configuration, loggerFactory);

Console.ReadLine();
