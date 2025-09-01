# Mcp.HelloWorld.Client

This sample demonstrates how to use the Model Context Protocol client with Azure OpenAI.

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
