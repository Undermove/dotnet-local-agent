using System.Diagnostics;
using Anthropic.SDK;
using Newtonsoft.Json;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core
{
    public class CodeSearchToolProgram
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
                        EditFileDefinition.Instance,
                        CodeSearchDefinition.Instance
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
                    Console.WriteLine("You can still chat, but code search and editing tools are not available.");
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

    public static class CodeSearchDefinition
    {
        public static ToolDefinition Instance = new ToolDefinition
        {
            Name = "code_search",
            Description = "Search for patterns in code files using ripgrep-like functionality. Supports regex patterns and file type filtering.",
            InputSchema = GenerateSchema<CodeSearchInput>(),
            ExecuteAsync = CodeSearchAsync
        };

        private static async Task<string> CodeSearchAsync(string input)
        {
            var searchInput = JsonConvert.DeserializeObject<CodeSearchInput>(input);
            
            Console.WriteLine($"Searching for pattern: {searchInput.Pattern}");
            
            try
            {
                // Try to use ripgrep if available, otherwise fall back to grep
                var result = await TryRipgrepAsync(searchInput) ?? await TryGrepAsync(searchInput);
                
                Console.WriteLine($"Search completed, found results");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed: {ex.Message}");
                throw;
            }
        }

        private static async Task<string> TryRipgrepAsync(CodeSearchInput searchInput)
        {
            try
            {
                var args = $"--color=never --line-number";
                
                if (!string.IsNullOrEmpty(searchInput.FilePattern))
                {
                    args += $" --glob=\"{searchInput.FilePattern}\"";
                }
                
                if (searchInput.CaseSensitive == false)
                {
                    args += " --ignore-case";
                }
                
                args += $" \"{searchInput.Pattern}\"";
                
                if (!string.IsNullOrEmpty(searchInput.Path))
                {
                    args += $" \"{searchInput.Path}\"";
                }
                else
                {
                    args += " .";
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "rg",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    return output;
                }
                else if (process.ExitCode == 1)
                {
                    // No matches found
                    return "No matches found";
                }
                else
                {
                    throw new Exception($"ripgrep failed: {error}");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("No such file or directory") || ex is FileNotFoundException)
            {
                // ripgrep not available
                return null;
            }
        }

        private static async Task<string> TryGrepAsync(CodeSearchInput searchInput)
        {
            var args = "-n"; // line numbers
            
            if (searchInput.CaseSensitive == false)
            {
                args += " -i"; // ignore case
            }
            
            args += " -r"; // recursive
            
            if (!string.IsNullOrEmpty(searchInput.FilePattern))
            {
                args += $" --include=\"{searchInput.FilePattern}\"";
            }
            
            args += $" \"{searchInput.Pattern}\"";
            
            if (!string.IsNullOrEmpty(searchInput.Path))
            {
                args += $" \"{searchInput.Path}\"";
            }
            else
            {
                args += " .";
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "grep",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return output;
            }
            else if (process.ExitCode == 1)
            {
                // No matches found
                return "No matches found";
            }
            else
            {
                throw new Exception($"grep failed: {error}");
            }
        }
    }

    public class CodeSearchInput
    {
        [JsonProperty("pattern")]
        public string Pattern { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }
        
        [JsonProperty("file_pattern")]
        public string FilePattern { get; set; }
        
        [JsonProperty("case_sensitive")]
        public bool? CaseSensitive { get; set; }
    }
}