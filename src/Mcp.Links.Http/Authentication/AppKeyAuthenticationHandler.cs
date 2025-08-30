using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mcp.Links.Http.Authentication;


public class AppKeyAuthenticationHandler : AuthenticationHandler<AppKeyAuthenticationOptions>
{
    private const string AppIdHeaderName = "X-AppId";
    private const string AppKeyHeaderName = "X-AppKey";
    public AppKeyAuthenticationHandler(IOptionsMonitor<AppKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 只对 /mcp 端点进行认证检查
        if (!Request.Path.StartsWithSegments("/mcp"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(AppIdHeaderName, out var appIdHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("App ID is missing"));
        }

        if (!Request.Headers.TryGetValue(AppKeyHeaderName, out var appKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("App Key is missing"));
        }

        var appId = appIdHeaderValues.FirstOrDefault();
        var appKey = appKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrEmpty(appId))
        {
            return Task.FromResult(AuthenticateResult.Fail("App ID is empty"));
        }

        if (string.IsNullOrEmpty(appKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("App Key is empty"));
        }

        // Check if the App ID and App Key are valid
        var validApp = Options.Apps.FirstOrDefault(a => a.AppId == appId && a.AppKey == appKey);

        if (validApp.Equals(default(AppIdAndAppKey)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid App ID or App Key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, appId),
            new Claim("AppId", appId)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class AppKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public AppIdAndAppKey[] Apps { get; set; }
}

public record struct AppIdAndAppKey(string AppId, string AppKey);