using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Newtonsoft.Json;
using NJsonSchema.Generation;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core
{
    public class ListFilesProgram
    {
        public static async Task RunAsync(string[] args)
        {
            var cmdArgs = CommandLineArgs.Parse(args);

            if (cmdArgs.Verbose)
            {
                Console.WriteLine("Verbose logging enabled");
                Console.WriteLine($"Using provider: {cmdArgs.Provider}");
            }

            try
            {
                var provider = cmdArgs.CreateProvider();

                if (cmdArgs.Provider == AIProviderType.Anthropic)
                {
                    // Используем полнофункциональный агент с инструментами для Anthropic
                    var client = new AnthropicClient(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
                    
                    if (cmdArgs.Verbose)
                    {
                        Console.WriteLine("Anthropic client initialized");
                    }

                    var tools = new List<ToolDefinition> 
                    { 
                        ReadFileDefinition.Instance,
                        ListFilesDefinition.Instance
                    };
                    
                    if (cmdArgs.Verbose)
                    {
                        Console.WriteLine($"Initialized {tools.Count} tools");
                    }

                    var agent = new AgentWithTools(client, GetUserMessage, tools, cmdArgs.Verbose);
                    await agent.RunAsync();
                }
                else
                {
                    // Для других провайдеров используем обычный чат без инструментов
                    Console.WriteLine("Note: LM Studio provider doesn't support tools yet.");
                    Console.WriteLine("You can still chat, but file listing tools are not available.");
                    Console.WriteLine("For full functionality, use: --provider anthropic");
                    Console.WriteLine();
                    
                    var agent = new Agent(provider, GetUserMessage, cmdArgs.Verbose);
                    await agent.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing AI provider: {ex.Message}");
                if (cmdArgs.Provider == AIProviderType.Anthropic)
                {
                    Console.WriteLine("Make sure ANTHROPIC_API_KEY environment variable is set");
                }
                else if (cmdArgs.Provider == AIProviderType.LMStudio)
                {
                    Console.WriteLine("Make sure LM Studio is running and accessible");
                }
                return;
            }
        }

        private static string GetUserMessage()
        {
            return Console.ReadLine();
        }
    }

    public static class ListFilesDefinition
    {
        public static ToolDefinition Instance = new ToolDefinition
        {
            Name = "list_files",
            Description = "List files and directories at a given path. If no path is provided, lists files in the current directory.",
            InputSchema = GenerateSchema<ListFilesInput>(),
            ExecuteAsync = ListFilesAsync
        };

        private static async Task<string> ListFilesAsync(string input)
        {
            var listFilesInput = JsonConvert.DeserializeObject<ListFilesInput>(input);
            
            var dir = string.IsNullOrEmpty(listFilesInput.Path) ? "." : listFilesInput.Path;
            
            Console.WriteLine($"Listing files in directory: {dir}");
            
            try
            {
                var files = new List<string>();
                
                await Task.Run(() =>
                {
                    var directoryInfo = new DirectoryInfo(dir);
                    
                    // Get all files and directories recursively
                    var allItems = directoryInfo.GetFileSystemInfos("*", SearchOption.AllDirectories);
                    
                    foreach (var item in allItems)
                    {
                        var relativePath = Path.GetRelativePath(dir, item.FullName);
                        
                        // Skip .devenv directory and its contents
                        if (relativePath.StartsWith(".devenv") || relativePath.Contains("/.devenv/") || relativePath.Contains("\\.devenv\\"))
                        {
                            continue;
                        }
                        
                        if (item is DirectoryInfo)
                        {
                            files.Add(relativePath + "/");
                        }
                        else
                        {
                            files.Add(relativePath);
                        }
                    }
                });
                
                var result = JsonConvert.SerializeObject(files);
                Console.WriteLine($"Successfully listed {files.Count} files in {dir}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list files in {dir}: {ex.Message}");
                throw;
            }
        }
    }

    public class ListFilesInput
    {
        [JsonProperty("path")]
        public string Path { get; set; }
    }
}