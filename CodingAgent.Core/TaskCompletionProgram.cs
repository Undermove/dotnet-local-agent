namespace CodingAgent.Core
{
    public class TaskCompletionProgram
    {
        public static async Task RunAsync(string[] args)
        {
            var cmdArgs = CommandLineArgs.Parse(args);

            if (cmdArgs.Verbose)
            {
                Console.WriteLine("🧠 Task Completion Agent - Advanced AI Assistant");
                Console.WriteLine("Verbose logging enabled");
                Console.WriteLine($"Using provider: {cmdArgs.Provider}");
            }

            try
            {
                var provider = cmdArgs.CreateProvider();

                // Используем основные инструменты для Task Completion Agent
                // CodeSearchDefinition исключен, так как он требует ripgrep и не критичен для основной функциональности
                var tools = new List<ToolDefinition> 
                { 
                    ReadFileDefinition.Instance,
                    ListFilesDefinition.Instance,
                    BashDefinition.Instance,
                    EditFileDefinition.Instance,
                    TaskValidationTools.BuildProjectDefinition,
                    TaskValidationTools.RunTestsDefinition,
                    TaskValidationTools.LintCodeDefinition,
                    TaskValidationTools.CheckFileStatusDefinition
                };
                
                if (cmdArgs.Verbose)
                {
                    Console.WriteLine($"Initialized {tools.Count} tools for {cmdArgs.Provider} provider");
                    Console.WriteLine($"Tools: {string.Join(", ", tools.Select(t => t.Name))}");
                    if (cmdArgs.Provider == AIProviderType.LMStudio)
                    {
                        Console.WriteLine("Note: Tool support depends on the model. Llama 3.1 8B Instruct should work well.");
                    }
                }

                // Конфигурация агента
                var config = new TaskCompletionConfig
                {
                    MaxIterations = 50,
                    MaxSubtaskAttempts = 3,
                    MaxFailedSubtasks = 5,
                    MaxExecutionTimeMinutes = 30
                };

                var agent = new TaskCompletionAgent(provider, tools, cmdArgs.Verbose, config);
                
                Console.WriteLine("🎯 Task Completion Agent");
                Console.WriteLine("This agent will break down complex tasks and work to complete them systematically.");
                Console.WriteLine("It uses the TOTE cycle: Test-Operate-Test-Exit to ensure thorough completion.");
                Console.WriteLine();
                
                await RunInteractiveMode(agent, cmdArgs.Verbose);
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

        private static async Task RunInteractiveMode(TaskCompletionAgent agent, bool verbose)
        {
            Console.WriteLine("Enter your task description (or 'quit' to exit):");
            
            while (true)
            {
                Console.Write("\u001b[94mTask\u001b[0m: ");
                var taskInput = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(taskInput) || taskInput.ToLower() == "quit")
                {
                    Console.WriteLine("Goodbye! 👋");
                    break;
                }

                Console.WriteLine();
                Console.WriteLine("Any constraints? (press Enter to skip, or provide comma-separated constraints):");
                Console.Write("\u001b[93mConstraints\u001b[0m: ");
                var constraintsInput = Console.ReadLine();
                
                var constraints = new List<string>();
                if (!string.IsNullOrWhiteSpace(constraintsInput))
                {
                    constraints = constraintsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();
                }

                Console.WriteLine();
                Console.WriteLine("🚀 Starting task execution...");
                Console.WriteLine(new string('=', 60));

                try
                {
                    var result = await agent.CompleteTaskAsync(taskInput, constraints);
                    
                    Console.WriteLine();
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine("📊 TASK COMPLETION SUMMARY");
                    Console.WriteLine(new string('=', 60));
                    
                    Console.WriteLine($"Task: {result.TaskDescription}");
                    Console.WriteLine($"Status: {(result.Success ? "✅ COMPLETED" : "❌ FAILED")}");
                    Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1} seconds");
                    Console.WriteLine($"Iterations: {result.TotalIterations}");
                    
                    if (result.Constraints.Any())
                    {
                        Console.WriteLine($"Constraints: {string.Join(", ", result.Constraints)}");
                    }
                    
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Console.WriteLine($"Error: {result.Error}");
                    }

                    if (result.Plan != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine("📋 SUBTASKS SUMMARY:");
                        var completed = result.Plan.Subtasks.Count(s => s.Status == SubtaskStatus.Completed);
                        var failed = result.Plan.Subtasks.Count(s => s.Status == SubtaskStatus.Failed);
                        var pending = result.Plan.Subtasks.Count(s => s.Status == SubtaskStatus.Pending);
                        
                        Console.WriteLine($"  ✅ Completed: {completed}");
                        Console.WriteLine($"  ❌ Failed: {failed}");
                        Console.WriteLine($"  ⏳ Pending: {pending}");
                        
                        if (verbose)
                        {
                            Console.WriteLine();
                            Console.WriteLine("📝 DETAILED SUBTASKS:");
                            foreach (var subtask in result.Plan.Subtasks)
                            {
                                var statusIcon = subtask.Status switch
                                {
                                    SubtaskStatus.Completed => "✅",
                                    SubtaskStatus.Failed => "❌",
                                    SubtaskStatus.InProgress => "🔄",
                                    _ => "⏳"
                                };
                                Console.WriteLine($"  {statusIcon} {subtask.Id}: {subtask.Description}");
                                if (subtask.AttemptCount > 1)
                                {
                                    Console.WriteLine($"     (Attempts: {subtask.AttemptCount})");
                                }
                            }
                        }
                    }

                    if (verbose && result.Iterations.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("🔄 ITERATION DETAILS:");
                        foreach (var iteration in result.Iterations)
                        {
                            var statusIcon = iteration.Success ? "✅" : "❌";
                            Console.WriteLine($"  {statusIcon} Iteration {iteration.IterationNumber}: {iteration.SelectedSubtask?.Description ?? "No subtask"}");
                            if (!string.IsNullOrEmpty(iteration.Error))
                            {
                                Console.WriteLine($"     Error: {iteration.Error}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"💥 Unexpected error during task execution: {ex.Message}");
                    if (verbose)
                    {
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine(new string('=', 60));
                Console.WriteLine("Ready for next task!");
                Console.WriteLine();
            }
        }
    }
}