using System.Text.Json;
using OpenAI.Chat;

namespace CodingAgent.Core;

public class UniversalAgentWithTools
{
    private readonly IAIProvider _provider;
    private readonly Func<string> _getUserMessage;
    private readonly List<ToolDefinition> _tools;
    private readonly bool _verbose;

    public UniversalAgentWithTools(IAIProvider provider, Func<string> getUserMessage, List<ToolDefinition> tools, bool verbose)
    {
        _provider = provider;
        _getUserMessage = getUserMessage;
        _tools = tools;
        _verbose = verbose;
    }

    public async Task RunAsync()
    {
        var conversation = new List<ChatMessage>();

        // Добавляем системный промпт для Llama 3.1
        var systemPrompt = GenerateSystemPrompt();
        conversation.Add(new SystemChatMessage(systemPrompt));

        if (_verbose)
        {
            Console.WriteLine("Starting chat session with universal tools support");
            Console.WriteLine($"System prompt length: {systemPrompt.Length} characters");
        }
        Console.WriteLine("Chat with AI (use 'ctrl-c' to quit)");

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

            conversation.Add(new UserChatMessage(userInput));

            if (_verbose)
            {
                Console.WriteLine($"Sending message to AI, conversation length: {conversation.Count}");
            }

            var response = await RunInferenceAsync(conversation);
            if (response == null)
            {
                Console.WriteLine("Error: Failed to get response from AI");
                return;
            }

            // Обрабатываем ответ
            if (!string.IsNullOrEmpty(response.TextContent))
            {
                Console.WriteLine($"\u001b[93mAI\u001b[0m: {response.TextContent}");
                conversation.Add(new AssistantChatMessage(response.TextContent));
            }

            // Обрабатываем tool calls
            if (response.HasToolCalls)
            {
                await ProcessToolCalls(response.ToolCalls, conversation);
            }
        }

        if (_verbose)
        {
            Console.WriteLine("Chat session ended");
        }
    }

    private async Task ProcessToolCalls(List<ToolCall> toolCalls, List<ChatMessage> conversation)
    {
        var toolResults = new List<string>();

        foreach (var toolCall in toolCalls)
        {
            if (_verbose)
            {
                Console.WriteLine($"Tool call detected: {toolCall.Name} with arguments: {toolCall.Arguments}");
            }
            Console.WriteLine($"\u001b[96mtool\u001b[0m: {toolCall.Name}({toolCall.Arguments})");

            // Находим и выполняем инструмент
            string toolResult = null;
            Exception toolError = null;
            bool toolFound = false;

            foreach (var tool in _tools)
            {
                if (tool.Name == toolCall.Name)
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"Executing tool: {tool.Name}");
                    }
                    try
                    {
                        toolResult = await tool.ExecuteAsync(toolCall.Arguments);
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
                toolError = new Exception($"tool '{toolCall.Name}' not found");
                Console.WriteLine($"\u001b[91merror\u001b[0m: {toolError.Message}");
            }

            // Добавляем результат инструмента в разговор
            var resultMessage = toolError != null 
                ? $"Error executing {toolCall.Name}: {toolError.Message}"
                : toolResult ?? "Tool executed successfully";
            
            toolResults.Add(resultMessage);
        }

        // Добавляем результаты инструментов в разговор
        if (toolResults.Count > 0)
        {
            var toolResultsText = string.Join("\n", toolResults.Select((result, i) => 
                $"Tool {toolCalls[i].Name} result: {result}"));
            conversation.Add(new UserChatMessage($"Tool execution results:\n{toolResultsText}"));

            // Получаем следующий ответ от AI
            var followupResponse = await RunInferenceAsync(conversation);
            if (followupResponse != null && !string.IsNullOrEmpty(followupResponse.TextContent))
            {
                Console.WriteLine($"\u001b[93mAI\u001b[0m: {followupResponse.TextContent}");
                conversation.Add(new AssistantChatMessage(followupResponse.TextContent));
            }
        }
    }

    private async Task<AIResponse?> RunInferenceAsync(List<ChatMessage> conversation)
    {
        // Конвертируем наши ToolDefinition в ChatTool для OpenAI SDK
        var openAITools = ToolConverter.ConvertToOpenAITools(_tools);

        if (_verbose)
        {
            Console.WriteLine($"Making API call with {openAITools.Count} tools");
        }

        try
        {
            var response = await _provider.SendMessageWithToolsAsync(conversation, openAITools.Cast<object>().ToList(), _verbose);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during inference: {ex.Message}");
            if (_verbose)
            {
                Console.WriteLine($"Full error: {ex}");
            }
            return null;
        }
    }

    private string GenerateSystemPrompt()
    {
        var toolDescriptions = _tools.Select(tool => 
            $"- {tool.Name}: {tool.Description}").ToList();

        return $@"You are a helpful AI assistant with access to the following tools:

{string.Join("\n", toolDescriptions)}

CRITICAL INSTRUCTIONS:
1. You MUST use the available tools when the user requests actions that these tools can perform
2. NEVER provide code examples or manual instructions when you can use a tool directly
3. When the user asks you to create, read, edit, or search files - USE THE TOOLS IMMEDIATELY
4. When the user asks you to run commands - USE THE bash_command tool
5. Always use tools first, then provide explanations if needed

EXAMPLES OF CORRECT BEHAVIOR:
- User: ""Create a file test.txt with content 'Hello'"" → USE edit_file with {{""path"": ""test.txt"", ""old_str"": """", ""new_str"": ""Hello""}}
- User: ""Read the contents of config.json"" → USE read_file tool immediately  
- User: ""Find all Python files"" → USE list_files tool immediately
- User: ""Search for 'TODO' in the code"" → USE code_search tool immediately
- User: ""Run npm install"" → USE bash_command tool immediately

EDIT_FILE TOOL USAGE EXAMPLES:
✅ CORRECT - Creating new file: {{""path"": ""hello.txt"", ""old_str"": """", ""new_str"": ""Hello World!""}}
✅ CORRECT - Editing existing file: {{""path"": ""config.py"", ""old_str"": ""DEBUG = False"", ""new_str"": ""DEBUG = True""}}
❌ WRONG - Empty new_str: {{""path"": ""test.txt"", ""old_str"": """", ""new_str"": """"}}
❌ WRONG - Same old_str and new_str: {{""path"": ""file.txt"", ""old_str"": ""text"", ""new_str"": ""text""}}

EXAMPLES OF WRONG BEHAVIOR (DO NOT DO THIS):
- Saying ""I cannot create files"" when you have edit_file tool
- Providing bash commands instead of using bash_command tool
- Giving manual instructions instead of using available tools
- Explaining how to do something instead of doing it with tools

Remember: You have real capabilities through these tools. Use them actively and immediately when appropriate!";
    }
}