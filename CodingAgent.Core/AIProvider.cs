using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CodingAgent.Core;

public enum AIProviderType
{
    Anthropic,
    LMStudio,
    OpenAI
}

public interface IAIProvider
{
    Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false);
    Task<AIResponse> SendMessageWithToolsAsync(List<ChatMessage> messages, List<object>? tools = null, bool verbose = false);
}

public class AIResponse
{
    public string? TextContent { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = new();
    public bool HasToolCalls => ToolCalls.Count > 0;
}

public class ToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}

public class AnthropicProvider(string apiKey, string model = "claude-3-5-sonnet-20241022") : IAIProvider
{
    private readonly AnthropicClient _client = new(apiKey);

    public async Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false)
    {
        var messageParams = new MessageParameters
        {
            Messages = [new Message(RoleType.User, message)],
            Model = model,
            MaxTokens = 4096,
            Stream = false
        };

        if (tools != null && tools.Count > 0)
        {
            messageParams.Tools = tools.Cast<Anthropic.SDK.Common.Tool>().ToList();
        }

        if (verbose)
        {
            Console.WriteLine($"🤖 Sending message to Claude ({model})...");
        }

        var response = await _client.Messages.GetClaudeMessageAsync(messageParams);
        
        if (response.Content.FirstOrDefault() is TextContent textContent)
        {
            return textContent.Text;
        }

        return "No response received.";
    }

    public async Task<AIResponse> SendMessageWithToolsAsync(List<ChatMessage> messages, List<object>? tools = null, bool verbose = false)
    {
        // Для Anthropic пока используем старый метод и конвертируем результат
        var firstMessage = messages.LastOrDefault()?.Content?.FirstOrDefault()?.ToString() ?? "";
        var textResponse = await SendMessageAsync(firstMessage, tools, verbose);
        
        return new AIResponse
        {
            TextContent = textResponse,
            ToolCalls = new List<ToolCall>() // TODO: Реализовать парсинг tool calls из Anthropic response
        };
    }
}

public class OpenAIProvider(string apiKey, string model = "gpt-4") : IAIProvider
{
    private readonly OpenAIClient _client = new(new ApiKeyCredential(apiKey));

