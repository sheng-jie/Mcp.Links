using System;
using System.ComponentModel;
using AntDesign.TableModels;
using AntDesign;
using global::Mcp.Links.Configuration;
using global::Mcp.Links.Http.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace Mcp.Links.Http.Pages.Mcp
{
    public partial class ServerDetailBase : ComponentBase
    {
        [Parameter] public string ServerId { get; set; } = "";

        [Inject] protected IMcpServerService McpServerService { get; set; } = default!;
        [Inject] protected IMessageService MessageService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;

        protected McpServerInfo? serverInfo;
        protected McpToolInfo[] tools = Array.Empty<McpToolInfo>();
        protected McpPromptInfo[] prompts = Array.Empty<McpPromptInfo>();
        protected McpResourceInfo[] resources = Array.Empty<McpResourceInfo>();

        protected bool loading = true;
        protected bool loadingTools = false;
        protected bool loadingPrompts = false;
        protected bool loadingResources = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadServerData();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!string.IsNullOrEmpty(ServerId))
            {
                await LoadServerData();
            }
        }

        protected async Task LoadServerData()
        {
            loading = true;
            try
            {
                serverInfo = await McpServerService.GetServerAsync(ServerId);
                if (serverInfo != null)
                {
                    await LoadAllData();
                }
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load server data: {ex.Message}");
            }
            finally
            {
                loading = false;
            }
        }

        protected async Task LoadAllData()
        {
            await Task.WhenAll(
                LoadTools(),
                LoadPrompts(),
                LoadResources()
            );
        }

        protected async Task LoadTools()
        {
            loadingTools = true;
            try
            {
                tools = await McpServerService.GetServerToolsAsync(ServerId);
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load tools: {ex.Message}");
                tools = Array.Empty<McpToolInfo>();
            }
            finally
            {
                loadingTools = false;
            }
        }

        protected async Task LoadPrompts()
        {
            loadingPrompts = true;
            try
            {
                prompts = await McpServerService.GetServerPromptsAsync(ServerId);
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load prompts: {ex.Message}");
                prompts = Array.Empty<McpPromptInfo>();
            }
            finally
            {
                loadingPrompts = false;
            }
        }

        protected async Task LoadResources()
        {
            loadingResources = true;
            try
            {
                resources = await McpServerService.GetServerResourcesAsync(ServerId);
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load resources: {ex.Message}");
                resources = Array.Empty<McpResourceInfo>();
            }
            finally
            {
                loadingResources = false;
            }
        }

        protected async Task RefreshData()
        {
            await LoadServerData();
            MessageService.Success("Server data refreshed successfully.");
        }

        protected async Task RefreshTools()
        {
            await LoadTools();
            StateHasChanged();
        }

        protected async Task RefreshPrompts()
        {
            await LoadPrompts();
            StateHasChanged();
        }

        protected async Task RefreshResources()
        {
            await LoadResources();
            StateHasChanged();
        }

        protected void GoBack()
        {
            Navigation.NavigateTo("/mcp/servers");
        }

        protected void OnCollapseChange(string[] keys)
        {
        }
    }
}
