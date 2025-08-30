using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AntDesign;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections;

namespace Mcp.Links.Http.Pages.Mcp;

public partial class EnvCheck : ComponentBase
{
    // [Inject] private IMessageService MessageService { get; set; } = null!;
    // [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private bool isCheckingAll = false;
    private bool showInstallationHelp = false;

    private ToolStatus nodeStatus = new();
    private ToolStatus npxStatus = new();
    private ToolStatus dotnetStatus = new();
    private ToolStatus dnxStatus = new();
    private ToolStatus pythonStatus = new();
    private ToolStatus uvStatus = new();
    private ToolStatus uvxStatus = new();

    protected override async Task OnInitializedAsync()
    {
        // Initialize all statuses
        nodeStatus = new ToolStatus();
        npxStatus = new ToolStatus();
        dotnetStatus = new ToolStatus();
        dnxStatus = new ToolStatus();
        pythonStatus = new ToolStatus();
        uvStatus = new ToolStatus();
        uvxStatus = new ToolStatus();
        
        // Auto-check all on page load
        await CheckAllEnvironments();
    }

    private async Task CheckAllEnvironments()
    {
        isCheckingAll = true;
        showInstallationHelp = false;
        StateHasChanged();

        try
        {
            // Check all tools in parallel
            var tasks = new List<Task>
            {
                CheckNodejs(),
                CheckNpx(),
                CheckDotnet(),
                CheckDnx(),
                CheckPython(),
                CheckUv(),
                CheckUvx()
            };

            await Task.WhenAll(tasks);

            // Show installation help if any tool is missing
            if (nodeStatus.Status == CheckStatus.Error || 
                npxStatus.Status == CheckStatus.Error || 
                dotnetStatus.Status == CheckStatus.Error || 
                dnxStatus.Status == CheckStatus.Error || 
                pythonStatus.Status == CheckStatus.Error || 
                uvStatus.Status == CheckStatus.Error || 
                uvxStatus.Status == CheckStatus.Error)
            {
                showInstallationHelp = true;
            }
        }
        finally
        {
            isCheckingAll = false;
            StateHasChanged();
        }
    }

