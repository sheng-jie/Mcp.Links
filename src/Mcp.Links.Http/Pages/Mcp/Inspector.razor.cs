using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Encodings.Web;
using Mcp.Links.Http.Services;

namespace Mcp.Links.Http.Pages.Mcp;

public class ArgumentInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; } = "string";
    public bool IsRequired { get; set; }
}

public partial class Inspector
{
    [Parameter, SupplyParameterFromQuery] public string? Server { get; set; }
    
    // Data properties
    private McpServerInfo[] availableServers = Array.Empty<McpServerInfo>();
    private string selectedServerId = "";
    private string activeTabKey = "tools";
    
    // Connection testing
    private bool isTestingConnection = false;
    private McpConnectionTestResult? connectionTestResult;
    
    // Tools
    private bool isLoadingTools = false;
    private McpInspectorTool[]? tools;
    private bool toolDialogVisible = false;
    private McpInspectorTool? selectedTool;
    private string toolParametersJson = "";
    private bool isCallingTool = false;
    private McpToolCallResult? toolCallResult;
    
    // Resources
    private bool isLoadingResources = false;
    private McpInspectorResource[]? resources;
    private bool resourceDialogVisible = false;
    private McpInspectorResource? selectedResource;
    private bool isLoadingResourceContent = false;
    private McpResourceContent? resourceContent;
    private string? resourceContentError;
    private bool isCopyingResource = false;
    
    // Prompts
    private bool isLoadingPrompts = false;
    private McpInspectorPrompt[]? prompts;
    private bool promptDialogVisible = false;
    private McpInspectorPrompt? selectedPrompt;
    private string promptArgumentsJson = "";
    private Dictionary<string, string> promptArgumentValues = new();
    private bool isGettingPrompt = false;
    private McpPromptContent? promptResult;
    private string? promptError;
    
