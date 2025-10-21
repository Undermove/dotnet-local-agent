using System.Diagnostics;
using Anthropic.SDK;
using Newtonsoft.Json;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core;

public class BashToolProgram
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
                    BashDefinition.Instance
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
                Console.WriteLine("You can still chat, but bash execution tools are not available.");
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
        }
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