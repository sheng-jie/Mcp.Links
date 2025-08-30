[English](README.md) | [简体中文](README.zh-CN.md)

# MCP 聚合器
此仓库包含一系列使用 MCP C# SDK 的模型上下文协议 (MCP) 聚合器。这些聚合器旨在促进多个 MCP 服务器的集成和管理，从而实现流畅的数据处理和交互。

# 使用方法

## 作为 Stdio 服务器运行

这个特定的聚合器是一个基于标准输入/输出 (stdio) 的 MCP 服务器，它利用 MCP C# SDK 提供了一种简单有效的方式来通过命令行界面与 MCP 模型进行交互。

要使用 MCP 聚合器 Stdio，您可以使用以下命令运行服务器：
```bash
dnx Mcp.Links.Stdio --mcp-file=path/to/mcp.json --yes
```

如果没有提供 `--mcp-file`，它将使用默认的 `mcp.json`：
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

## 作为 HTTP 服务器运行
要将 MCP 聚合器作为 HTTP 服务器运行，您可以使用以下命令：
```bash
dotnet run --project src/Mcp.Links.Http/Mcp.Links.Http.csproj
```
这将启动一个提供 MCP 聚合器功能的 HTTP 服务器。您可以在 `http://localhost:5146/mcp` 访问它。

# 待办事项
- [ ] 支持聚合提示和资源。
- [x] 支持作为 Stdio MCP 服务器运行。（支持工具）
- [x] 支持作为 HTTP MCP 服务器运行。（支持工具）
- [ ] 添加 Web UI 来配置 MCP 服务器。
  - [ ] 添加页面来列出 MCP 服务器。
  - [ ] 添加页面来配置 MCP 服务器。
  - [ ] 添加页面来为每个 MCP 服务器配置工具集。
  - [ ] 添加 Docker 支持。
  - [ ] 添加 Sqlite 支持用于存储 MCP 服务器配置。
