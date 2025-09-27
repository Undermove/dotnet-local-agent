using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CodingAgent.Core;

public enum AIProviderType
{
    Anthropic,
    LMStudio
}

public interface IAIProvider
{
    Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false);
}

public class AnthropicProvider : IAIProvider
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public AnthropicProvider(string apiKey, string model = "claude-3-5-sonnet-20241022")
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
    }

    public async Task<string> SendMessageAsync(string message, List<object>? tools = null, bool verbose = false)
    {
        var messageParams = new MessageParameters
        {
            Messages = [new Message(RoleType.User, message)],
            Model = _model,
            MaxTokens = 4096,
            Stream = false
        };

        if (tools != null && tools.Count > 0)
        {
            messageParams.Tools = tools.Cast<Anthropic.SDK.Common.Tool>().ToList();
        }

        if (verbose)
        {
            Console.WriteLine($"🤖 Sending message to Claude ({_model})...");
        }

        var response = await _client.Messages.GetClaudeMessageAsync(messageParams);
        
        if (response.Content.FirstOrDefault() is TextContent textContent)
        {
            return textContent.Text;
        }

        return "No response received.";
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

        // Примечание: LM Studio может не поддерживать tools в зависимости от модели
        // Поэтому пока не добавляем tools для LM Studio

        if (verbose)
        {
            Console.WriteLine($"🤖 Sending message to LM Studio ({_model})...");
        }

        try
        {
            var chatClient = _client.GetChatClient(_model);
            var response = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
            {
                Temperature = 0.7f
            });
            
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error connecting to LM Studio: {ex.Message}. Make sure LM Studio is running on the configured endpoint";
        }
    }
}

public static class AIProviderFactory
{
    public static IAIProvider CreateProvider(AIProviderType providerType, string? apiKey = null, string? baseUrl = null, string? model = null)
    {
        return providerType switch
        {
            AIProviderType.Anthropic => new AnthropicProvider(
                apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set"),
                model ?? "claude-3-5-sonnet-20241022"
            ),
            AIProviderType.LMStudio => new LMStudioProvider(
                baseUrl ?? "http://localhost:1234",
                model ?? "local-model"
            ),
            _ => throw new ArgumentException($"Unsupported provider type: {providerType}")
        };
    }
}