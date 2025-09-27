using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Generation;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core
{
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
                var provider = cmdArgs.CreateProvider();

                if (cmdArgs.Provider == AIProviderType.Anthropic)
                {
                    // Используем полнофункциональный агент с инструментами для Anthropic
                    var client = new AnthropicClient(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
                    
                    if (cmdArgs.Verbose)
                    {
                        Console.WriteLine("Anthropic client initialized");
                    }

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
                return;
            }
        }

        private static string GetUserMessage()
        {
            return Console.ReadLine();
        }
    }

    public class AgentWithTools
    {
        private readonly AnthropicClient _client;
        private readonly Func<string> _getUserMessage;
        private readonly List<ToolDefinition> _tools;
        private readonly bool _verbose;

        public AgentWithTools(AnthropicClient client, Func<string> getUserMessage, List<ToolDefinition> tools, bool verbose)
        {
            _client = client;
            _getUserMessage = getUserMessage;
            _tools = tools;
            _verbose = verbose;
        }

        public async Task RunAsync()
        {
            var conversation = new List<Message>();

            if (_verbose)
            {
                Console.WriteLine("Starting chat session with tools enabled");
            }
            Console.WriteLine("Chat with Claude (use 'ctrl-c' to quit)");

            while (true)
            {
                Console.Write("\u001b[94mYou\u001b[0m: ");
                var userInput = _getUserMessage();
                
                if (userInput == null)
                {
                    if (_verbose)
                    {
                        Console.WriteLine("User input ended, breaking from chat loop");
                    }
                    break;
                }

                // Skip empty messages
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    if (_verbose)
                    {
                        Console.WriteLine("Skipping empty message");
                    }
                    continue;
                }

                if (_verbose)
                {
                    Console.WriteLine($"User input received: \"{userInput}\"");
                }

                var userMessage = new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase> { new TextContent { Text = userInput } }
                };
                conversation.Add(userMessage);

                if (_verbose)
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

                    if (_verbose)
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
                            if (_verbose)
                            {
                                Console.WriteLine($"Tool use detected: {toolUse.Name} with input: {toolUse.Input}");
                            }
                            Console.WriteLine($"\u001b[96mtool\u001b[0m: {toolUse.Name}({toolUse.Input})");

                            // Find and execute the tool
                            string toolResult = null;
                            Exception toolError = null;
                            bool toolFound = false;

                            foreach (var tool in _tools)
                            {
                                if (tool.Name == toolUse.Name)
                                {
                                    if (_verbose)
                                    {
                                        Console.WriteLine($"Executing tool: {tool.Name}");
                                    }
                                    try
                                    {
                                        toolResult = await tool.ExecuteAsync(toolUse.Input.ToString());
                                        Console.WriteLine($"\u001b[92mresult\u001b[0m: {toolResult}");
                                        if (_verbose)
                                        {
                                            Console.WriteLine($"Tool execution successful, result length: {toolResult.Length} chars");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        toolError = ex;
                                        Console.WriteLine($"\u001b[91merror\u001b[0m: {ex.Message}");
                                        if (_verbose)
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
                                toolError = new Exception($"tool '{toolUse.Name}' not found");
                                Console.WriteLine($"\u001b[91merror\u001b[0m: {toolError.Message}");
                            }

                            // Add tool result to collection
                            if (toolError != null)
                            {
                                toolResults.Add(new ToolResultContent
                                {
                                    ToolUseId = toolUse.Id,
                                    Content = new List<ContentBase> { new TextContent { Text = toolError.Message } },
                                    IsError = true
                                });
                            }
                            else
                            {
                                toolResults.Add(new ToolResultContent
                                {
                                    ToolUseId = toolUse.Id,
                                    Content = new List<ContentBase> { new TextContent { Text = toolResult ?? "" } },
                                    IsError = false
                                });
                            }
                        }
                    }

                    // If there were no tool uses, we're done
                    if (!hasToolUse)
                    {
                        break;
                    }

                    // Send all tool results back and get Claude's response
                    if (_verbose)
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

                    if (_verbose)
                    {
                        Console.WriteLine($"Received followup response with {response.Content.Count} content blocks");
                    }

                    // Continue loop to process the new message
                }
            }

            if (_verbose)
            {
                Console.WriteLine("Chat session ended");
            }
        }

        private async Task<Message> RunInferenceAsync(List<Message> conversation)
        {
            var anthropicTools = new List<Anthropic.SDK.Common.Tool>();
            foreach (var tool in _tools)
            {
                var function = new Anthropic.SDK.Common.Function(
                    tool.Name,
                    tool.Description,
                    System.Text.Json.Nodes.JsonNode.Parse(JsonConvert.SerializeObject(tool.InputSchema))
                );
                anthropicTools.Add(new Anthropic.SDK.Common.Tool(function));
            }

            if (_verbose)
            {
                Console.WriteLine($"Making API call to Claude with model: claude-3-sonnet-20240229 and {anthropicTools.Count} tools");
            }

            try
            {
                var parameters = new MessageParameters
                {
                    Model = "claude-3-sonnet-20240229",
                    MaxTokens = 1024,
                    Messages = conversation,
                    Tools = anthropicTools,
                    Stream = false
                };

                var response = await _client.Messages.GetClaudeMessageAsync(parameters);

                if (_verbose)
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
                if (_verbose)
                {
                    Console.WriteLine($"API call failed: {ex.Message}");
                }
                return null;
            }
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
                var content = await File.ReadAllTextAsync(readFileInput.Path);
                Console.WriteLine($"Successfully read file {readFileInput.Path} ({content.Length} bytes)");
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read file {readFileInput.Path}: {ex.Message}");
                throw;
            }
        }
    }

    public class ReadFileInput
    {
        [JsonProperty("path")]
        public string Path { get; set; }
    }




}