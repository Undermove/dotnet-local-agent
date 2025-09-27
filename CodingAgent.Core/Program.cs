using System;
using System.Threading.Tasks;

namespace CodingAgent.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            var command = args[0].ToLower();
            var remainingArgs = new string[args.Length - 1];
            Array.Copy(args, 1, remainingArgs, 0, args.Length - 1);

            try
            {
                switch (command)
                {
                    case "chat":
                        await ChatProgram.RunAsync(remainingArgs);
                        break;
                    case "read":
                        await ReadFileProgram.RunAsync(remainingArgs);
                        break;
                    case "list":
                        await ListFilesProgram.RunAsync(remainingArgs);
                        break;
                    case "bash":
                        await BashToolProgram.RunAsync(remainingArgs);
                        break;
                    case "edit":
                        await EditToolProgram.RunAsync(remainingArgs);
                        break;
                    case "search":
                        await CodeSearchToolProgram.RunAsync(remainingArgs);
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (Array.Exists(remainingArgs, arg => arg == "--verbose"))
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("🧠 C# Coding Agent - Build Your Own AI Assistant");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  chat    - Basic chat with AI");
            Console.WriteLine("  read    - Chat with file reading capability");
            Console.WriteLine("  list    - Chat with file listing capability");
            Console.WriteLine("  bash    - Chat with shell command execution");
            Console.WriteLine("  edit    - Chat with file editing capability");
            Console.WriteLine("  search  - Chat with code search capability");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --verbose           Enable detailed logging");
            Console.WriteLine("  --provider <name>   AI provider: anthropic (default) or lmstudio");
            Console.WriteLine("  --model <name>      Model name (optional)");
            Console.WriteLine("  --base-url <url>    Base URL for LM Studio (default: http://localhost:1234)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run chat");
            Console.WriteLine("  dotnet run chat --provider lmstudio");
            Console.WriteLine("  dotnet run read --provider lmstudio --base-url http://localhost:1234");
            Console.WriteLine("  dotnet run edit --verbose --provider anthropic");
            Console.WriteLine();
            Console.WriteLine("Environment Variables:");
            Console.WriteLine("  ANTHROPIC_API_KEY  Required for Anthropic provider");
            Console.WriteLine("  AI_PROVIDER        Default provider (anthropic or lmstudio)");
            Console.WriteLine("  LM_STUDIO_URL      Default LM Studio URL");
        }
    }
}