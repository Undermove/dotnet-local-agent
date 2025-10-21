namespace CodingAgent.Core;

public class ChatProgram
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
            var agent = new Agent(provider, GetUserMessage, cmdArgs.Verbose);
            await agent.RunAsync();
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

public class Agent(IAIProvider provider, Func<string> getUserMessage, bool verbose)
{
    public async Task RunAsync()
    {
        if (verbose)
        {
            Console.WriteLine("Starting chat session");
        }
        Console.WriteLine("Chat with AI (use 'ctrl-c' to quit)");

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

            try
            {
                var response = await provider.SendMessageAsync(userInput, null, verbose);
                Console.WriteLine($"\u001b[93mAI\u001b[0m: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (verbose)
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        if (verbose)
        {
            Console.WriteLine("Chat session ended");
        }
    }


}