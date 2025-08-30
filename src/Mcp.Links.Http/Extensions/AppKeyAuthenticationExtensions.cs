using System;
using Mcp.Links.Http.Authentication;

namespace Mcp.Links.Http.Extensions;

public static class AppKeyAuthenticationExtensions
{
    public const string AuthorizationPolicyName = "McpAppKeyPolicy";

    public static IServiceCollection AddAppKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // do not set default authentication scheme
        // services.AddAuthentication(options =>
        // {
        //    options.DefaultAuthenticateScheme = "AppKey";
        //    options.DefaultChallengeScheme = "AppKey";
        // })
        services.AddAuthentication()
        .AddScheme<AppKeyAuthenticationOptions, AppKeyAuthenticationHandler>("AppKey", options =>
        {
            options.Apps = configuration.GetSection("mcpClients").Get<AppIdAndAppKey[]>() ?? Array.Empty<AppIdAndAppKey>();
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyName, policy =>
            {
                policy.AddAuthenticationSchemes("AppKey");
                policy.RequireClaim("AppId");
            });
        });

        return services;
    }
}