    // Export
    private bool isExportingEntry = false;
    private bool isExportingComplete = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableServers();
    }

    protected override async Task OnParametersSetAsync()
    {
        // If a server parameter is provided via URL, pre-select it
        if (!string.IsNullOrEmpty(Server) && availableServers.Any())
        {
            var serverExists = availableServers.Any(s => s.ServerId == Server);
            if (serverExists && selectedServerId != Server)
            {
                selectedServerId = Server;
                await OnServerSelectionChanged(selectedServerId);
            }
        }
    }

    private async Task LoadAvailableServers()
    {
        try
        {
            availableServers = await McpServerService.GetAllServersAsync();
            
            // If a server parameter is provided via URL, pre-select it
            if (!string.IsNullOrEmpty(Server))
            {
                var serverExists = availableServers.Any(s => s.ServerId == Server);
                if (serverExists)
                {
                    selectedServerId = Server;
                    await OnServerSelectionChanged(selectedServerId);
                    return; // Skip auto-selection of first enabled server
                }
            }
            
            // Auto-select the first enabled server if available and no URL parameter
            var enabledServer = availableServers.FirstOrDefault(s => s.Enabled);
            if (enabledServer != null)
            {
                selectedServerId = enabledServer.ServerId;
                await OnServerSelectionChanged(selectedServerId);
            }
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load servers: {ex.Message}");
        }
    }

    private async Task OnServerSelectionChanged(string serverId)
    {
        selectedServerId = serverId;
        
        // Reset all data when server changes
        connectionTestResult = null;
        tools = null;
        resources = null;
        prompts = null;
        
        if (!string.IsNullOrEmpty(serverId))
        {
            // Load data for the selected tab
            await LoadDataForActiveTab();
        }
        
        StateHasChanged();
    }

    private async Task OnTabChanged(string tabKey)
    {
        activeTabKey = tabKey;
        
        if (!string.IsNullOrEmpty(selectedServerId))
        {
            await LoadDataForActiveTab();
        }
    }

    private async Task LoadDataForActiveTab()
    {
        switch (activeTabKey)
        {
            case "tools":
                if (tools == null)
                    await LoadTools();
                break;
            case "resources":
                if (resources == null)
                    await LoadResources();
                break;
            case "prompts":
                if (prompts == null)
                    await LoadPrompts();
                break;
        }
    }

    private async Task TestConnection()
    {
        if (string.IsNullOrEmpty(selectedServerId))
            return;

        isTestingConnection = true;
        StateHasChanged();

        try
        {
            connectionTestResult = await InspectorService.TestConnectionAsync(selectedServerId);
        }
        catch (System.Exception ex)
        {
            connectionTestResult = new McpConnectionTestResult
            {
                IsConnected = false,
                ErrorMessage = ex.Message,
                ConnectionTime = TimeSpan.Zero
            };
        }
        finally
        {
            isTestingConnection = false;
            StateHasChanged();
        }
    }

    private async Task LoadTools()
    {
        if (string.IsNullOrEmpty(selectedServerId))
            return;

        isLoadingTools = true;
        StateHasChanged();

        try
        {
            tools = await InspectorService.GetServerToolsAsync(selectedServerId);
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load tools: {ex.Message}");
            tools = Array.Empty<McpInspectorTool>();
        }
        finally
        {
            isLoadingTools = false;
            StateHasChanged();
        }
    }

    private async Task LoadResources()
    {
        if (string.IsNullOrEmpty(selectedServerId))
            return;

        isLoadingResources = true;
        StateHasChanged();

        try
        {
            resources = await InspectorService.GetServerResourcesAsync(selectedServerId);
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load resources: {ex.Message}");
            resources = Array.Empty<McpInspectorResource>();
        }
        finally
        {
            isLoadingResources = false;
            StateHasChanged();
        }
    }

    private async Task LoadPrompts()
    {
        if (string.IsNullOrEmpty(selectedServerId))
            return;

        isLoadingPrompts = true;
        StateHasChanged();

        try
        {
            prompts = await InspectorService.GetServerPromptsAsync(selectedServerId);
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to load prompts: {ex.Message}");
            prompts = Array.Empty<McpInspectorPrompt>();
        }
        finally
        {
            isLoadingPrompts = false;
            StateHasChanged();
        }
    }

    private void ShowToolDialog(McpInspectorTool tool)
    {
        selectedTool = tool;
        toolParametersJson = tool.IsParameterless ? "" : GenerateExampleJson(tool.InputSchema);
        toolCallResult = null;
        toolDialogVisible = true;
    }

    private async Task CallTool()
    {
        if (selectedTool == null || string.IsNullOrEmpty(selectedServerId))
            return;

        isCallingTool = true;
        StateHasChanged();

        try
        {
            Dictionary<string, object>? parameters = null;
            
            if (!selectedTool.IsParameterless && !string.IsNullOrWhiteSpace(toolParametersJson))
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(toolParametersJson);
                    parameters = JsonElementToDictionary(jsonElement);
                }
                catch (JsonException ex)
                {
                    MessageService.Error($"Invalid JSON parameters: {ex.Message}");
                    return;
                }
            }

            toolCallResult = await InspectorService.CallToolAsync(selectedServerId, selectedTool.Name, parameters);
        }
        catch (System.Exception ex)
        {
            toolCallResult = new McpToolCallResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ExecutionTime = TimeSpan.Zero
            };
        }
        finally
        {
            isCallingTool = false;
            StateHasChanged();
        }
    }

    private async Task ShowResourceDialog(McpInspectorResource resource)
    {
        selectedResource = resource;
        resourceContent = null;
        resourceContentError = null;
        resourceDialogVisible = true;
        
        await LoadResourceContent();
    }

    private async Task LoadResourceContent()
    {
        if (selectedResource == null || string.IsNullOrEmpty(selectedServerId))
            return;

        isLoadingResourceContent = true;
        StateHasChanged();

        try
        {
            resourceContent = await InspectorService.ReadResourceAsync(selectedServerId, selectedResource.Uri);
            resourceContentError = null;
        }
        catch (System.Exception ex)
        {
            resourceContentError = ex.Message;
            resourceContent = null;
        }
        finally
        {
            isLoadingResourceContent = false;
            StateHasChanged();
        }
    }

    private async Task CopyResourceContent()
    {
        if (resourceContent?.Content == null)
            return;

        isCopyingResource = true;
        StateHasChanged();

        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", resourceContent.Content);
            MessageService.Success("Resource content copied to clipboard!");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to copy to clipboard: {ex.Message}");
        }
        finally
        {
            isCopyingResource = false;
            StateHasChanged();
        }
    }

    private void ShowPromptDialog(McpInspectorPrompt prompt)
    {
        selectedPrompt = prompt;
        promptArgumentsJson = prompt.HasArguments ? "{}" : "";
        promptArgumentValues = new Dictionary<string, string>();
        
        // Initialize argument values
        if (prompt.HasArguments && prompt.Arguments != null)
        {
            foreach (var arg in prompt.Arguments)
            {
                var argJson = JsonSerializer.Serialize(arg);
                var argElement = JsonSerializer.Deserialize<JsonElement>(argJson);
                
                if (argElement.TryGetProperty("name", out var nameProperty))
                {
                    var argName = nameProperty.GetString();
                    if (!string.IsNullOrEmpty(argName))
                    {
                        // Generate a default value based on the argument schema
                        var defaultValue = GenerateDefaultValueForArgument(argElement);
                        promptArgumentValues[argName] = defaultValue;
                    }
                }
            }
        }
        
        promptResult = null;
        promptError = null;
        promptDialogVisible = true;
    }

    private async Task GetPrompt()
    {
        if (selectedPrompt == null || string.IsNullOrEmpty(selectedServerId))
            return;

        isGettingPrompt = true;
        StateHasChanged();

        try
        {
            Dictionary<string, object>? arguments = null;
            
            if (selectedPrompt.HasArguments)
            {
                // Use individual argument values instead of JSON textarea
                arguments = new Dictionary<string, object>();
                
                foreach (var kvp in promptArgumentValues)
                {
                    var value = kvp.Value;
                    
                    // Try to parse the value based on the argument schema
                    if (selectedPrompt.Arguments != null)
                    {
                        var argSchema = selectedPrompt.Arguments
                            .FirstOrDefault(arg =>
                            {
                                var argJson = JsonSerializer.Serialize(arg);
                                var argElement = JsonSerializer.Deserialize<JsonElement>(argJson);
                                return argElement.TryGetProperty("name", out var nameProperty) &&
                                       nameProperty.GetString() == kvp.Key;
                            });
                        
                        if (argSchema != null)
                        {
                            var parsedValue = ParseArgumentValue(value, argSchema);
                            arguments[kvp.Key] = parsedValue;
                        }
                        else
                        {
                            arguments[kvp.Key] = value;
                        }
                    }
                    else
                    {
                        arguments[kvp.Key] = value;
                    }
                }
            }

            promptResult = await InspectorService.GetPromptAsync(selectedServerId, selectedPrompt.Name, arguments);
            promptError = null;
        }
        catch (System.Exception ex)
        {
            promptError = ex.Message;
            promptResult = null;
        }
        finally
        {
            isGettingPrompt = false;
            StateHasChanged();
        }
    }

    private async Task ExportServerEntry()
    {
        if (string.IsNullOrEmpty(selectedServerId))
            return;

        isExportingEntry = true;
        StateHasChanged();

        try
        {
            var export = await InspectorService.ExportServerConfigAsync(selectedServerId);
            var json = JsonSerializer.Serialize(export.Configuration, new JsonSerializerOptions { WriteIndented = true });
            
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", json);
            MessageService.Success("Server entry copied to clipboard!");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to export server entry: {ex.Message}");
        }
        finally
        {
            isExportingEntry = false;
            StateHasChanged();
        }
    }

    private async Task ExportCompleteConfig()
    {
        isExportingComplete = true;
        StateHasChanged();

        try
        {
            var config = await InspectorService.ExportAllServersConfigAsync();
            
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", config);
            MessageService.Success("Complete configuration copied to clipboard!");
        }
        catch (System.Exception ex)
        {
            MessageService.Error($"Failed to export complete config: {ex.Message}");
        }
        finally
        {
            isExportingComplete = false;
            StateHasChanged();
        }
    }

    private static Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>();
        
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                dictionary[property.Name] = JsonElementToObject(property.Value);
            }
        }
        
        return dictionary;
    }

    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
            _ => element.ToString()
        };
    }

    private static string GenerateExampleJson(object? schema)
    {
        if (schema == null)
            return "{}";

        try
        {
            // Convert schema to JsonElement for easier processing
            var schemaJson = JsonSerializer.Serialize(schema);
            var schemaElement = JsonSerializer.Deserialize<JsonElement>(schemaJson);
            
            var example = GenerateExampleFromSchema(schemaElement);
            return JsonSerializer.Serialize(example, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            });
        }
        catch
        {
            return "{}";
        }
    }

    private static object GenerateExampleFromSchema(JsonElement schema)
    {
        // Handle different schema types
        if (schema.TryGetProperty("type", out var typeProperty))
        {
            var type = typeProperty.GetString();
            
            switch (type?.ToLower())
            {
                case "object":
                    return GenerateObjectExample(schema);
                case "array":
                    return GenerateArrayExample(schema);
                case "string":
                    return GenerateStringExample(schema);
                case "number":
                case "integer":
                    return GenerateNumberExample(schema);
                case "boolean":
                    return false;
                case "null":
                    return null!;
            }
        }
        
        // If no type specified, try to infer from properties
        if (schema.TryGetProperty("properties", out _))
        {
            return GenerateObjectExample(schema);
        }
        
        return "example_value";
    }

    private static Dictionary<string, object> GenerateObjectExample(JsonElement schema)
    {
        var example = new Dictionary<string, object>();
        
        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                var propertyName = property.Name;
                var propertySchema = property.Value;
                
                example[propertyName] = GenerateExampleFromSchema(propertySchema);
            }
        }
        
        // Handle required properties to ensure they're included
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProp in required.EnumerateArray())
            {
                var propName = requiredProp.GetString();
                if (!string.IsNullOrEmpty(propName) && !example.ContainsKey(propName))
                {
                    example[propName] = "required_value";
                }
            }
        }
        
        return example;
    }

    private static object[] GenerateArrayExample(JsonElement schema)
    {
        if (schema.TryGetProperty("items", out var items))
        {
            var itemExample = GenerateExampleFromSchema(items);
            return new[] { itemExample };
        }
        
        return new object[] { "example_item" };
    }

    private static string GenerateStringExample(JsonElement schema)
    {
        // Check for enum values
        if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var firstEnum = enumValues.EnumerateArray().FirstOrDefault();
            if (firstEnum.ValueKind == JsonValueKind.String)
            {
                return firstEnum.GetString() ?? "enum_value";
            }
        }
        
        // Check for examples
        if (schema.TryGetProperty("example", out var example) && example.ValueKind == JsonValueKind.String)
        {
            return example.GetString() ?? "example_string";
        }
        
        // Check for default value
        if (schema.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String)
        {
            return defaultValue.GetString() ?? "default_value";
        }
        
        // Generate based on format or pattern
        if (schema.TryGetProperty("format", out var format))
        {
            var formatStr = format.GetString();
            return formatStr switch
            {
                "email" => "user@example.com",
                "uri" => "https://example.com",
                "date" => "2023-12-01",
                "date-time" => "2023-12-01T10:00:00Z",
                "uuid" => "550e8400-e29b-41d4-a716-446655440000",
                _ => "example_string"
            };
        }
        
        // Check property name for hints
        if (schema.TryGetProperty("title", out var title))
        {
            var titleStr = title.GetString()?.ToLower();
            if (!string.IsNullOrEmpty(titleStr))
            {
                if (titleStr.Contains("email")) return "user@example.com";
                if (titleStr.Contains("name")) return "example_name";
                if (titleStr.Contains("url") || titleStr.Contains("uri")) return "https://example.com";
                if (titleStr.Contains("path")) return "/example/path";
                if (titleStr.Contains("id")) return "example_id";
            }
        }
        
        return "example_string";
    }

    private static object GenerateNumberExample(JsonElement schema)
    {
        // Check for examples
        if (schema.TryGetProperty("example", out var example) && example.ValueKind == JsonValueKind.Number)
        {
            return example.GetDecimal();
        }
        
        // Check for default value
        if (schema.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.Number)
        {
            return defaultValue.GetDecimal();
        }
        
        // Check for minimum value
        if (schema.TryGetProperty("minimum", out var minimum) && minimum.ValueKind == JsonValueKind.Number)
        {
            return minimum.GetDecimal();
        }
        
        // Check if it's an integer
        if (schema.TryGetProperty("type", out var type) && type.GetString() == "integer")
        {
            return 42;
        }
        
        return 3.14;
    }

    private async Task RegenerateToolExample()
    {
        if (selectedTool != null)
        {
            toolParametersJson = GenerateExampleJson(selectedTool.InputSchema);
            await InvokeAsync(StateHasChanged);
        }
    }
    
    private static string GenerateDefaultValueForArgument(JsonElement argSchema)
    {
        try
        {
            // Check if there's a default value
            if (argSchema.TryGetProperty("default", out var defaultValue))
            {
                return defaultValue.ValueKind switch
                {
                    JsonValueKind.String => defaultValue.GetString() ?? "",
                    JsonValueKind.Number => defaultValue.GetDecimal().ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => defaultValue.ToString()
                };
            }
            
            // Check the type and generate appropriate default
            if (argSchema.TryGetProperty("type", out var typeProperty))
            {
                var type = typeProperty.GetString();
                return type?.ToLower() switch
                {
                    "string" => "",
                    "number" or "integer" => "0",
                    "boolean" => "false",
                    _ => ""
                };
            }
            
            // Check for description to infer type
            if (argSchema.TryGetProperty("description", out var description))
            {
                var desc = description.GetString()?.ToLower() ?? "";
                if (desc.Contains("email")) return "user@example.com";
                if (desc.Contains("url") || desc.Contains("uri")) return "https://example.com";
                if (desc.Contains("path")) return "/example/path";
                if (desc.Contains("name")) return "example_name";
                if (desc.Contains("id")) return "example_id";
            }
            
            return "";
        }
        catch
        {
            return "";
        }
    }

    private static object ParseArgumentValue(string value, object argSchema)
    {
        try
        {
            var argJson = JsonSerializer.Serialize(argSchema);
            var argElement = JsonSerializer.Deserialize<JsonElement>(argJson);
            
            if (argElement.TryGetProperty("type", out var typeProperty))
            {
                var type = typeProperty.GetString();
                return type?.ToLower() switch
                {
                    "integer" => int.TryParse(value, out var intVal) ? intVal : 0,
                    "number" => decimal.TryParse(value, out var decVal) ? decVal : 0m,
                    "boolean" => bool.TryParse(value, out var boolVal) ? boolVal : false,
                    "string" => value,
                    _ => value
                };
            }
            
            return value;
        }
        catch
        {
            return value;
        }
    }

    private string GetStringValue(string argName)
    {
        return promptArgumentValues.TryGetValue(argName, out var value) ? value : "";
    }

    private decimal GetNumericValue(string argName)
    {
        if (promptArgumentValues.TryGetValue(argName, out var value) && 
            decimal.TryParse(value, out var numValue))
        {
            return numValue;
        }
        return 0;
    }

    private bool GetBooleanValue(string argName)
    {
        if (promptArgumentValues.TryGetValue(argName, out var value) && 
            bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }
        return false;
    }

    private void UpdateArgumentValue(string argName, string? value)
    {
        promptArgumentValues[argName] = value ?? "";
    }

    private void UpdateBooleanArgumentValue(string argName, bool value)
    {
        promptArgumentValues[argName] = value.ToString().ToLower();
    }

    private void UpdateNumericArgumentValue(string argName, decimal value)
    {
        promptArgumentValues[argName] = value.ToString();
    }

    private string GetArgumentsAsJson()
    {
        try
        {
            var args = new Dictionary<string, object>();
            foreach (var kvp in promptArgumentValues)
            {
                args[kvp.Key] = kvp.Value;
            }
            return JsonSerializer.Serialize(args, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            });
        }
        catch
        {
            return "{}";
        }
    }

    private void UpdateArgumentsFromJson(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                promptArgumentValues.Clear();
                return;
            }

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                promptArgumentValues.Clear();
                foreach (var property in jsonElement.EnumerateObject())
                {
                    promptArgumentValues[property.Name] = property.Value.ToString();
                }
            }
        }
        catch
        {
            // Ignore JSON parsing errors for now
        }
    }

    private ArgumentInfo GetArgumentInfo(object arg)
    {
        try
        {
            var argJson = JsonSerializer.Serialize(arg);
            var argElement = JsonSerializer.Deserialize<JsonElement>(argJson);
            
            var info = new ArgumentInfo();
            
            if (argElement.TryGetProperty("name", out var nameProperty))
                info.Name = nameProperty.GetString();
            if (argElement.TryGetProperty("description", out var descProperty))
                info.Description = descProperty.GetString();
            if (argElement.TryGetProperty("type", out var typeProperty))
                info.Type = typeProperty.GetString();
            if (argElement.TryGetProperty("required", out var requiredProperty))
                info.IsRequired = requiredProperty.GetBoolean();
                
            return info;
        }
        catch
        {
            return new ArgumentInfo { Name = "unknown", Type = "string" };
        }
    }
}
