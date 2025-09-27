using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Anthropic.SDK;
using Newtonsoft.Json;
using NJsonSchema.Generation;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core
{
    public class BashToolProgram
    {
        public static async Task RunAsync(string[] args)
        {
            bool verbose = Array.Exists(args, arg => arg == "--verbose");

            if (verbose)
            {
                Console.WriteLine("Verbose logging enabled");
            }

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: ANTHROPIC_API_KEY environment variable is not set");
                return;
            }

            var client = new AnthropicClient(apiKey);
            if (verbose)
            {
                Console.WriteLine("Anthropic client initialized");
            }

            var tools = new List<ToolDefinition> 
            { 
                ReadFileDefinition.Instance,
                ListFilesDefinition.Instance,
                BashDefinition.Instance
            };
            
            if (verbose)
            {
                Console.WriteLine($"Initialized {tools.Count} tools");
            }

            var agent = new AgentWithTools(client, GetUserMessage, tools, verbose);
            await agent.RunAsync();
        }

        private static string GetUserMessage()
        {
            return Console.ReadLine();
        }
    }

    public static class BashDefinition
    {
        public static ToolDefinition Instance = new ToolDefinition
        {
            Name = "bash",
            Description = "Execute a bash command and return its output. Use this to run shell commands.",
            InputSchema = GenerateSchema<BashInput>(),
            ExecuteAsync = BashAsync
        };

        private static async Task<string> BashAsync(string input)
        {
            var bashInput = JsonConvert.DeserializeObject<BashInput>(input);
            
            Console.WriteLine($"Executing bash command: {bashInput.Command}");
            
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashInput.Command.Replace("\"", "\\\"")}\"",
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

                var result = "";
                if (!string.IsNullOrEmpty(output))
                {
                    result += output;
                }
                if (!string.IsNullOrEmpty(error))
                {
                    if (!string.IsNullOrEmpty(result))
                        result += "\n";
                    result += $"STDERR: {error}";
                }

                if (process.ExitCode != 0)
                {
                    result += $"\nExit code: {process.ExitCode}";
                }

                Console.WriteLine($"Command executed with exit code: {process.ExitCode}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute command: {ex.Message}");
                throw;
            }
        }
    }

    public class BashInput
    {
        [JsonProperty("command")]
        public string Command { get; set; }
    }
}