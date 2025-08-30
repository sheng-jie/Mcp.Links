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
using Microsoft.JSInterop;

namespace Mcp.Links.Http.Pages.Mcp
{
    public partial class ServerListBase : ComponentBase
    {
        [Inject] protected IMcpServerService McpServerService { get; set; } = default!;
        [Inject] protected IMessageService MessageService { get; set; } = default!;
        [Inject] protected IConfiguration Configuration { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected McpServerInfo[] serverData = Array.Empty<McpServerInfo>();
        
        // Add server modal state
        protected bool addServerModalVisible = false;
        protected bool addServerLoading = false;
        protected AddServerModel newServerModel = new();
        protected List<EnvironmentVariable> environmentVariables = new();
        protected List<HeaderVariable> headers = new();
        protected string argumentsText = "";
        protected Form<AddServerModel> addServerForm = default!;

        // Edit server modal state
        protected bool editServerModalVisible = false;
        protected bool editServerLoading = false;
        protected EditServerModel editServerModel = new();
        protected List<EnvironmentVariable> editEnvironmentVariables = new();
        protected List<HeaderVariable> editHeaders = new();
        protected string editArgumentsText = "";
        protected Form<EditServerModel> editServerForm = default!;

        // Toggle server status loading states
        protected readonly Dictionary<string, bool> serverToggleLoading = new();
        
        // Delete server loading states
        protected readonly Dictionary<string, bool> serverDeleteLoading = new();

        // MCP JSON modal state
        protected bool mcpJsonModalVisible = false;
        protected bool loadingMcpJson = false;
        protected bool copyLoading = false;
        protected bool refreshJsonLoading = false;
        protected string mcpJsonContent = "";

        protected override async Task OnInitializedAsync()
        {
            await LoadServerDataAsync();
        }

        protected async Task LoadServerDataAsync()
        {
            try
            {
                serverData = await McpServerService.GetAllServersAsync();
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load server data: {ex.Message}");
                serverData = Array.Empty<McpServerInfo>();
            }
        }

        protected void OnRowClick(RowData<McpServerInfo> row)
        {
            
        }

        protected void ViewServerDetails(string serverId)
        {
            Navigation.NavigateTo($"/mcp/servers/{serverId}");
        }

        protected void InspectServer(string serverId)
        {
            Navigation.NavigateTo($"/mcp/inspector?server={serverId}");
        }

        protected async void ToggleServerStatus(string serverId)
        {
            try
            {
                var server = serverData.FirstOrDefault(s => s.ServerId == serverId);
                if (server == null)
                {
                    MessageService.Error($"Server '{serverId}' not found.");
                    return;
                }

                // Set loading state for this specific server
                serverToggleLoading[serverId] = true;
                StateHasChanged(); // Refresh UI to show loading state

                var newStatus = !server.Enabled;
                
                // Update the server status using the service
                await McpServerService.ToggleServerStatusAsync(serverId, newStatus);
                
                // Refresh the data to reflect changes
                await LoadServerDataAsync();
                
                var statusText = newStatus ? "enabled" : "disabled";
                MessageService.Success($"Server '{serverId}' has been {statusText}.");
                
                StateHasChanged();
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to update server status: {ex.Message}");
            }
            finally
            {
                // Clear loading state for this specific server
                serverToggleLoading[serverId] = false;
                StateHasChanged(); // Refresh UI to clear loading state
            }
        }

        protected void EditServer(string serverId)
        {
            // Check if server is currently being processed
            var isToggleLoading = serverToggleLoading.GetValueOrDefault(serverId, false);
            var isDeleteLoading = serverDeleteLoading.GetValueOrDefault(serverId, false);
            
            if (isToggleLoading || isDeleteLoading)
            {
                MessageService.Warning($"Server '{serverId}' is currently being processed. Please wait and try again.");
                return;
            }

            var server = serverData.FirstOrDefault(s => s.ServerId == serverId);
            if (server == null)
            {
                MessageService.Error($"Server '{serverId}' not found.");
                return;
            }

            // Reset the edit form with current server data
            editServerModel = new EditServerModel
            {
                ServerId = server.ServerId,
                Type = server.Type,
                Command = server.Command,
                Url = server.Url,
                Enabled = server.Enabled
            };

            // Set up environment variables
            editEnvironmentVariables.Clear();
            if (server.Environment != null)
            {
                foreach (var kvp in server.Environment)
                {
                    editEnvironmentVariables.Add(new EnvironmentVariable { Key = kvp.Key, Value = kvp.Value });
                }
            }

            // Set up headers
            editHeaders.Clear();
            if (server.Headers != null)
            {
                foreach (var kvp in server.Headers)
                {
                    editHeaders.Add(new HeaderVariable { Key = kvp.Key, Value = kvp.Value });
                }
            }

            // Set up arguments
            editArgumentsText = server.Args != null ? string.Join('\n', server.Args) : "";

            editServerModalVisible = true;
        }

        protected async void DeleteServer(string serverId)
        {
            try
            {
                // Set loading state for this specific server
                serverDeleteLoading[serverId] = true;
                StateHasChanged(); // Refresh UI to show loading state

                // Remove using the service
                await McpServerService.DeleteServerAsync(serverId);
                
                // Refresh the data to reflect changes
                await LoadServerDataAsync();
                
                MessageService.Success($"Server '{serverId}' has been deleted successfully.");
                StateHasChanged();
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to delete server: {ex.Message}");
            }
            finally
            {
                // Clear loading state for this specific server
                serverDeleteLoading[serverId] = false;
                StateHasChanged(); // Refresh UI to clear loading state
            }
        }

        protected void AddNewServer()
        {
            // Reset the form
            newServerModel = new AddServerModel();
            environmentVariables.Clear();
            headers.Clear();
            argumentsText = "";
            addServerModalVisible = true;
        }

        protected void HandleAddServerOk()
        {
            if (addServerForm != null)
            {
                addServerForm.Submit();
            }
        }

        protected void HandleAddServerCancel()
        {
            addServerModalVisible = false;
        }

        protected async Task OnAddServerFormFinish()
        {
            addServerLoading = true;
            try
            {
                await SaveNewServer();
                addServerModalVisible = false;
                MessageService.Success($"Server '{newServerModel.ServerId}' has been added successfully.");
                await LoadServerDataAsync(); // Refresh the table
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to add server: {ex.Message}");
            }
            finally
            {
                addServerLoading = false;
            }
        }

        protected void OnAddServerFormFinishFailed()
        {
            MessageService.Error("Please fix the validation errors and try again.");
        }

        protected void OnServerTypeChanged(string value)
        {
            newServerModel.Type = value;
            // Clear fields that are not relevant for the selected type
            if (value != "stdio")
            {
                newServerModel.Command = null;
                argumentsText = "";
                environmentVariables.Clear(); // Clear environment variables for non-stdio types
            }
            if (value == "stdio")
            {
                newServerModel.Url = null;
                headers.Clear(); // Clear headers for stdio type since they're only for HTTP/SSE
            }
        }

        protected void AddEnvironmentVariable()
        {
            environmentVariables.Add(new EnvironmentVariable());
        }

        protected void RemoveEnvironmentVariable(int index)
        {
            if (index >= 0 && index < environmentVariables.Count)
            {
                environmentVariables.RemoveAt(index);
            }
        }

        protected void AddHeader()
        {
            headers.Add(new HeaderVariable());
        }

        protected void RemoveHeader(int index)
        {
            if (index >= 0 && index < headers.Count)
            {
                headers.RemoveAt(index);
            }
        }

        protected async Task SaveNewServer()
        {
            // Validate the model
            if (string.IsNullOrWhiteSpace(newServerModel.ServerId))
                throw new ArgumentException("Server ID is required");

            if (string.IsNullOrWhiteSpace(newServerModel.Type))
                throw new ArgumentException("Server type is required");

            // Check if server ID already exists
            if (await McpServerService.ServerExistsAsync(newServerModel.ServerId))
                throw new ArgumentException($"Server with ID '{newServerModel.ServerId}' already exists");

            // Create the new server config
            var serverConfig = new McpServerConfig
            {
                Type = newServerModel.Type,
                Enabled = newServerModel.Enabled
            };

            // Set type-specific fields
            if (newServerModel.Type == "stdio")
            {
                serverConfig.Command = newServerModel.Command;
                
                // Set arguments if provided
                if (!string.IsNullOrWhiteSpace(argumentsText))
                {
                    serverConfig.Args = argumentsText
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(arg => arg.Trim())
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .ToArray();
                }
            }
            else if (newServerModel.Type == "sse" || newServerModel.Type == "http")
            {
                serverConfig.Url = newServerModel.Url;
            }

            // Set environment variables if provided
            if (environmentVariables.Any(ev => !string.IsNullOrWhiteSpace(ev.Key)))
            {
                serverConfig.Env = environmentVariables
                    .Where(ev => !string.IsNullOrWhiteSpace(ev.Key))
                    .ToDictionary(ev => ev.Key, ev => ev.Value ?? "");
            }

            // Set headers if provided
            if (headers.Any(h => !string.IsNullOrWhiteSpace(h.Key)))
            {
                serverConfig.Headers = headers
                    .Where(h => !string.IsNullOrWhiteSpace(h.Key))
                    .ToDictionary(h => h.Key, h => h.Value ?? "");
            }

            // Create the server using the service
            await McpServerService.CreateServerAsync(newServerModel.ServerId, serverConfig);
        }

        // Edit server modal handlers
        protected void HandleEditServerOk()
        {
            if (editServerForm != null)
            {
                editServerForm.Submit();
            }
        }

        protected void HandleEditServerCancel()
        {
            editServerModalVisible = false;
        }

        protected async Task OnEditServerFormFinish()
        {
            editServerLoading = true;
            try
            {
                await SaveEditedServer();
                editServerModalVisible = false;
                MessageService.Success($"Server '{editServerModel.ServerId}' has been updated successfully.");
                await LoadServerDataAsync(); // Refresh the table
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to update server: {ex.Message}");
            }
            finally
            {
                editServerLoading = false;
            }
        }

        protected void OnEditServerFormFinishFailed()
        {
            MessageService.Error("Please fix the validation errors and try again.");
        }

        protected void OnEditServerTypeChanged(string value)
        {
            editServerModel.Type = value;
            // Clear fields that are not relevant for the selected type
            if (value != "stdio")
            {
                editServerModel.Command = null;
                editArgumentsText = "";
                editEnvironmentVariables.Clear(); // Clear environment variables for non-stdio types
            }
            if (value == "stdio")
            {
                editServerModel.Url = null;
                editHeaders.Clear(); // Clear headers for stdio type since they're only for HTTP/SSE
            }
        }

        protected void AddEditEnvironmentVariable()
        {
            editEnvironmentVariables.Add(new EnvironmentVariable());
        }

        protected void RemoveEditEnvironmentVariable(int index)
        {
            if (index >= 0 && index < editEnvironmentVariables.Count)
            {
                editEnvironmentVariables.RemoveAt(index);
            }
        }

        protected void AddEditHeader()
        {
            editHeaders.Add(new HeaderVariable());
        }

        protected void RemoveEditHeader(int index)
        {
            if (index >= 0 && index < editHeaders.Count)
            {
                editHeaders.RemoveAt(index);
            }
        }

        protected async Task SaveEditedServer()
        {
            // Validate the model
            if (string.IsNullOrWhiteSpace(editServerModel.ServerId))
                throw new ArgumentException("Server ID is required");

            if (string.IsNullOrWhiteSpace(editServerModel.Type))
                throw new ArgumentException("Server type is required");

            // Create the updated server config
            var serverConfig = new McpServerConfig
            {
                Type = editServerModel.Type,
                Enabled = editServerModel.Enabled
            };

            // Set type-specific fields
            if (editServerModel.Type == "stdio")
            {
                serverConfig.Command = editServerModel.Command;
                
                // Set arguments if provided
                if (!string.IsNullOrWhiteSpace(editArgumentsText))
                {
                    serverConfig.Args = editArgumentsText
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(arg => arg.Trim())
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .ToArray();
                }
            }
            else if (editServerModel.Type == "sse" || editServerModel.Type == "http")
            {
                serverConfig.Url = editServerModel.Url;
            }

            // Set environment variables if provided
            if (editEnvironmentVariables.Any(ev => !string.IsNullOrWhiteSpace(ev.Key)))
            {
                serverConfig.Env = editEnvironmentVariables
                    .Where(ev => !string.IsNullOrWhiteSpace(ev.Key))
                    .ToDictionary(ev => ev.Key, ev => ev.Value ?? "");
            }

            // Set headers if provided
            if (editHeaders.Any(h => !string.IsNullOrWhiteSpace(h.Key)))
            {
                serverConfig.Headers = editHeaders
                    .Where(h => !string.IsNullOrWhiteSpace(h.Key))
                    .ToDictionary(h => h.Key, h => h.Value ?? "");
            }

            // Update the server using the service
            await McpServerService.UpdateServerAsync(editServerModel.ServerId, serverConfig);
        }

        // MCP JSON modal handlers
        protected async void ShowMcpJsonModal()
        {
            mcpJsonModalVisible = true;
            loadingMcpJson = true;
            mcpJsonContent = "";
            StateHasChanged();

            try
            {
                mcpJsonContent = await McpServerService.GetMcpJsonContentAsync();
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to load mcp.json content: {ex.Message}");
                mcpJsonContent = "Error loading content";
            }
            finally
            {
                loadingMcpJson = false;
                StateHasChanged();
            }
        }

        protected void HandleMcpJsonCancel()
        {
            mcpJsonModalVisible = false;
        }

        protected async void RefreshMcpJson()
        {
            refreshJsonLoading = true;
            StateHasChanged();

            try
            {
                mcpJsonContent = await McpServerService.GetMcpJsonContentAsync();
                MessageService.Success("mcp.json content refreshed successfully.");
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to refresh mcp.json content: {ex.Message}");
            }
            finally
            {
                refreshJsonLoading = false;
                StateHasChanged();
            }
        }

        protected async void CopyMcpJsonToClipboard()
        {
            if (string.IsNullOrEmpty(mcpJsonContent))
            {
                MessageService.Warning("No content to copy.");
                return;
            }

            copyLoading = true;
            StateHasChanged();

            try
            {
                // Use JavaScript interop to copy to clipboard
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", mcpJsonContent);
                MessageService.Success("Content copied to clipboard successfully!");
            }
            catch (System.Exception ex)
            {
                MessageService.Error($"Failed to copy to clipboard: {ex.Message}");
            }
            finally
            {
                copyLoading = false;
                StateHasChanged();
            }
        }

        public class AddServerModel
        {
            [Required(ErrorMessage = "Server ID is required")]
            [DisplayName("Server ID")]
            public string ServerId { get; set; } = "";

            [Required(ErrorMessage = "Server type is required")]
            [DisplayName("Server Type")]
            public string Type { get; set; } = "";

            [DisplayName("Command")]
            public string? Command { get; set; }

            [DisplayName("URL")]
            public string? Url { get; set; }

            [DisplayName("Enabled")]
            public bool Enabled { get; set; } = true;
        }

        public class EditServerModel
        {
            [Required(ErrorMessage = "Server ID is required")]
            [DisplayName("Server ID")]
            public string ServerId { get; set; } = "";

            [Required(ErrorMessage = "Server type is required")]
            [DisplayName("Server Type")]
            public string Type { get; set; } = "";

            [DisplayName("Command")]
            public string? Command { get; set; }

            [DisplayName("URL")]
            public string? Url { get; set; }

            [DisplayName("Enabled")]
            public bool Enabled { get; set; } = true;
        }

        public class EnvironmentVariable
        {
            public string Key { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public class HeaderVariable
        {
            public string Key { get; set; } = "";
            public string Value { get; set; } = "";
        }
    }
}