    public async Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false)
    {
        var messages = new List<ChatMessage>
        {
            new UserChatMessage(message)
        };

        if (verbose)
        {
            Console.WriteLine($"🤖 Sending message to OpenAI ({model})...");
            if (tools != null && tools.Count > 0)
            {
                Console.WriteLine($"📋 Sending {tools.Count} tools to model");
            }
        }

        try
        {
            var chatClient = _client.GetChatClient(model);
            var options = new ChatCompletionOptions
            {
            };

            if (tools != null && tools.Count > 0)
            {
                var openAITools = new List<ChatTool>();
                foreach (var tool in tools)
                {
                    if (tool is ChatTool chatTool)
                    {
                        openAITools.Add(chatTool);
                    }
                }
                
                if (openAITools.Count > 0)
                {
                    foreach (var tool in openAITools)
                    {
                        options.Tools.Add(tool);
                    }
                    if (verbose)
                    {
                        Console.WriteLine($"✅ Added {openAITools.Count} tools to request");
                    }
                }
            }
            
            var response = await chatClient.CompleteChatAsync(messages, options);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error connecting to OpenAI: {ex.Message}. Make sure OPENAI_API_KEY is set correctly.";
        }
    }

    public async Task<AIResponse> SendMessageWithToolsAsync(List<ChatMessage> messages, List<object>? tools = null, bool verbose = false)
    {
        if (verbose)
        {
            Console.WriteLine($"🤖 Sending {messages.Count} messages to OpenAI ({model})...");
            if (tools != null && tools.Count > 0)
            {
                Console.WriteLine($"📋 Sending {tools.Count} tools to model");
            }
        }

        try
        {
            var chatClient = _client.GetChatClient(model);
            var options = new ChatCompletionOptions
            {
            };

            if (tools != null && tools.Count > 0)
            {
                if (verbose)
                {
                    Console.WriteLine($"🔍 Processing {tools.Count} tools for OpenAI...");
                }
                
                var openAITools = new List<ChatTool>();
                foreach (var tool in tools)
                {
                    if (tool is ChatTool chatTool)
                    {
                        openAITools.Add(chatTool);
                        if (verbose)
                        {
                            Console.WriteLine($"🔧 Tool: {chatTool.Kind} - Function: {chatTool.FunctionName}");
                        }
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"⚠️ Skipping tool of type: {tool.GetType().Name}");
                    }
                }
                
                if (openAITools.Count > 0)
                {
                    foreach (var tool in openAITools)
                    {
                        options.Tools.Add(tool);
                    }
                    if (verbose)
                    {
                        Console.WriteLine($"✅ Added {openAITools.Count} tools to request");
                    }
                }
                else if (verbose)
                {
                    Console.WriteLine($"❌ No valid ChatTool objects found in {tools.Count} provided tools");
                }
            }
            
            var response = await chatClient.CompleteChatAsync(messages, options);
            var chatResponse = response.Value;

            var aiResponse = new AIResponse();

            if (chatResponse.Content.Count > 0)
            {
                aiResponse.TextContent = chatResponse.Content[0].Text;
            }

            if (chatResponse.ToolCalls.Count > 0)
            {
                foreach (var toolCall in chatResponse.ToolCalls)
                {
                    aiResponse.ToolCalls.Add(new ToolCall
                    {
                        Id = toolCall.Id,
                        Name = toolCall.FunctionName,
                        Arguments = toolCall.FunctionArguments.ToString()
                    });
                }

                if (verbose)
                {
                    Console.WriteLine($"🔧 Received {aiResponse.ToolCalls.Count} tool calls from model");
                }
            }

            return aiResponse;
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"❌ OpenAI API Error: {ex.GetType().Name}");
                Console.WriteLine($"❌ Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
            }
            
            return new AIResponse
            {
                TextContent = $"Error connecting to OpenAI: {ex.Message}. Make sure OPENAI_API_KEY is set correctly."
            };
        }
    }
}

public class LMStudioProvider : IAIProvider
{
    private readonly OpenAIClient _client;
    private readonly string _model;

