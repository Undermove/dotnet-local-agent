namespace CodingAgent.Core;

public class CommandLineArgs
{
    public bool Verbose { get; set; }
    public AIProviderType Provider { get; set; } = AIProviderType.Anthropic;
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }

    public static CommandLineArgs Parse(string[] args)
    {
        var result = new CommandLineArgs();
        
        // Получаем провайдер по умолчанию из переменной окружения
        var defaultProvider = Environment.GetEnvironmentVariable("AI_PROVIDER")?.ToLower();
        if (defaultProvider == "lmstudio")
        {
            result.Provider = AIProviderType.LMStudio;
        }
        else if (defaultProvider == "openai")
        {
            result.Provider = AIProviderType.OpenAI;
        }

        // Получаем базовый URL для LM Studio из переменной окружения
        result.BaseUrl = Environment.GetEnvironmentVariable("LM_STUDIO_URL");

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--verbose":
                    result.Verbose = true;
                    break;
                    
                case "--provider":
                    if (i + 1 < args.Length)
                    {
                        var providerName = args[i + 1].ToLower();
                        result.Provider = providerName switch
                        {
                            "anthropic" => AIProviderType.Anthropic,
                            "lmstudio" => AIProviderType.LMStudio,
                            "openai" => AIProviderType.OpenAI,
                            _ => throw new ArgumentException($"Unknown provider: {args[i + 1]}")
                        };
                        i++; // Skip the next argument as it's the provider name
                    }
                    break;
                    
                case "--model":
                    if (i + 1 < args.Length)
                    {
                        result.Model = args[i + 1];
                        i++; // Skip the next argument as it's the model name
                    }
                    break;
                    
                case "--base-url":
                    if (i + 1 < args.Length)
                    {
                        result.BaseUrl = args[i + 1];
                        i++; // Skip the next argument as it's the base URL
                    }
                    break;
            }
        }

        return result;
    }

    public IAIProvider CreateProvider()
    {
        return AIProviderFactory.CreateProvider(Provider, null, BaseUrl, Model);
    }
}