using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json;

namespace CodingAgent.Core;

/// <summary>
/// Инструменты для валидации и проверки результатов выполнения задач
/// </summary>
public static class TaskValidationTools
{
    private static ToolDefinition _buildProjectDefinition;
    private static ToolDefinition _runTestsDefinition;
    private static ToolDefinition _lintCodeDefinition;
    private static ToolDefinition _checkFileStatusDefinition;

    /// <summary>
    /// Инструмент для запуска build проекта
    /// </summary>
    public static ToolDefinition BuildProjectDefinition
    {
        get
        {
            if (_buildProjectDefinition == null)
            {
                _buildProjectDefinition = new ToolDefinition
                {
                    Name = "build_project",
                    Description = "Build a .NET project or solution to check for compilation errors",
                    InputSchema = CreateBuildProjectSchema(),
                    ExecuteAsync = BuildProjectAsync
                };
            }
            return _buildProjectDefinition;
        }
    }

    /// <summary>
    /// Инструмент для запуска тестов
    /// </summary>
    public static ToolDefinition RunTestsDefinition
    {
        get
        {
            if (_runTestsDefinition == null)
            {
                _runTestsDefinition = new ToolDefinition
                {
                    Name = "run_tests",
                    Description = "Run tests for a .NET project to verify functionality",
                    InputSchema = CreateRunTestsSchema(),
                    ExecuteAsync = RunTestsAsync
                };
            }
            return _runTestsDefinition;
        }
    }

    /// <summary>
    /// Инструмент для проверки синтаксиса и стиля кода
    /// </summary>
    public static ToolDefinition LintCodeDefinition
    {
        get
        {
            if (_lintCodeDefinition == null)
            {
                _lintCodeDefinition = new ToolDefinition
                {
                    Name = "lint_code",
                    Description = "Check code style and syntax using dotnet format or other linting tools",
                    InputSchema = CreateLintCodeSchema(),
                    ExecuteAsync = LintCodeAsync
                };
            }
            return _lintCodeDefinition;
        }
    }

    /// <summary>
    /// Инструмент для проверки статуса файлов
    /// </summary>
    public static ToolDefinition CheckFileStatusDefinition
    {
        get
        {
            if (_checkFileStatusDefinition == null)
            {
                _checkFileStatusDefinition = new ToolDefinition
                {
                    Name = "check_file_status",
                    Description = "Check if files exist and get their basic information (size, modification time, etc.)",
                    InputSchema = CreateCheckFileStatusSchema(),
                    ExecuteAsync = CheckFileStatusAsync
                };
            }
            return _checkFileStatusDefinition;
        }
    }

