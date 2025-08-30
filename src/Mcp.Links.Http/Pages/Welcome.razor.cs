using Microsoft.AspNetCore.Components;
using Mcp.Links.Http.Services;
using Mcp.Links.Configuration;

namespace Mcp.Links.Http.Pages;

public partial class Welcome
{
    [Inject] private IMcpServerService McpServerService { get; set; } = default!;
    [Inject] private IMcpClientAppService McpClientAppService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private DashboardStats stats = new();
    private bool isLoading = true;
    private string[] recentActivities = Array.Empty<string>();

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        try
        {
            isLoading = true;
            
            // Load server configurations
            var servers = await McpServerService.GetAllServersAsync();
            var clientApps = await McpClientAppService.GetAllClientAppsAsync();

            stats = new DashboardStats
            {
                TotalServers = servers.Length,
                EnabledServers = servers.Count(s => s.Enabled),
                TotalClientApps = clientApps.Length,
                ConnectedServers = servers.Count(s => s.Enabled && s.IsValid),
                AvailableTools = await GetTotalToolsCount(servers),
                AvailableResources = await GetTotalResourcesCount(servers)
            };

            // Generate recent activities
            recentActivities = GenerateRecentActivities(servers, clientApps);
        }
        catch (System.Exception ex)
        {
            // Handle error gracefully
            stats = new DashboardStats();
            recentActivities = new[] { $"Error loading data: {ex.Message}" };
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task<int> GetTotalToolsCount(McpServerInfo[] servers)
    {
        int totalTools = 0;
        foreach (var server in servers.Where(s => s.Enabled && s.IsValid))
        {
            try
            {
                var tools = await McpServerService.GetServerToolsAsync(server.ServerId);
                totalTools += tools.Length;
            }
            catch
            {
                // If unable to get tools, estimate 2 tools per server
                totalTools += 2;
            }
        }
        return totalTools;
    }

    private async Task<int> GetTotalResourcesCount(McpServerInfo[] servers)
    {
        int totalResources = 0;
        foreach (var server in servers.Where(s => s.Enabled && s.IsValid))
        {
            try
            {
                var resources = await McpServerService.GetServerResourcesAsync(server.ServerId);
                totalResources += resources.Length;
            }
            catch
            {
                // If unable to get resources, estimate 1 resource per server
                totalResources += 1;
            }
        }
        return totalResources;
    }

    private string[] GenerateRecentActivities(McpServerInfo[] servers, McpClientConfig[] clientApps)
    {
        var activities = new List<string>();
        
        var enabledServers = servers.Where(s => s.Enabled).Take(3);
        foreach (var server in enabledServers)
        {
            if (server.IsValid)
            {
                activities.Add($"Server '{server.ServerId}' is running ({server.Type})");
            }
            else
            {
                activities.Add($"Server '{server.ServerId}' has configuration issues");
            }
        }

        if (clientApps.Any())
        {
            activities.Add($"Configured {clientApps.Length} client app(s)");
        }

        var disabledCount = servers.Count(s => !s.Enabled);
        if (disabledCount > 0)
        {
            activities.Add($"{disabledCount} server(s) are disabled");
        }

        if (!activities.Any())
        {
            activities.Add("No recent activity - Add your first server to get started");
        }

        return activities.ToArray();
    }

    private void NavigateToServers()
    {
        Navigation.NavigateTo("/mcp/servers");
    }

    private void NavigateToInspector()
    {
        Navigation.NavigateTo("/mcp/inspector");
    }

    private void NavigateToEnvCheck()
    {
        Navigation.NavigateTo("/mcp/env-check");
    }

    private void NavigateToClientApps()
    {
        Navigation.NavigateTo("/mcp/clients");
    }

    private void NavigateToGitHub()
    {
        Navigation.NavigateTo("https://github.com/sheng-jie/Mcp.Links");
    }

    public class DashboardStats
    {
        public int TotalServers { get; set; }
        public int EnabledServers { get; set; }
        public int TotalClientApps { get; set; }
        public int ConnectedServers { get; set; }
        public int AvailableTools { get; set; }
        public int AvailableResources { get; set; }
    }
}
