using AntDesign.Extensions.Localization;
using AntDesign.ProLayout;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net.Http.Json;

namespace Mcp.Links.Http.Layouts
{
    public partial class BasicLayout : LayoutComponentBase, IDisposable
    {
        private MenuDataItem[] _menuData = Array.Empty<MenuDataItem>();

        [Inject] private ReuseTabsService TabService { get; set; } = null!;

        protected override void OnInitialized()
        {
            _menuData = new[] {
                new MenuDataItem
                {
                    Path = "/",
                    Name = "Welcome",
                    Key = "welcome",
                    Icon = "smile",
                },
                new MenuDataItem
                {
                    Path = "/mcp/store",
                    Name = "Store",
                    Key = "mcp-store",
                    Icon = "shop",
                },
                new MenuDataItem
                {
                    Path = "/mcp/servers",
                    Name = "Servers",
                    Key = "mcp-servers",
                    Icon = "hdd",
                },
                new MenuDataItem
                {
                    Path = "/mcp/clients",
                    Name = "Clients",
                    Key = "mcp-client-apps",
                    Icon = "appstore",
                },
                new MenuDataItem
                {
                    Path = "/mcp/inspector",
                    Name = "Inspector",
                    Key = "mcp-inspector",
                    Icon = "bug",
                },
                new MenuDataItem
                {
                    Path = "/mcp/env-check",
                    Name = "Environment Check",
                    Key = "mcp-env-check",
                    Icon = "check-circle",
                }
                // new MenuDataItem
                // {
                //     Path = "/mcp",
                //     Name = "MCP",
                //     Key = "mcp",
                //     Icon = "api",
                //     Children = new[]
                //     {
                //         new MenuDataItem
                //         {
                //             Path = "/mcp/servers",
                //             Name = "Servers",
                //             Key = "mcp-servers",
                //             Icon = "hdd",
                //         },
                //         new MenuDataItem
                //         {
                //             Path = "/mcp/clients",
                //             Name = "Clients",
                //             Key = "mcp-client-apps",
                //             Icon = "appstore",
                //         },
                //         new MenuDataItem
                //         {
                //             Path = "/mcp/inspector",
                //             Name = "Inspector",
                //             Key = "mcp-inspector",
                //             Icon = "bug",
                //         }
                //     }
                // }
            };
        }
        void Reload()
        {
            TabService.ReloadPage();
        }

        public void Dispose()
        {

        }

    }
}
