using Mcp.Links.Aggregation;
using Mcp.Links.Configuration;
using Mcp.Links.Http.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components;
using AntDesign.ProLayout;
using Mcp.Links.Http.Extensions;
using Mcp.Links.Http;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);


// read the MCP server configuration from the args
builder.Configuration.AddCommandLine(args);

var mcpFilePath = args.FirstOrDefault(a => a.StartsWith("--mcp-file="))?.Split('=')[1];
if (!string.IsNullOrEmpty(mcpFilePath) && File.Exists(mcpFilePath))
{
    builder.Configuration.AddJsonFile(mcpFilePath, optional: false, reloadOnChange: true);
}
else
{
    // Fallback to the default mcp.json in the current directory
    builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("mcp.json", optional: false, reloadOnChange: true)
    .AddJsonFile("client-apps.json", optional: false, reloadOnChange: true);
}


// Register the configuration for MCP servers - bind to root since McpServerOptions expects the full JSON
builder.Services.Configure<McpServerConfigOptions>(builder.Configuration);
builder.Services.Configure<McpClientConfigOptions>(builder.Configuration);
builder.Services.AddSingleton<IValidateOptions<McpServerConfigOptions>, McpServerConfigOptionsValidator>();

// Add APP key authentication only for MCP
builder.Services.AddAppKeyAuthentication(builder.Configuration);

// Register MCP server service
builder.Services.AddScoped<IMcpServerService, McpServerService>();

// Register MCP inspector service
builder.Services.AddScoped<IMcpInspectorService, McpInspectorService>();

// Register MCP client app service  
builder.Services.AddScoped<IMcpClientAppService, McpClientAppService>();


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntDesign();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetService<NavigationManager>()!.BaseUri)
});


builder.Services.Configure<ProSettings>(builder.Configuration.GetSection("ProSettings"));
builder.Services.AddInteractiveStringLocalizer();
builder.Services.AddLocalization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<McpClientStartupService>();

builder.Services.AddAggregatedHttpMcpServer();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseStatusCodePagesWithReExecute("/not-found", createScopeForErrors: true);

app.UseHttpsRedirection();

// Add authentication middleware
app.UseAuthentication();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapMcp("/mcp")
   .RequireAuthorization(AppKeyAuthenticationExtensions.AuthorizationPolicyName);

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");


app.Run();
