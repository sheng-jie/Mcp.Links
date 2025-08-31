using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using AntDesign;
using System.ComponentModel.DataAnnotations;
using Mcp.Links.Http.Services;
using Markdig;

namespace Mcp.Links.Http.Pages.Mcp
{
    public class StoreBase : ComponentBase, IDisposable
    {
        [Inject] protected ILogger<StoreBase> Logger { get; set; } = null!;
        [Inject] protected MessageService MessageService { get; set; } = null!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] protected IWebHostEnvironment Environment { get; set; } = null!;
        [Inject] protected IMcpStoreService McpStoreService { get; set; } = null!;
        [Inject] protected NavigationManager Navigation { get; set; } = null!;

        protected List<McpStoreItem> storeItems = new();
        protected List<McpStoreItem> filteredStoreItems = new();
        protected McpStoreItem? selectedItem;
        protected McpStoreInstallationInfo? installationInfo;
        protected McpStoreValidationResult? validationResult;
        protected string searchText = string.Empty;
        protected string customServerId = string.Empty;
        
        protected bool loading = true;
        protected bool refreshLoading = false;
        protected bool copyConfigLoading = false;
        protected bool installLoading = false;
        
        protected bool detailModalVisible = false;
        protected bool configModalVisible = false;
        protected bool installModalVisible = false;
        
        protected int currentPage = 1;
        protected int pageSize = 18;
        protected int totalItems = 0;

        private bool _disposed = false;

        // Markdown pipeline for converting markdown to HTML
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        protected override async Task OnInitializedAsync()
        {
            await LoadStoreData();
        }

        protected async Task LoadStoreData()
        {
            try
            {
                loading = true;
                StateHasChanged();

                var storeFilePath = Path.Combine(Environment.ContentRootPath, "mcp-store.json");
                
                if (File.Exists(storeFilePath))
                {
                    var json = await File.ReadAllTextAsync(storeFilePath);
                    var items = JsonSerializer.Deserialize<List<McpStoreItem>>(json);
                    
                    if (items != null)
                    {
                        // Filter out invalid items and the last item which seems to be a count
                        storeItems = items.Where(item => 
                            !string.IsNullOrEmpty(item.Title) && 
                            item.Title != "4 results found" &&
                            !string.IsNullOrEmpty(item.Address)).ToList();
                        
                        ApplyFilter();
                    }
                }
                else
                {
                    Logger.LogWarning("Store file not found: {FilePath}", storeFilePath);
                    storeItems = new List<McpStoreItem>();
                    filteredStoreItems = new List<McpStoreItem>();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error loading store data");
                MessageService.Error("Failed to load store data. Please try again.");
            }
            finally
            {
                loading = false;
                StateHasChanged();
            }
        }

        protected void ApplyFilter()
        {
            var filtered = storeItems.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchTerm = searchText.ToLowerInvariant();
                filtered = filtered.Where(item =>
                    (item.Title?.ToLowerInvariant().Contains(searchTerm) == true) ||
                    (item.Author?.ToLowerInvariant().Contains(searchTerm) == true) ||
                    (item.Profile?.ToLowerInvariant().Contains(searchTerm) == true));
            }
            
            totalItems = filtered.Count();
            
            // Apply pagination
            var startIndex = (currentPage - 1) * pageSize;
            filteredStoreItems = filtered.Skip(startIndex).Take(pageSize).ToList();
            
            StateHasChanged();
        }

        protected void OnSearchInput(ChangeEventArgs e)
        {
            searchText = e.Value?.ToString() ?? string.Empty;
            currentPage = 1; // Reset to first page when searching
            ApplyFilter();
        }

        protected void OnPageChanged(PaginationEventArgs e)
        {
            currentPage = e.Page;
            ApplyFilter();
        }

        protected async Task RefreshStore()
        {
            refreshLoading = true;
            StateHasChanged();
            
            await LoadStoreData();
            
            refreshLoading = false;
            StateHasChanged();
            
            MessageService.Success("Store data refreshed successfully!");
        }

        protected void ViewDetails(McpStoreItem item)
        {
            selectedItem = item;
            detailModalVisible = true;
            StateHasChanged();
        }

        protected void ViewConfig(McpStoreItem item)
        {
            selectedItem = item;
            configModalVisible = true;
            StateHasChanged();
        }

        protected async Task InstallServer(McpStoreItem item)
        {
            try
            {
                selectedItem = item;
                
                // Parse installation info and validate
                installationInfo = await McpStoreService.ParseInstallationInfoAsync(item);
                validationResult = await McpStoreService.ValidateStoreItemAsync(item);
                
                // Pre-fill server ID
                customServerId = installationInfo.GeneratedServerId;
                
                installModalVisible = true;
                StateHasChanged();
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error preparing installation for {Title}", item.Title);
                MessageService.Error($"Failed to prepare installation: {ex.Message}");
            }
        }

