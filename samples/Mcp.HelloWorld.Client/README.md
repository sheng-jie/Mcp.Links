# Mcp.HelloWorld.Client

This sample demonstrates how to use the Model Context Protocol client with Azure OpenAI.

## Features

### Enhanced Elicitation Case
The `ElicitationCase.cs` has been enhanced based on the Node.js MCP server implementation with the following improvements:

- **Better error handling**: Comprehensive try-catch blocks for robust error management
- **User-friendly interface**: Emoji indicators and clear prompts for better UX
- **Input validation**: Proper validation with retry logic for invalid inputs
- **Flexible input**: Support for skipping optional fields
- **Progress tracking**: Visual progress indicators showing current step
- **Data summary**: Clear summary of collected information before submission
- **Action handling**: Proper support for accept, decline, and cancel actions
- **Graceful degradation**: Handles missing elicitation tools gracefully

### Elicitation Features Demonstrated:
- ✅ Boolean input with multiple formats (true/false, yes/no)
- ✅ Number input with range validation
- ✅ String input with proper handling
- ✅ Skip functionality for optional fields
- ✅ Confirmation step before data submission
- ✅ Comprehensive error reporting

## Configuration

The application uses .NET's configuration system to manage settings securely.

### Setting up User Secrets

To store sensitive information like API keys securely, use .NET User Secrets:

1. Initialize user secrets (already done):
   ```bash
   dotnet user-secrets init
   ```

2. Set your Azure OpenAI endpoint:
   ```bash
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-openai-instance.openai.azure.com"
   ```

3. Set your Azure OpenAI API key:
   ```bash
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
   ```

### Configuration Files

- `appsettings.json`: Contains non-sensitive configuration like deployment names
- User secrets: Contains sensitive information like API keys and endpoints

### Configuration Structure

```json
{
  "AzureOpenAI": {
    "Endpoint": "your-endpoint-here",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "gpt-4o-mini",
    "ChatDeploymentName": "gpt-4.1"
  }
}
```

## Running the Application

After setting up the configuration:

```bash
dotnet run
```

The application will:
1. Connect to the specified Azure OpenAI service
2. Connect to an MCP server (everything server)
3. List available tools
4. Allow interactive chat with tool usage
5. Demonstrate elicitation capabilities when the elicitation tool is called

## Elicitation Demo

To test the enhanced elicitation features:
1. Run the application
2. The elicitation case will automatically connect to the `@modelcontextprotocol/server-everything` server
3. It will look for the `elicitation` tool and call it
4. Follow the interactive prompts to provide information
5. See how the different action types (accept, decline, cancel) are handled
