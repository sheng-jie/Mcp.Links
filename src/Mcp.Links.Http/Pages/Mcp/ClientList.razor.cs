using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AntDesign;
using AntDesign.TableModels;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Mcp.Links.Configuration;
using Mcp.Links.Http.Services;

namespace Mcp.Links.Http.Pages.Mcp;

public partial class ClientListBase : ComponentBase
{
    [Inject] protected IMcpClientAppService McpClientAppService { get; set; } = default!;
    [Inject] protected IMcpServerService McpServerService { get; set; } = default!;
    [Inject] protected MessageService MessageService { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

    protected McpClientConfig[] clientAppData = Array.Empty<McpClientConfig>();
    protected McpServerInfo[] availableServers = Array.Empty<McpServerInfo>();
    protected string clientAppDataKey = Guid.NewGuid().ToString(); // Key to force table re-render
    protected Table<McpClientConfig> clientAppTable = default!;

    protected McpClientConfig[] GetClientAppData()
    {
        return clientAppData;
    }

    protected async Task RefreshClientList()
    {
        isRefreshing = true;
        StateHasChanged();
        
        try
        {
            await LoadClientAppDataAsync();
            MessageService.Success("Client list refreshed successfully.");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to refresh client list: {ex.Message}");
        }
        finally
        {
            isRefreshing = false;
            StateHasChanged();
        }
    }
    
    // Add client app modal state
    protected bool addClientAppModalVisible = false;
    protected bool addClientAppLoading = false;
    protected AddClientAppModel newClientAppModel = new();
    protected string[] selectedServerIds = Array.Empty<string>();
    protected Form<AddClientAppModel> addClientAppForm = default!;

    // Edit client app modal state
    protected bool editClientAppModalVisible = false;
    protected bool editClientAppLoading = false;
    protected EditClientAppModel editClientAppModel = new();
    protected string[] editSelectedServerIds = Array.Empty<string>();
    protected Form<EditClientAppModel> editClientAppForm = default!;

    // Delete client app loading states
    protected readonly Dictionary<string, bool> clientAppDeleteLoading = new();
    
    // Configuration modal state
    protected bool configurationModalVisible = false;
    protected bool loadingConfiguration = false;
    protected bool copyConfigLoading = false;
    protected string configurationContent = "";
    protected string selectedConfigAppId = "";
    protected string selectedConfigServerId = "";
    
    // Refresh state
    protected bool isRefreshing = false;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadClientAppDataAsync(),
            LoadAvailableServersAsync()
        );
    }

    protected async Task LoadClientAppDataAsync()
    {
        try
        {
            var newData = await McpClientAppService.GetAllClientAppsAsync();
            
            // Create a completely new array reference to ensure binding update
            clientAppData = newData.ToArray();
            clientAppDataKey = Guid.NewGuid().ToString(); // Force table re-render
            
            StateHasChanged(); // Ensure UI is updated with new data immediately
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load client app data: {ex.Message}");
            clientAppData = Array.Empty<McpClientConfig>();
            clientAppDataKey = Guid.NewGuid().ToString(); // Force table re-render even on error
            StateHasChanged();
        }
    }

    protected async Task LoadAvailableServersAsync()
    {
        try
        {
            availableServers = await McpServerService.GetAllServersAsync();
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load server data: {ex.Message}");
            availableServers = Array.Empty<McpServerInfo>();
        }
    }

    protected void OnRowClick(RowData<McpClientConfig> row)
    {
    }

    protected void ViewClientAppDetails(string appId)
    {
        // TODO: Navigate to client app details page when created
        MessageService.Info($"Client app details for '{appId}' - Feature coming soon!");
    }

    protected async Task ViewClientConfiguration(string appId, string serverId)
    {
        try
        {
            selectedConfigAppId = appId;
            selectedConfigServerId = serverId;
            configurationModalVisible = true;
            loadingConfiguration = true;
            configurationContent = "";
            StateHasChanged();

            // Generate the configuration
            configurationContent = await McpClientAppService.GenerateClientConfigurationAsync(appId, serverId);
            
            MessageService.Success("Configuration generated successfully.");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to generate configuration: {ex.Message}");
            configurationContent = "Error generating configuration";
        }
        finally
        {
            loadingConfiguration = false;
            StateHasChanged();
        }
    }

    protected void AddNewClientApp()
    {
        // Reset the form and auto-generate AppKey
        newClientAppModel = new AddClientAppModel
        {
            AppKey = Guid.NewGuid().ToString("N") // Auto-generate AppKey when opening the form
        };
        selectedServerIds = Array.Empty<string>();
        addClientAppModalVisible = true;
    }

    protected void GenerateNewAppKey()
    {
        newClientAppModel.AppKey = Guid.NewGuid().ToString("N");
        MessageService.Success("New App Key has been generated.");
    }

    protected void HandleAddClientAppOk()
    {
        if (addClientAppForm != null)
        {
            addClientAppForm.Submit();
        }
    }

    protected void HandleAddClientAppCancel()
    {
        addClientAppModalVisible = false;
    }

    protected async Task OnAddClientAppFormFinish()
    {
        addClientAppLoading = true;
        StateHasChanged(); // Update UI to show loading state
        
        try
        {
            await SaveNewClientApp();
            addClientAppModalVisible = false;
            await LoadClientAppDataAsync(); // Refresh the table
            MessageService.Success($"Client app '{newClientAppModel.Name}' has been added successfully.");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to add client app: {ex.Message}");
        }
        finally
        {
            addClientAppLoading = false;
            StateHasChanged(); // Update UI to clear loading state
        }
    }

    protected void OnAddClientAppFormFinishFailed()
    {
        MessageService.Error("Please fix the validation errors and try again.");
    }

    protected void EditClientApp(string appId)
    {
        // Check if client app is currently being processed
        var isDeleteLoading = clientAppDeleteLoading.GetValueOrDefault(appId, false);
        
        if (isDeleteLoading)
        {
            MessageService.Warning($"Client app '{appId}' is currently being processed. Please wait and try again.");
            return;
        }

        var clientApp = clientAppData.FirstOrDefault(app => app.AppId == appId);
        if (clientApp == null)
        {
            MessageService.Error($"Client app '{appId}' not found.");
            return;
        }

        // Reset the edit form with current client app data
        editClientAppModel = new EditClientAppModel
        {
            AppId = clientApp.AppId,
            AppKey = clientApp.AppKey,
            Name = clientApp.Name,
            Description = clientApp.Description
        };

        // Set up associated servers
        editSelectedServerIds = clientApp.McpServerIds ?? Array.Empty<string>();

        editClientAppModalVisible = true;
    }

    protected void HandleEditClientAppOk()
    {
        if (editClientAppForm != null)
        {
            editClientAppForm.Submit();
        }
    }

    protected void HandleEditClientAppCancel()
    {
        editClientAppModalVisible = false;
    }

    protected async Task OnEditClientAppFormFinish()
    {
        editClientAppLoading = true;
        StateHasChanged(); // Update UI to show loading state
        
        try
        {
            await SaveEditedClientApp();
            editClientAppModalVisible = false;
            await LoadClientAppDataAsync(); // Refresh the table
            MessageService.Success($"Client app '{editClientAppModel.Name}' has been updated successfully.");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to update client app: {ex.Message}");
        }
        finally
        {
            editClientAppLoading = false;
            StateHasChanged(); // Update UI to clear loading state
        }
    }

    protected void OnEditClientAppFormFinishFailed()
    {
        MessageService.Error("Please fix the validation errors and try again.");
    }

    protected void RegenerateAppKey()
    {
        editClientAppModel.AppKey = Guid.NewGuid().ToString().Replace("-", "");
        MessageService.Success("App Key has been regenerated.");
    }

    protected async Task CopyAppKey(string appKey)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", appKey);
            MessageService.Success("App Key copied to clipboard!");
        }
        catch (System.Exception)
        {
            // Fallback for older browsers or when clipboard API is not available
            MessageService.Warning("Unable to copy to clipboard. Please copy manually: " + appKey);
        }
    }

    protected async Task DeleteClientApp(string appId)
    {
        try
        {
            // Set loading state for this specific client app
            clientAppDeleteLoading[appId] = true;
            StateHasChanged(); // Refresh UI to show loading state

            // Remove using the service
            await McpClientAppService.DeleteClientAppAsync(appId);
            // Refresh the data to reflect changes
            await LoadClientAppDataAsync();
            MessageService.Success($"Client app '{appId}' has been deleted successfully.");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to delete client app: {ex.Message}");
        }
        finally
        {
            // Clear loading state for this specific client app
            clientAppDeleteLoading[appId] = false;
            StateHasChanged(); // Refresh UI to clear loading state
        }
    }

    protected async Task SaveNewClientApp()
    {
        // Validate the model
        if (string.IsNullOrWhiteSpace(newClientAppModel.AppId))
            throw new ArgumentException("App ID is required");

        if (string.IsNullOrWhiteSpace(newClientAppModel.Name))
            throw new ArgumentException("Name is required");

        if (string.IsNullOrWhiteSpace(newClientAppModel.AppKey))
            throw new ArgumentException("App Key is required");

        // Check if client app ID already exists
        if (await McpClientAppService.ClientAppExistsAsync(newClientAppModel.AppId))
            throw new ArgumentException($"Client app with ID '{newClientAppModel.AppId}' already exists");

        // Create the new client app using the generated AppKey
        var clientApp = new McpClientConfig
        {
            AppId = newClientAppModel.AppId,
            AppKey = newClientAppModel.AppKey, // Use the AppKey from the model (already generated)
            Name = newClientAppModel.Name,
            Description = newClientAppModel.Description,
            McpServerIds = selectedServerIds ?? Array.Empty<string>()
        };

        // Create the client app using the service
        await McpClientAppService.CreateClientAppAsync(clientApp);
    }

    protected async Task SaveEditedClientApp()
    {
        // Validate the model
        if (string.IsNullOrWhiteSpace(editClientAppModel.AppId))
            throw new ArgumentException("App ID is required");

        if (string.IsNullOrWhiteSpace(editClientAppModel.AppKey))
            throw new ArgumentException("App Key is required");

        if (string.IsNullOrWhiteSpace(editClientAppModel.Name))
            throw new ArgumentException("Name is required");

        // Create the updated client app
        var clientApp = new McpClientConfig
        {
            AppId = editClientAppModel.AppId,
            AppKey = editClientAppModel.AppKey,
            Name = editClientAppModel.Name,
            Description = editClientAppModel.Description,
            McpServerIds = editSelectedServerIds ?? Array.Empty<string>()
        };

        // Update the client app using the service
        await McpClientAppService.UpdateClientAppAsync(editClientAppModel.AppId, clientApp);
    }

    protected string MaskAppKey(string appKey)
    {
        if (string.IsNullOrEmpty(appKey))
            return "";
        
        if (appKey.Length <= 8)
            return new string('*', appKey.Length);
        
        return appKey[..4] + new string('*', appKey.Length - 8) + appKey[^4..];
    }

    protected string GetServerTypeColor(string type)
    {
        return type switch
        {
            "stdio" => "blue",
            "sse" => "green",
            "http" => "orange",
            _ => "default"
        };
    }

    protected bool GetServerSelected(string serverId)
    {
        return selectedServerIds?.Contains(serverId) ?? false;
    }

    protected void OnServerSelectionChanged(string serverId, bool isSelected)
    {
        var currentList = selectedServerIds?.ToList() ?? new List<string>();
        
        if (isSelected && !currentList.Contains(serverId))
        {
            currentList.Add(serverId);
        }
        else if (!isSelected && currentList.Contains(serverId))
        {
            currentList.Remove(serverId);
        }
        
        selectedServerIds = currentList.ToArray();
    }

    protected bool GetEditServerSelected(string serverId)
    {
        return editSelectedServerIds?.Contains(serverId) ?? false;
    }

    protected void OnEditServerSelectionChanged(string serverId, bool isSelected)
    {
        var currentList = editSelectedServerIds?.ToList() ?? new List<string>();
        
        if (isSelected && !currentList.Contains(serverId))
        {
            currentList.Add(serverId);
        }
        else if (!isSelected && currentList.Contains(serverId))
        {
            currentList.Remove(serverId);
        }
        
        editSelectedServerIds = currentList.ToArray();
    }

    protected void HandleConfigurationCancel()
    {
        configurationModalVisible = false;
    }

    protected async Task CopyConfigurationToClipboard()
    {
        if (string.IsNullOrEmpty(configurationContent))
        {
            MessageService.Warning("No configuration to copy.");
            return;
        }

        copyConfigLoading = true;
        StateHasChanged();

        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", configurationContent);
            MessageService.Success("Configuration copied to clipboard successfully!");
        }
        catch (System.Exception)
        {
            MessageService.Warning("Unable to copy to clipboard. Please copy manually.");
        }
        finally
        {
            copyConfigLoading = false;
            StateHasChanged();
        }
    }

    public class AddClientAppModel
    {
        [Required(ErrorMessage = "App ID is required")]
        [DisplayName("App ID")]
        public string AppId { get; set; } = "";

        [DisplayName("App Key")]
        public string AppKey { get; set; } = "";

        [Required(ErrorMessage = "Name is required")]
        [DisplayName("Name")]
        public string Name { get; set; } = "";

        [DisplayName("Description")]
        public string? Description { get; set; }
    }

    public class EditClientAppModel
    {
        [Required(ErrorMessage = "App ID is required")]
        [DisplayName("App ID")]
        public string AppId { get; set; } = "";

        [Required(ErrorMessage = "App Key is required")]
        [DisplayName("App Key")]
        public string AppKey { get; set; } = "";

        [Required(ErrorMessage = "Name is required")]
        [DisplayName("Name")]
        public string Name { get; set; } = "";

        [DisplayName("Description")]
        public string? Description { get; set; }
    }
}
