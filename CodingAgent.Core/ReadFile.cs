using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Newtonsoft.Json;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core;

public class ReadFileProgram
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
            // Initialize path validator if working directory is specified
            PathValidator pathValidator = null;
            if (!string.IsNullOrEmpty(cmdArgs.WorkingDirectory))
            {
                pathValidator = new PathValidator(cmdArgs.WorkingDirectory);
                if (cmdArgs.Verbose)
                {
                    Console.WriteLine($"Working directory set to: {pathValidator.WorkingDirectory}");
                }
            }
            
            var provider = cmdArgs.CreateProvider();

            if (cmdArgs.Provider == AIProviderType.Anthropic)
            {
                // Используем полнофункциональный агент с инструментами для Anthropic
                var client = new AnthropicClient(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
                    
                if (cmdArgs.Verbose)
                {
                    Console.WriteLine("Anthropic client initialized");
                }

                // Set path validator for read_file tool
                ReadFileDefinition.PathValidator = pathValidator;

                var tools = new List<ToolDefinition> { ReadFileDefinition.Instance };
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
                Console.WriteLine("You can still chat, but file reading tools are not available.");
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

public class AgentWithTools(
    AnthropicClient client,
    Func<string> getUserMessage,
    List<ToolDefinition> tools,
    bool verbose)
{
    public async Task RunAsync()
    {
        var conversation = new List<Message>();

        if (verbose)
        {
            Console.WriteLine("Starting chat session with tools enabled");
        }
        Console.WriteLine("Chat with Claude (use 'ctrl-c' to quit)");

        while (true)
        {
            Console.Write("\u001b[94mYou\u001b[0m: ");
            var userInput = getUserMessage();
                
            if (userInput == null)
            {
                if (verbose)
                {
                    Console.WriteLine("User input ended, breaking from chat loop");
                }
                break;
            }

            // Skip empty messages
            if (string.IsNullOrWhiteSpace(userInput))
            {
                if (verbose)
                {
                    Console.WriteLine("Skipping empty message");
                }
                continue;
            }

            if (verbose)
            {
                Console.WriteLine($"User input received: \"{userInput}\"");
            }

            var userMessage = new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase> { new TextContent { Text = userInput } }
            };
            conversation.Add(userMessage);

            if (verbose)
            {
                Console.WriteLine($"Sending message to Claude, conversation length: {conversation.Count}");
            }

            var response = await RunInferenceAsync(conversation);
            if (response == null)
            {
                Console.WriteLine("Error: Failed to get response from Claude");
                return;
            }

            conversation.Add(response);

            // Keep processing until Claude stops using tools
            while (true)
            {
                var toolResults = new List<ToolResultContent>();
                var hasToolUse = false;

                if (verbose)
                {
                    Console.WriteLine($"Processing {response.Content.Count} content blocks from Claude");
                }

                foreach (var content in response.Content)
                {
                    if (content is TextContent textContent)
                    {
                        Console.WriteLine($"\u001b[93mClaude\u001b[0m: {textContent.Text}");
                    }
                    else if (content is ToolUseContent toolUse)
                    {
                        hasToolUse = true;
                        if (verbose)
                        {
                            Console.WriteLine($"Tool use detected: {toolUse.Name} with input: {toolUse.Input}");
                        }
                        Console.WriteLine($"\u001b[96mtool\u001b[0m: {toolUse.Name}({toolUse.Input})");

                        // Find and execute the tool
                        string toolResult = null;
                        bool toolFound = false;

                        foreach (var tool in tools)
                        {
                            if (tool.Name == toolUse.Name)
                            {
                                if (verbose)
                                {
                                    Console.WriteLine($"Executing tool: {tool.Name}");
                                }
                                try
                                {
                                    toolResult = await tool.ExecuteAsync(toolUse.Input.ToString());
                                    Console.WriteLine($"\u001b[92mresult\u001b[0m: {toolResult}");
                                    if (verbose)
                                    {
                                        Console.WriteLine($"Tool execution successful, result length: {toolResult.Length} chars");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Capture error and pass it as tool result
                                    toolResult = $"Error: {ex.Message}";
                                    Console.WriteLine($"\u001b[91merror\u001b[0m: {ex.Message}");
                                    if (verbose)
                                    {
                                        Console.WriteLine($"Tool execution failed: {ex}");
                                    }
                                }
                                toolFound = true;
                                break;
                            }
                        }

                        if (!toolFound)
                        {
                            toolResult = $"Error: tool '{toolUse.Name}' not found";
                            Console.WriteLine($"\u001b[91merror\u001b[0m: {toolResult}");
                        }

                        // Add tool result to collection (tools now return errors as strings)
                        toolResults.Add(new ToolResultContent
                        {
                            ToolUseId = toolUse.Id,
                            Content = new List<ContentBase> { new TextContent { Text = toolResult ?? "" } },
                            IsError = false
                        });
                    }
                }

                // If there were no tool uses, we're done
                if (!hasToolUse)
                {
                    break;
                }

                // Send all tool results back and get Claude's response
                if (verbose)
                {
                    Console.WriteLine($"Sending {toolResults.Count} tool results back to Claude");
                }

                var toolResultMessage = new Message
                {
                    Role = RoleType.User,
                    Content = toolResults.Cast<ContentBase>().ToList()
                };
                conversation.Add(toolResultMessage);

                // Get Claude's response after tool execution
                response = await RunInferenceAsync(conversation);
                if (response == null)
                {
                    Console.WriteLine("Error: Failed to get followup response from Claude");
                    return;
                }
                conversation.Add(response);

                if (verbose)
                {
                    Console.WriteLine($"Received followup response with {response.Content.Count} content blocks");
                }

                // Continue loop to process the new message
            }
        }

        if (verbose)
        {
            Console.WriteLine("Chat session ended");
        }
    }

    private async Task<Message> RunInferenceAsync(List<Message> conversation)
    {
        var anthropicTools = new List<Anthropic.SDK.Common.Tool>();
        foreach (var tool in tools)
        {
            var function = new Anthropic.SDK.Common.Function(
                tool.Name,
                tool.Description,
                System.Text.Json.Nodes.JsonNode.Parse(JsonConvert.SerializeObject(tool.InputSchema))
            );
            anthropicTools.Add(new Anthropic.SDK.Common.Tool(function));
        }

        if (verbose)
        {
            Console.WriteLine($"Making API call to Claude with model: claude-3-sonnet-20240229 and {anthropicTools.Count} tools");
        }

        try
        {
            var systemPrompt = GenerateSystemPrompt();
                
            var parameters = new MessageParameters
            {
                Model = "claude-3-sonnet-20240229",
                MaxTokens = 1024,
                Messages = conversation,
                Tools = anthropicTools,
                Stream = false,
                System = new List<SystemMessage> { new SystemMessage(systemPrompt) }
            };

            var response = await client.Messages.GetClaudeMessageAsync(parameters);

            if (verbose)
            {
                Console.WriteLine("API call successful, response received");
            }

            return new Message
            {
                Role = RoleType.Assistant,
                Content = response.Content
            };
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"API call failed: {ex.Message}");
            }
            return null;
        }
    }

    private string GenerateSystemPrompt()
    {
        var prompt = @"You are a helpful coding assistant with access to powerful tools for file operations and code analysis.

AVAILABLE TOOLS:
";

        foreach (var tool in tools)
        {
            prompt += $"- {tool.Name}: {tool.Description}\n";
        }

        prompt += @"
CRITICAL INSTRUCTIONS:
1. You MUST use the available tools to perform file operations
2. NEVER provide code examples or instructions - USE THE TOOLS DIRECTLY
3. When asked to create/edit a file, immediately use the edit_file tool
4. When asked to read a file, immediately use the read_file tool  
5. When asked to search code, immediately use the code_search tool
6. When asked to run commands, immediately use the bash tool
7. Do NOT explain how to do something - DO IT using the tools

EXAMPLES OF CORRECT BEHAVIOR:
- User: ""Create a file called test.txt with content 'hello'""
  → IMMEDIATELY call edit_file(Path=""test.txt"", OldStr="""", NewStr=""hello"")
- User: ""What's in the README file?""
  → IMMEDIATELY call read_file(Path=""README.md"")
- User: ""Find all TODO comments""
  → IMMEDIATELY call code_search tool to search for them
- User: ""Создай файл test.txt с содержимым 'Привет мир!'""
  → IMMEDIATELY call edit_file(Path=""test.txt"", OldStr="""", NewStr=""Привет мир!"")

WRONG BEHAVIOR (DO NOT DO THIS):
- Providing Python/bash code examples
- Explaining how to create files manually
- Giving step-by-step instructions

YOU HAVE REAL TOOLS - USE THEM IMMEDIATELY!";

        return prompt;
    }
}

public class ToolDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public object InputSchema { get; set; }
    public Func<string, Task<string>> ExecuteAsync { get; set; }
}

public static class ReadFileDefinition
{
    public static PathValidator? PathValidator { get; set; }

    public static ToolDefinition Instance = new ToolDefinition
    {
        Name = "read_file",
        Description = "Read the contents of a given relative file path. Use this when you want to see what's inside a file. Do not use this with directory names.",
        InputSchema = GenerateSchema<ReadFileInput>(),
        ExecuteAsync = ReadFileAsync
    };

    private static async Task<string> ReadFileAsync(string input)
    {
        var readFileInput = JsonConvert.DeserializeObject<ReadFileInput>(input);
            
        Console.WriteLine($"Reading file: {readFileInput.Path}");
            
        try
        {
            // Validate path is within working directory
            var validatedPath = readFileInput.Path;
            if (PathValidator != null)
            {
                validatedPath = PathValidator.ValidatePath(readFileInput.Path);
            }

            var content = await File.ReadAllTextAsync(validatedPath);
            Console.WriteLine($"Successfully read file {readFileInput.Path} ({content.Length} bytes)");
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read file {readFileInput.Path}: {ex.Message}");
            return $"Error reading file: {ex.Message}";
        }
    }
}

public class ReadFileInput
{
    [JsonProperty("path")]
    public string Path { get; set; }
}