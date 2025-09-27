using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace CodingAgent.Core
{
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
                return;
            }
        }

        private static string GetUserMessage()
        {
            return Console.ReadLine();
        }
    }

    public class Agent
    {
        private readonly IAIProvider _provider;
        private readonly Func<string> _getUserMessage;
        private readonly bool _verbose;

        public Agent(IAIProvider provider, Func<string> getUserMessage, bool verbose)
        {
            _provider = provider;
            _getUserMessage = getUserMessage;
            _verbose = verbose;
        }

        public async Task RunAsync()
        {
            if (_verbose)
            {
                Console.WriteLine("Starting chat session");
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

                try
                {
                    var response = await _provider.SendMessageAsync(userInput, null, _verbose);
                    Console.WriteLine($"\u001b[93mAI\u001b[0m: {response}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (_verbose)
                    {
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }
            }

            if (_verbose)
            {
                Console.WriteLine("Chat session ended");
            }
        }


    }
}