    private async Task CheckNodejs()
    {
        nodeStatus.Loading = true;
        StateHasChanged();

        try
        {
            var result = await RunCommandAsync("node", "--version");
            
            // If the first attempt fails on Windows, try with cmd
            if (!result.Success && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = await RunCommandAsync("cmd", "/c node --version");
            }
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                nodeStatus.Status = CheckStatus.Success;
                nodeStatus.Version = result.Output.Trim();
                nodeStatus.Message = $"Node.js is available: {result.Output.Trim()}";
                nodeStatus.Icon = "check-circle";
                nodeStatus.Color = "#52c41a";
            }
            else
            {
                nodeStatus.Status = CheckStatus.Error;
                nodeStatus.Version = "";
                nodeStatus.Message = $"Node.js is not installed or not in PATH. Debug: ExitCode={result.ExitCode}, Output='{result.Output}'";
                nodeStatus.Icon = "close-circle";
                nodeStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            nodeStatus.Status = CheckStatus.Error;
            nodeStatus.Version = "";
            nodeStatus.Message = $"Error checking Node.js: {ex.Message}";
            nodeStatus.Icon = "close-circle";
            nodeStatus.Color = "#ff4d4f";
        }
        finally
        {
            nodeStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckNpx()
    {
        npxStatus.Loading = true;
        StateHasChanged();

        try
        {
            var result = await RunCommandAsync("npx", "--version");
            
            // If the first attempt fails on Windows, try with cmd
            if (!result.Success && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = await RunCommandAsync("cmd", "/c npx --version");
            }
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                npxStatus.Status = CheckStatus.Success;
                npxStatus.Version = result.Output.Trim();
                npxStatus.Message = $"NPX is available: {result.Output.Trim()}";
                npxStatus.Icon = "check-circle";
                npxStatus.Color = "#52c41a";
            }
            else
            {
                npxStatus.Status = CheckStatus.Error;
                npxStatus.Version = "";
                npxStatus.Message = $"NPX is not installed or not in PATH. Debug: ExitCode={result.ExitCode}, Output='{result.Output}'";
                npxStatus.Icon = "close-circle";
                npxStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            npxStatus.Status = CheckStatus.Error;
            npxStatus.Version = "";
            npxStatus.Message = $"Error checking NPX: {ex.Message}";
            npxStatus.Icon = "close-circle";
            npxStatus.Color = "#ff4d4f";
        }
        finally
        {
            npxStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckDotnet()
    {
        dotnetStatus.Loading = true;
        StateHasChanged();

        try
        {
            var result = await RunCommandAsync("dotnet", "--version");
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                dotnetStatus.Status = CheckStatus.Success;
                dotnetStatus.Version = result.Output.Trim();
                dotnetStatus.Message = $".NET is available: {result.Output.Trim()}";
                dotnetStatus.Icon = "check-circle";
                dotnetStatus.Color = "#52c41a";
            }
            else
            {
                dotnetStatus.Status = CheckStatus.Error;
                dotnetStatus.Version = "";
                dotnetStatus.Message = $".NET is not installed or not in PATH. Debug: ExitCode={result.ExitCode}, Output='{result.Output}'";
                dotnetStatus.Icon = "close-circle";
                dotnetStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            dotnetStatus.Status = CheckStatus.Error;
            dotnetStatus.Version = "";
            dotnetStatus.Message = $"Error checking .NET: {ex.Message}";
            dotnetStatus.Icon = "close-circle";
            dotnetStatus.Color = "#ff4d4f";
        }
        finally
        {
            dotnetStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckDnx()
    {
        dnxStatus.Loading = true;
        StateHasChanged();

        try
        {
            // Check for DNX using the help command since there's no --version flag
            var result = await RunCommandAsync("dnx", "--help");
            
            // If the first attempt fails on Windows, try with cmd
            if (!result.Success && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = await RunCommandAsync("cmd", "/c dnx --help");
            }
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                // Check if the output contains expected DNX help content
                if (result.Output.Contains("Executes a tool from source without permanently installing it") ||
                    result.Output.Contains("dotnet dnx"))
                {
                    dnxStatus.Status = CheckStatus.Success;
                    dnxStatus.Version = "Available";
                    dnxStatus.Message = "DNX is available and working";
                    dnxStatus.Icon = "check-circle";
                    dnxStatus.Color = "#52c41a";
                }
                else
                {
                    dnxStatus.Status = CheckStatus.Error;
                    dnxStatus.Version = "";
                    dnxStatus.Message = "DNX command exists but may not be functioning correctly";
                    dnxStatus.Icon = "close-circle";
                    dnxStatus.Color = "#ff4d4f";
                }
            }
            else
            {
                dnxStatus.Status = CheckStatus.Error;
                dnxStatus.Version = "";
                dnxStatus.Message = $"DNX is not installed or not in PATH. Debug: ExitCode={result.ExitCode}, Output='{result.Output}'";
                dnxStatus.Icon = "close-circle";
                dnxStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            dnxStatus.Status = CheckStatus.Error;
            dnxStatus.Version = "";
            dnxStatus.Message = $"Error checking DNX: {ex.Message}";
            dnxStatus.Icon = "close-circle";
            dnxStatus.Color = "#ff4d4f";
        }
        finally
        {
            dnxStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckPython()
    {
        pythonStatus.Loading = true;
        StateHasChanged();

        try
        {
            // Try both 'python' and 'python3' commands
            var result = await RunCommandAsync("python", "--version");
            
            if (!result.Success)
            {
                result = await RunCommandAsync("python3", "--version");
            }
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                pythonStatus.Status = CheckStatus.Success;
                pythonStatus.Version = result.Output.Trim().Replace("Python ", "");
                pythonStatus.Message = $"Python is available: {result.Output.Trim()}";
                pythonStatus.Icon = "check-circle";
                pythonStatus.Color = "#52c41a";
            }
            else
            {
                pythonStatus.Status = CheckStatus.Error;
                pythonStatus.Version = "";
                pythonStatus.Message = "Python is not installed or not in PATH";
                pythonStatus.Icon = "close-circle";
                pythonStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            pythonStatus.Status = CheckStatus.Error;
            pythonStatus.Version = "";
            pythonStatus.Message = $"Error checking Python: {ex.Message}";
            pythonStatus.Icon = "close-circle";
            pythonStatus.Color = "#ff4d4f";
        }
        finally
        {
            pythonStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckUv()
    {
        uvStatus.Loading = true;
        StateHasChanged();

        try
        {
            var result = await RunCommandAsync("uv", "--version");
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                uvStatus.Status = CheckStatus.Success;
                uvStatus.Version = result.Output.Trim().Replace("uv ", "");
                uvStatus.Message = $"UV is available: {result.Output.Trim()}";
                uvStatus.Icon = "check-circle";
                uvStatus.Color = "#52c41a";
            }
            else
            {
                uvStatus.Status = CheckStatus.Error;
                uvStatus.Version = "";
                uvStatus.Message = "UV is not installed or not in PATH";
                uvStatus.Icon = "close-circle";
                uvStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            uvStatus.Status = CheckStatus.Error;
            uvStatus.Version = "";
            uvStatus.Message = $"Error checking UV: {ex.Message}";
            uvStatus.Icon = "close-circle";
            uvStatus.Color = "#ff4d4f";
        }
        finally
        {
            uvStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckUvx()
    {
        uvxStatus.Loading = true;
        StateHasChanged();

        try
        {
            var result = await RunCommandAsync("uvx", "--version");
            
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                uvxStatus.Status = CheckStatus.Success;
                uvxStatus.Version = result.Output.Trim().Replace("uvx ", "");
                uvxStatus.Message = $"UVX is available: {result.Output.Trim()}";
                uvxStatus.Icon = "check-circle";
                uvxStatus.Color = "#52c41a";
            }
            else
            {
                uvxStatus.Status = CheckStatus.Error;
                uvxStatus.Version = "";
                uvxStatus.Message = "UVX is not installed or not in PATH";
                uvxStatus.Icon = "close-circle";
                uvxStatus.Color = "#ff4d4f";
            }
        }
        catch (System.Exception ex)
        {
            uvxStatus.Status = CheckStatus.Error;
            uvxStatus.Version = "";
            uvxStatus.Message = $"Error checking UVX: {ex.Message}";
            uvxStatus.Icon = "close-circle";
            uvxStatus.Color = "#ff4d4f";
        }
        finally
        {
            uvxStatus.Loading = false;
            StateHasChanged();
        }
    }

    private async Task<CommandResult> RunCommandAsync(string command, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            // Add environment variables to ensure PATH is properly set
            foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
            {
                var key = envVar.Key?.ToString();
                var value = envVar.Value?.ToString();
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    startInfo.EnvironmentVariables[key] = value;
                }
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Set a timeout to avoid hanging
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var processTask = process.WaitForExitAsync();
            
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                process.Kill();
                return new CommandResult
                {
                    Success = false,
                    Output = "Command timed out after 10 seconds",
                    ExitCode = -1
                };
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var output = await outputTask;
            var error = await errorTask;

            // For some commands, the version might be in stderr instead of stdout
            var resultOutput = !string.IsNullOrWhiteSpace(output) ? output : error;

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Output = resultOutput?.Trim() ?? "",
                ExitCode = process.ExitCode
            };
        }
        catch (System.Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Output = ex.Message,
                ExitCode = -1
            };
        }
    }

    private class ToolStatus
    {
        public CheckStatus Status { get; set; } = CheckStatus.NotChecked;
        public string Version { get; set; } = "";
        public string Message { get; set; } = "";
        public bool Loading { get; set; } = false;
        public string Icon { get; set; } = "clock-circle";
        public string Color { get; set; } = "#d9d9d9";
    }

    private enum CheckStatus
    {
        NotChecked,
        Success,
        Error
    }

    private class CommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public int ExitCode { get; set; }
    }
}