    public LMStudioProvider(string baseUrl = "http://localhost:1234", string model = "local-model")
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri($"{baseUrl}/v1")
        };
        
        // LM Studio не требует реального API ключа, но SDK требует непустую строку
        _client = new OpenAIClient(new ApiKeyCredential("lm-studio"), options);
        _model = model;
    }

    public async Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false)
    {
        var messages = new List<ChatMessage>
        {
            new UserChatMessage(message)
        };

        if (verbose)
        {
            Console.WriteLine($"🤖 Sending message to LM Studio ({_model})...");
            if (tools != null && tools.Count > 0)
            {
                Console.WriteLine($"📋 Sending {tools.Count} tools to model");
            }
        }

        try
        {
            var chatClient = _client.GetChatClient(_model);
            var options = new ChatCompletionOptions
            {
            };

            // Добавляем поддержку tools для моделей, которые их поддерживают (например, Llama 3.1)
            if (tools != null && tools.Count > 0)
            {
                // Конвертируем tools в формат OpenAI
                var openAITools = new List<ChatTool>();
                foreach (var tool in tools)
                {
                    if (tool is ChatTool chatTool)
                    {
                        openAITools.Add(chatTool);
                    }
                }
                
                if (openAITools.Count > 0)
                {
                    // Используем правильный способ добавления tools
                    foreach (var tool in openAITools)
                    {
                        options.Tools.Add(tool);
                    }
                    if (verbose)
                    {
                        Console.WriteLine($"✅ Added {openAITools.Count} tools to request");
                    }
                }
            }
            
            var response = await chatClient.CompleteChatAsync(messages, options);
            
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error connecting to LM Studio: {ex.Message}. Make sure LM Studio is running on the configured endpoint";
        }
    }

    public async Task<AIResponse> SendMessageWithToolsAsync(List<ChatMessage> messages, List<object>? tools = null, bool verbose = false)
    {
        if (verbose)
        {
            Console.WriteLine($"🤖 Sending {messages.Count} messages to LM Studio ({_model})...");
            if (tools != null && tools.Count > 0)
            {
                Console.WriteLine($"📋 Sending {tools.Count} tools to model");
            }
        }

        try
        {
            var chatClient = _client.GetChatClient(_model);
            var options = new ChatCompletionOptions
            {
            };

            // Добавляем поддержку tools для моделей, которые их поддерживают (например, Llama 3.1)
            if (tools != null && tools.Count > 0)
            {
                if (verbose)
                {
                    Console.WriteLine($"🔍 Processing {tools.Count} tools for LM Studio...");
                }
                
                // Конвертируем tools в формат OpenAI
                var openAITools = new List<ChatTool>();
                foreach (var tool in tools)
                {
                    if (tool is ChatTool chatTool)
                    {
                        openAITools.Add(chatTool);
                        if (verbose)
                        {
                            Console.WriteLine($"🔧 Tool: {chatTool.Kind} - Function: {chatTool.FunctionName}");
                        }
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"⚠️ Skipping tool of type: {tool.GetType().Name}");
                    }
                }
                
                if (openAITools.Count > 0)
                {
                    // Используем правильный способ добавления tools
                    foreach (var tool in openAITools)
                    {
                        options.Tools.Add(tool);
                    }
                    if (verbose)
                    {
                        Console.WriteLine($"✅ Added {openAITools.Count} tools to request");
                    }
                }
                else if (verbose)
                {
                    Console.WriteLine($"❌ No valid ChatTool objects found in {tools.Count} provided tools");
                }
            }
            
            var response = await chatClient.CompleteChatAsync(messages, options);
            var chatResponse = response.Value;

            var aiResponse = new AIResponse();

            // Обрабатываем текстовый контент
            if (chatResponse.Content.Count > 0)
            {
                aiResponse.TextContent = chatResponse.Content[0].Text;
            }

            // Обрабатываем tool calls
            if (chatResponse.ToolCalls.Count > 0)
            {
                foreach (var toolCall in chatResponse.ToolCalls)
                {
                    aiResponse.ToolCalls.Add(new ToolCall
                    {
                        Id = toolCall.Id,
                        Name = toolCall.FunctionName,
                        Arguments = toolCall.FunctionArguments.ToString()
                    });
                }

                if (verbose)
                {
                    Console.WriteLine($"🔧 Received {aiResponse.ToolCalls.Count} tool calls from model");
                }
            }

            return aiResponse;
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"❌ LM Studio API Error: {ex.GetType().Name}");
                Console.WriteLine($"❌ Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
            }
            
            return new AIResponse
            {
                TextContent = $"Error connecting to LM Studio: {ex.Message}. Make sure LM Studio is running on the configured endpoint"
            };
        }
    }
}

public static class AIProviderFactory
{
    public static IAIProvider CreateProvider(AIProviderType providerType, string? apiKey = null, string? baseUrl = null, string? model = null)
    {
        return providerType switch
        {
            AIProviderType.Anthropic => CreateAnthropicProvider(apiKey, model),
            AIProviderType.LMStudio => new LMStudioProvider(
                baseUrl ?? Environment.GetEnvironmentVariable("LM_STUDIO_URL") ?? "http://localhost:1234",
                model ?? "local-model"
            ),
            AIProviderType.OpenAI => CreateOpenAIProvider(apiKey, model),
            _ => throw new ArgumentException($"Unsupported provider type: {providerType}")
        };
    }

    private static AnthropicProvider CreateAnthropicProvider(string? apiKey, string? model)
    {
        var key = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set. Please set it or use --provider lmstudio for local AI.");
        }
        return new AnthropicProvider(key, model ?? "claude-3-5-sonnet-20241022");
    }

    private static OpenAIProvider CreateOpenAIProvider(string? apiKey, string? model)
    {
        var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set. Please set it or use --provider lmstudio for local AI.");
        }
        return new OpenAIProvider(key, model ?? "gpt-4");
    }
}