    private static object CreateBuildProjectSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["project_path"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Path to the project file (.csproj) or solution file (.sln)"
                },
                ["configuration"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Build configuration (Debug, Release, etc.)"
                },
                ["verbosity"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Build verbosity level (quiet, minimal, normal, detailed, diagnostic)"
                }
            },
            ["required"] = new[] { "project_path" }
        };
    }

    private static object CreateRunTestsSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["project_path"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Path to the test project file (.csproj) or solution file (.sln)"
                },
                ["configuration"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Test configuration (Debug, Release, etc.)"
                },
                ["verbosity"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Test verbosity level (quiet, minimal, normal, detailed, diagnostic)"
                }
            },
            ["required"] = new[] { "project_path" }
        };
    }

    private static object CreateLintCodeSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["project_path"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Path to the project file (.csproj) or solution file (.sln)"
                },
                ["verify_only"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "Only verify formatting without making changes"
                }
            },
            ["required"] = new[] { "project_path" }
        };
    }

    private static object CreateCheckFileStatusSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["file_paths"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object>
                    {
                        ["type"] = "string"
                    },
                    ["description"] = "Array of file paths to check"
                }
            },
            ["required"] = new[] { "file_paths" }
        };
    }

    private static async Task<string> BuildProjectAsync(string input)
    {
        var buildInput = JsonConvert.DeserializeObject<BuildProjectInput>(input);
            
        Console.WriteLine($"Building project: {buildInput.ProjectPath}");
            
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{buildInput.ProjectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(buildInput.Configuration))
            {
                processStartInfo.Arguments += $" --configuration {buildInput.Configuration}";
            }

            if (buildInput.Verbosity != null)
            {
                processStartInfo.Arguments += $" --verbosity {buildInput.Verbosity}";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start dotnet build process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
                
            await process.WaitForExitAsync();

            var result = new
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };

            var resultJson = System.Text.Json.JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                
            Console.WriteLine($"Build completed with exit code: {process.ExitCode}");
            return resultJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Build failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<string> RunTestsAsync(string input)
    {
        var testInput = JsonConvert.DeserializeObject<RunTestsInput>(input);
            
        Console.WriteLine($"Running tests for: {testInput.ProjectPath}");
            
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testInput.ProjectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(testInput.Configuration))
            {
                processStartInfo.Arguments += $" --configuration {testInput.Configuration}";
            }

            if (testInput.Verbosity != null)
            {
                processStartInfo.Arguments += $" --verbosity {testInput.Verbosity}";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start dotnet test process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
                
            await process.WaitForExitAsync();

            var result = new
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };

            var resultJson = System.Text.Json.JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                
            Console.WriteLine($"Tests completed with exit code: {process.ExitCode}");
            return resultJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test execution failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<string> LintCodeAsync(string input)
    {
        var lintInput = JsonConvert.DeserializeObject<LintCodeInput>(input);
            
        Console.WriteLine($"Linting code at: {lintInput.ProjectPath}");
            
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"format \"{lintInput.ProjectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (lintInput.VerifyOnly)
            {
                processStartInfo.Arguments += " --verify-no-changes";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start dotnet format process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
                
            await process.WaitForExitAsync();

            var result = new
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };

            var resultJson = System.Text.Json.JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                
            Console.WriteLine($"Linting completed with exit code: {process.ExitCode}");
            return resultJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linting failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<string> CheckFileStatusAsync(string input)
    {
        var statusInput = JsonConvert.DeserializeObject<CheckFileStatusInput>(input);
            
        Console.WriteLine($"Checking file status for {statusInput.FilePaths.Length} paths");
            
        try
        {
            var results = new List<object>();

            foreach (var path in statusInput.FilePaths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    results.Add(new
                    {
                        Path = path,
                        Exists = true,
                        IsFile = true,
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        CreatedTime = fileInfo.CreationTime,
                        ModifiedTime = fileInfo.LastWriteTime,
                        Extension = fileInfo.Extension
                    });
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    results.Add(new
                    {
                        Path = path,
                        Exists = true,
                        IsFile = false,
                        IsDirectory = true,
                        Size = (long?)null,
                        CreatedTime = dirInfo.CreationTime,
                        ModifiedTime = dirInfo.LastWriteTime,
                        Extension = (string?)null
                    });
                }
                else
                {
                    results.Add(new
                    {
                        Path = path,
                        Exists = false,
                        IsFile = (bool?)null,
                        IsDirectory = (bool?)null,
                        Size = (long?)null,
                        CreatedTime = (DateTime?)null,
                        ModifiedTime = (DateTime?)null,
                        Extension = (string?)null
                    });
                }
            }

            var resultJson = System.Text.Json.JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                
            Console.WriteLine($"File status check completed for {results.Count} items");
            return resultJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"File status check failed: {ex.Message}");
            throw;
        }
    }
}

// Input models for validation tools
public class BuildProjectInput
{
    [JsonProperty("project_path")]
    [System.Text.Json.Serialization.JsonPropertyName("project_path")]
    public string ProjectPath { get; set; } = "";

    [JsonProperty("configuration")]
    [System.Text.Json.Serialization.JsonPropertyName("configuration")]
    public string? Configuration { get; set; }

    [JsonProperty("verbosity")]
    [System.Text.Json.Serialization.JsonPropertyName("verbosity")]
    public string? Verbosity { get; set; }
}

public class RunTestsInput
{
    [JsonProperty("project_path")]
    [System.Text.Json.Serialization.JsonPropertyName("project_path")]
    public string ProjectPath { get; set; } = "";

    [JsonProperty("configuration")]
    [System.Text.Json.Serialization.JsonPropertyName("configuration")]
    public string? Configuration { get; set; }

    [JsonProperty("verbosity")]
    [System.Text.Json.Serialization.JsonPropertyName("verbosity")]
    public string? Verbosity { get; set; }
}

public class LintCodeInput
{
    [JsonProperty("project_path")]
    [System.Text.Json.Serialization.JsonPropertyName("project_path")]
    public string ProjectPath { get; set; } = "";

    [JsonProperty("verify_only")]
    [System.Text.Json.Serialization.JsonPropertyName("verify_only")]
    public bool VerifyOnly { get; set; } = false;
}

public class CheckFileStatusInput
{
    [JsonProperty("file_paths")]
    [System.Text.Json.Serialization.JsonPropertyName("file_paths")]
    public string[] FilePaths { get; set; } = Array.Empty<string>();
}