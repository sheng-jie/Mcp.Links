[简体中文](README.zh-CN.md) | [English](README.md)

# MCP Aggregators
This repository contains a collection of Model Context Protocol (MCP) aggregators that utilize the MCP C# SDK. These aggregators are designed to facilitate the integration and management of multiple MCP servers, allowing for streamlined data processing and interaction.
# Usage

## Run as Stdio Server

This specific aggregator is a standard input/output (stdio) based MCP server that leverages the MCP C# SDK to provide a simple and effective way to interact with MCP models through command line interfaces.

To use the MCP Aggregators Stdio, you can run the server with the following command:
```bash
dnx Mcp.Links.Stdio --mcp-file=path/to/mcp.json --yes
```

if no `--mcp-file` is provided, it will use default `mcp.json`:
```json
{
    "mcpServers": {
        "fetch": {
            "command": "uvx",
            "args": [
                "mcp-server-fetch"
            ],
            "env": {
                "port": 3300,
                "node-env": "dev"
            }
        }, 
        "samplemcpserver": {
            "type": "stdio",
            "command": "dnx",
            "args": [
                "sheng-jie.SampleMcpServer","--yes"
            ],
            "env": {
                "WEATHER_CHOICES": "晴朗,暴雨,潮湿,冰冻,多云,阴天,大雪,小雨,雷阵雨,雾霾"
            }
        },
        "time": {
            "type": "sse",
            "url": "https://mcp.api-inference.modelscope.net/0506441bba8744/sse"
        },
        "spec-coding": {
            "type": "stdio",
            "command": "dnx",
            "args": [
                "SpecCodingMcpServer", "--yes"
            ]
        },
        "csharp-code-interceptor": {
            "enabled": false,
            "type": "http",
            "url": "https://csharp.starworks.cc/mcp"
        }
    }
}
```

## Run as HTTP Server
To run the MCP Aggregators as an HTTP server, you can use the following command:
```bash
dotnet run --project src/Mcp.Links.Http/Mcp.Links.Http.csproj
```
This will start an HTTP server that serves the MCP aggregator functionality. You can access it at `http://localhost:5146/mcp`.



# TODO
- [ ] Support aggregation Prompts and Resources.
- [x] Support running as Stdio MCP server. (Tools are supported)
- [x] Support running as HTTP MCP server. (Tools are supported
- [ ] Add a web UI to configure MCP servers.
  - [ ] Add pages to list MCP servers.
  - [ ] Add pages to configure MCP servers.
  - [ ] Add pages to configure tool sets for each MCP server.
  - [ ] Add docker support.
  - [ ] Add Sqlite support for storing MCP server configurations.
