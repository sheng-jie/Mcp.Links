using System.Net.Http.Headers;
using Mcp.HelloWorld.Server.Resources;
using Mcp.HelloWorld.Server.Tools;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResources<SimpleResourceType>();

builder.Services.AddHttpClient("WeatherApi", client =>
{
    client.BaseAddress = new Uri("https://api.weather.gov");
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-tool", "1.0"));
});
    
var app = builder.Build();

app.MapMcp();

app.Run();
