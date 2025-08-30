using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;

namespace Mcp.Links.Aggregation;

public static class AggregatedMcpServerBuilderExtensions
{
    public static IMcpServerBuilder AddAggregatedHttpMcpServer(this IServiceCollection services, Action<McpServerOptions>? configureOptions = null)
    => services.AddMcpServer(configureOptions)
        .WithAggregatedTransport("http");

    public static IMcpServerBuilder AddAggregatedStdioMcpServer(this IServiceCollection services, Action<McpServerOptions>? configureOptions = null)
        => services.AddMcpServer(configureOptions)
            .WithAggregatedTransport("stdio");


    private static IMcpServerBuilder WithAggregatedTransport(this IMcpServerBuilder builder, string transportType)
    {
        Throw.IfNull(builder);

        builder.Services.TryAddSingleton<McpClientsFactory>();
        builder.Services.TryAddSingleton<McpToolsHandler>();

        if (transportType.Equals("stdio", StringComparison.OrdinalIgnoreCase))
            builder.WithStdioServerTransport();
        else
            builder.WithHttpTransport();

        builder.WithHttpTransport();

        builder.ConfigureHandlers();

        return builder;
    }
    

    private static void ConfigureHandlers(this IMcpServerBuilder builder)
    {
        builder.WithListToolsHandler(
            async (context, stoppingToken) =>
            {
                if (context.Services is null)
                    throw new InvalidOperationException("Service provider is null in the current context.");
                McpToolsHandler mcpToolsHandler = context.Services.GetRequiredService<McpToolsHandler>();
                return await mcpToolsHandler.ListAggregatedToolsAsync(context, stoppingToken);
            });

        builder.WithCallToolHandler(async (context, cancellationToken) =>
        {
            if (context.Services is null)
                throw new InvalidOperationException("Service provider is null in the current context.");
            var mcpToolsHandler = context.Services.GetRequiredService<McpToolsHandler>();
            return await mcpToolsHandler.CallAggregatedToolAsync(context, cancellationToken);
        });
    }
}

