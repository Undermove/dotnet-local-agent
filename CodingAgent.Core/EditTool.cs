using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Anthropic.SDK;
using Newtonsoft.Json;
using NJsonSchema.Generation;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core
{
    public class EditToolProgram
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
                        ListFilesDefinition.Instance,
                        BashDefinition.Instance,
                        EditFileDefinition.Instance
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
                    Console.WriteLine("You can still chat, but file editing tools are not available.");
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

    public static class EditFileDefinition
    {
        public static ToolDefinition Instance = new ToolDefinition
        {
            Name = "edit_file",
            Description = @"Make edits to a text file.

Replaces 'old_str' with 'new_str' in the given file. 'old_str' and 'new_str' MUST be different from each other.

If the file specified with path doesn't exist, it will be created.",
            InputSchema = GenerateSchema<EditFileInput>(),
            ExecuteAsync = EditFileAsync
        };

        private static async Task<string> EditFileAsync(string input)
        {
            var editFileInput = JsonConvert.DeserializeObject<EditFileInput>(input);
            
            Console.WriteLine($"Editing file: {editFileInput.Path}");
            
            try
            {
                // Validate inputs
                if (editFileInput.OldStr == editFileInput.NewStr)
                {
                    throw new ArgumentException("old_str and new_str must be different");
                }

                string content;
                bool fileExists = File.Exists(editFileInput.Path);
                
                if (fileExists)
                {
                    content = await File.ReadAllTextAsync(editFileInput.Path);
                    
                    // Check if old_str exists in the file
                    if (!content.Contains(editFileInput.OldStr))
                    {
                        throw new ArgumentException($"old_str '{editFileInput.OldStr}' not found in file");
                    }
                    
                    // Count occurrences to ensure it's unique
                    int count = 0;
                    int index = 0;
                    while ((index = content.IndexOf(editFileInput.OldStr, index)) != -1)
                    {
                        count++;
                        index += editFileInput.OldStr.Length;
                    }
                    
                    if (count > 1)
                    {
                        throw new ArgumentException($"old_str '{editFileInput.OldStr}' appears {count} times in file. It must appear exactly once.");
                    }
                    
                    // Replace the string
                    content = content.Replace(editFileInput.OldStr, editFileInput.NewStr);
                }
                else
                {
                    // Create new file with new_str content
                    content = editFileInput.NewStr;
                    
                    // Create directory if it doesn't exist
                    var directory = Path.GetDirectoryName(editFileInput.Path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                
                await File.WriteAllTextAsync(editFileInput.Path, content);
                
                var action = fileExists ? "modified" : "created";
                var result = $"File {editFileInput.Path} {action} successfully";
                Console.WriteLine(result);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to edit file {editFileInput.Path}: {ex.Message}");
                throw;
            }
        }
    }

    public class EditFileInput
    {
        [JsonProperty("path")]
        public string Path { get; set; }
        
        [JsonProperty("old_str")]
        public string OldStr { get; set; }
        
        [JsonProperty("new_str")]
        public string NewStr { get; set; }
    }
}