        protected void HandleDetailCancel()
        {
            detailModalVisible = false;
            selectedItem = null;
            StateHasChanged();
        }

        protected void HandleConfigCancel()
        {
            configModalVisible = false;
            selectedItem = null;
            StateHasChanged();
        }

        protected void HandleInstallCancel()
        {
            installModalVisible = false;
            selectedItem = null;
            installationInfo = null;
            validationResult = null;
            customServerId = string.Empty;
            StateHasChanged();
        }

        protected async Task CopyConfigToClipboard()
        {
            if (selectedItem?.Config == null || _disposed) return;

            try
            {
                copyConfigLoading = true;
                if (!_disposed) StateHasChanged();

                // Check if the component is still rendered before making JS calls
                if (JSRuntime != null && !_disposed)
                {
                    await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", selectedItem.Config);
                    if (!_disposed) MessageService.Success("Configuration copied to clipboard!");
                }
            }
            catch (Microsoft.JSInterop.JSDisconnectedException)
            {
                // Circuit has been disconnected, ignore this exception
                Logger.LogDebug("JavaScript interop call failed because circuit was disconnected");
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error copying configuration to clipboard");
                if (!_disposed) MessageService.Error("Failed to copy configuration to clipboard.");
            }
            finally
            {
                copyConfigLoading = false;
                if (!_disposed) StateHasChanged();
            }
        }

        protected async Task ConfirmInstall()
        {
            if (selectedItem == null || installationInfo == null) return;

            try
            {
                installLoading = true;
                StateHasChanged();

                // Validate that we can install
                if (validationResult?.IsValid != true)
                {
                    MessageService.Error($"Cannot install server: {string.Join(", ", validationResult?.Errors ?? new List<string>())}");
                    return;
                }

                // Install the server
                var installedServerId = await McpStoreService.InstallServerFromStoreAsync(
                    selectedItem, 
                    string.IsNullOrWhiteSpace(customServerId) ? null : customServerId.Trim());

                MessageService.Success($"Successfully installed '{selectedItem.Title}' as server '{installedServerId}'!");
                
                installModalVisible = false;
                selectedItem = null;
                installationInfo = null;
                validationResult = null;
                customServerId = string.Empty;
                
                // Navigate to the server detail page
                Navigation.NavigateTo($"/mcp/servers/{installedServerId}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error installing server");
                MessageService.Error($"Failed to install server: {ex.Message}");
            }
            finally
            {
                installLoading = false;
                StateHasChanged();
            }
        }

        protected async Task OpenLink(string url)
        {
            if (_disposed) return;

            try
            {
                // Check if the component is still rendered before making JS calls
                if (JSRuntime != null && !_disposed)
                {
                    await JSRuntime.InvokeVoidAsync("open", url, "_blank");
                }
            }
            catch (Microsoft.JSInterop.JSDisconnectedException)
            {
                // Circuit has been disconnected, ignore this exception
                Logger.LogDebug("JavaScript interop call failed because circuit was disconnected");
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error opening link: {Url}", url);
                if (!_disposed) MessageService.Warning("Could not open link. Please copy the URL manually.");
            }
        }

        protected RenderFragment[] GetCardActions(McpStoreItem item)
        {
            return new RenderFragment[]
            {
                CreateActionButton("eye", "View Details", () => ViewDetails(item)),
                CreateActionButton("code", "View Config", () => ViewConfig(item)),
                CreateActionButton("download", "Install", async () => await InstallServer(item))
            };
        }

        private RenderFragment CreateActionButton(string iconType, string tooltip, Action onClick)
        {
            return builder =>
            {
                builder.OpenComponent<Tooltip>(0);
                builder.AddAttribute(1, "Title", tooltip);
                builder.AddAttribute(2, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<Button>(0);
                    childBuilder.AddAttribute(1, "Type", ButtonType.Text);
                    childBuilder.AddAttribute(2, "Icon", iconType);
                    childBuilder.AddAttribute(3, "OnClick", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, _ => onClick()));
                    childBuilder.AddAttribute(4, "Size", ButtonSize.Small);
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };
        }

        protected string ConvertMarkdownToHtml(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;
                
            try
            {
                return Markdown.ToHtml(markdown, _markdownPipeline);
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error converting markdown to HTML");
                // Fallback to plain text
                return markdown;
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
