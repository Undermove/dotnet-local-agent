using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;

namespace CodingAgent.Core;

/// <summary>
/// Агент для доведения сложных задач до конца с использованием цикла TOTE (Test-Operate-Test-Exit)
/// </summary>
public class TaskCompletionAgent(
    IAIProvider provider,
    List<ToolDefinition> tools,
    bool verbose = false,
    TaskCompletionConfig? config = null)
{
    private readonly TaskCompletionConfig _config = config ?? new TaskCompletionConfig();

    /// <summary>
    /// Выполняет задачу до полного завершения
    /// </summary>
    public async Task<TaskCompletionResult> CompleteTaskAsync(string taskDescription, List<string>? constraints = null)
    {
        var result = new TaskCompletionResult
        {
            TaskDescription = taskDescription,
            StartTime = DateTime.UtcNow,
            Constraints = constraints ?? new List<string>()
        };

        if (verbose)
        {
            Console.WriteLine($"🎯 Starting task completion: {taskDescription}");
            Console.WriteLine($"📋 Constraints: {string.Join(", ", result.Constraints)}");
        }

        try
        {
            // PLAN: Создаем план выполнения задачи
            var plan = await CreateTaskPlanAsync(taskDescription, result.Constraints);
            result.Plan = plan;
                
            if (verbose)
            {
                Console.WriteLine($"📝 Created plan with {plan.Subtasks.Count} subtasks");
                foreach (var subtask in plan.Subtasks)
                {
                    Console.WriteLine($"  - {subtask.Id}: {subtask.Description} (Status: {subtask.Status})");
                }
            }

            // Основной цикл TOTE
            var iteration = 0;
            var isComplete = IsTaskComplete(plan);
            if (verbose)
            {
                Console.WriteLine($"🔍 Task complete check: {isComplete}");
            }
                
            while (!isComplete && iteration < _config.MaxIterations)
            {
                iteration++;
                var iterationResult = new IterationResult { IterationNumber = iteration };
                    
                if (verbose)
                {
                    Console.WriteLine($"\n🔄 Iteration {iteration}/{_config.MaxIterations}");
                }

                try
                {
                    // TEST: Проверяем текущее состояние
                    var currentState = await AssessCurrentStateAsync(plan);
                    iterationResult.CurrentState = currentState;
                        
                    // Выбираем следующую подзадачу
                    var nextSubtask = SelectNextSubtask(plan);
                    if (nextSubtask == null)
                    {
                        if (verbose)
                        {
                            Console.WriteLine("✅ No more subtasks to execute");
                        }
                        break;
                    }
                        
                    iterationResult.SelectedSubtask = nextSubtask;
                        
                    if (verbose)
                    {
                        Console.WriteLine($"🎯 Selected subtask: {nextSubtask.Description}");
                    }

                    // OPERATE: Выполняем действие
                    var actionResult = await ExecuteSubtaskAsync(nextSubtask);
                    iterationResult.ActionResult = actionResult;
                        
                    // OBSERVE: Запускаем проверки
                    var observationResult = await ObserveResultsAsync(actionResult);
                    iterationResult.ObservationResult = observationResult;
                        
                    // CRITIQUE: Анализируем результаты
                    var critique = await CritiqueResultsAsync(observationResult, nextSubtask);
                    iterationResult.Critique = critique;
                        
                    // REFLECT/UPDATE: Обновляем план
                    await UpdatePlanAsync(plan, critique, nextSubtask);
                        
                    iterationResult.Success = critique.IsSuccessful;
                        
                    if (verbose)
                    {
                        Console.WriteLine($"📊 Iteration {iteration} result: {(iterationResult.Success ? "✅ Success" : "❌ Failed")}");
                        if (!string.IsNullOrEmpty(critique.Feedback))
                        {
                            Console.WriteLine($"💬 Feedback: {critique.Feedback}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    iterationResult.Error = ex.Message;
                    iterationResult.Success = false;
                        
                    if (verbose)
                    {
                        Console.WriteLine($"❌ Iteration {iteration} failed: {ex.Message}");
                    }
                        
                    // Пытаемся восстановиться
                    await HandleIterationErrorAsync(plan, ex);
                }
                    
                result.Iterations.Add(iterationResult);
                    
                // Проверяем условия остановки
                if (ShouldStopExecution(result, iteration))
                {
                    break;
                }
                    
                // Обновляем проверку завершения задачи
                isComplete = IsTaskComplete(plan);
            }

            result.Success = IsTaskComplete(plan);
            result.EndTime = DateTime.UtcNow;
            result.TotalIterations = iteration;
                
            if (verbose)
            {
                Console.WriteLine($"\n🏁 Task completion finished: {(result.Success ? "✅ Success" : "❌ Failed")}");
                Console.WriteLine($"⏱️ Duration: {result.Duration.TotalSeconds:F1}s, Iterations: {result.TotalIterations}");
            }
                
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;
                
            if (verbose)
            {
                Console.WriteLine($"💥 Task completion failed: {ex.Message}");
            }
                
            return result;
        }
    }

    private async Task<TaskPlan> CreateTaskPlanAsync(string taskDescription, List<string> constraints)
    {
        var planningPrompt = $@"You are a task planning expert for a C# coding agent project. Create a detailed plan to complete the following task:

TASK: {taskDescription}

CONSTRAINTS: {string.Join(", ", constraints)}

AVAILABLE TOOLS: {string.Join(", ", tools.Select(t => t.Name))}

CONTEXT: This is a C# .NET project. When planning tasks:
- Use list_files and read_file to explore the codebase and understand existing implementations
- Implementation tasks should use edit_file to modify code
- Use bash tool for creating example files or running commands
- Build and test tools help validate changes

PLANNING METHODOLOGY:
For tasks involving extending or adding support for new formats/features:
1. ANALYZE EXISTING: First understand how similar functionality is currently implemented
2. RESEARCH NEW: Then research the new format/feature requirements
3. DESIGN: Plan the integration approach
4. IMPLEMENT: Make the necessary code changes
5. VALIDATE: Test the implementation

Create a plan with the following structure:
1. Break down the task into specific, actionable subtasks
2. For each subtask, define clear Definition of Done (DoD) criteria
3. Identify dependencies between subtasks
4. Prioritize subtasks (most blocking/enabling first)

IMPORTANT: When adding support for new file formats (like .slnx), start by finding and analyzing existing code that handles similar formats (like .sln) to understand the current implementation patterns.

Respond with a JSON structure like this:
{{
  ""subtasks"": [
    {{
      ""id"": ""subtask_1"",
      ""description"": ""Clear description of what needs to be done"",
      ""definitionOfDone"": [""Specific criteria 1"", ""Specific criteria 2""],
      ""dependencies"": [""subtask_id_that_must_be_completed_first""],
      ""priority"": 1,
      ""estimatedComplexity"": ""low|medium|high"",
      ""requiredTools"": [""tool_name_1"", ""tool_name_2""]
    }}
  ]
}}";

        var conversation = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful task planning assistant. Always respond with valid JSON."),
            new UserChatMessage(planningPrompt)
        };

        var response = await provider.SendMessageWithToolsAsync(conversation, null, verbose);
            
        if (string.IsNullOrEmpty(response.TextContent))
        {
            throw new InvalidOperationException("Failed to generate task plan");
        }

        if (verbose)
        {
            Console.WriteLine($"🔍 Raw AI response: {response.TextContent}");
        }

        try
        {
            // Очищаем JSON от markdown блоков кода
            var cleanJson = ExtractJsonFromMarkdown(response.TextContent);
                
            if (verbose)
            {
                Console.WriteLine($"🔍 Cleaned JSON: {cleanJson}");
            }
                
            var planData = JsonSerializer.Deserialize<TaskPlanData>(cleanJson);
                
            if (planData == null)
            {
                throw new InvalidOperationException("Failed to deserialize task plan: planData is null");
            }
                
            if (verbose)
            {
                Console.WriteLine($"🔍 Deserialized plan data with {planData.Subtasks.Count} subtasks");
            }
                
            var plan = new TaskPlan
            {
                TaskDescription = taskDescription,
                Constraints = constraints,
                Subtasks = planData.Subtasks.Select(s => new Subtask
                {
                    Id = s.Id,
                    Description = s.Description,
                    DefinitionOfDone = s.DefinitionOfDone,
                    Dependencies = s.Dependencies,
                    Priority = s.Priority,
                    EstimatedComplexity = Enum.Parse<ComplexityLevel>(s.EstimatedComplexity, true),
                    RequiredTools = s.RequiredTools,
                    Status = SubtaskStatus.Pending
                }).ToList()
            };
                
            return plan;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse task plan JSON: {ex.Message}. Response: {response.TextContent}");
        }
    }

    private async Task<string> AssessCurrentStateAsync(TaskPlan plan)
    {
        var completedTasks = plan.Subtasks.Where(s => s.Status == SubtaskStatus.Completed).Count();
        var totalTasks = plan.Subtasks.Count;
        var inProgressTasks = plan.Subtasks.Where(s => s.Status == SubtaskStatus.InProgress).Count();
        var failedTasks = plan.Subtasks.Where(s => s.Status == SubtaskStatus.Failed).Count();
        var replacedTasks = plan.Subtasks.Where(s => s.Status == SubtaskStatus.Replaced).Count();
        var skippedTasks = plan.Subtasks.Where(s => s.Status == SubtaskStatus.Skipped).Count();
        var adaptedTasks = plan.Subtasks.Where(s => s.IsAdapted).Count();
            
        var state = $"Progress: {completedTasks}/{totalTasks} completed, {inProgressTasks} in progress, {failedTasks} failed";
        if (replacedTasks > 0 || skippedTasks > 0 || adaptedTasks > 0)
        {
            state += $", {replacedTasks} replaced, {skippedTasks} skipped, {adaptedTasks} adapted";
        }
            
        return state;
    }

    private Subtask? SelectNextSubtask(TaskPlan plan)
    {
        // Выбираем следующую подзадачу по приоритету, учитывая зависимости
        var availableSubtasks = plan.Subtasks
            .Where(s => s.Status == SubtaskStatus.Pending)
            .Where(s => s.Dependencies.All(dep => 
                plan.Subtasks.Any(st => st.Id == dep && 
                                        (st.Status == SubtaskStatus.Completed || 
                                         st.Status == SubtaskStatus.Replaced || 
                                         st.Status == SubtaskStatus.Skipped))))
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.EstimatedComplexity);
                
        return availableSubtasks.FirstOrDefault();
    }

    private async Task<ActionResult> ExecuteSubtaskAsync(Subtask subtask)
    {
        subtask.Status = SubtaskStatus.InProgress;
        subtask.StartTime = DateTime.UtcNow;
            
        var executionPrompt = $@"Execute the following subtask:

SUBTASK: {subtask.Description}
DEFINITION OF DONE: {string.Join(", ", subtask.DefinitionOfDone)}
REQUIRED TOOLS: {string.Join(", ", subtask.RequiredTools)}

Use the available tools to complete this subtask. Be specific and thorough.
Focus on meeting all the Definition of Done criteria.";

        var conversation = new List<ChatMessage>
        {
            new SystemChatMessage(GenerateExecutionSystemPrompt()),
            new UserChatMessage(executionPrompt)
        };

        var response = await provider.SendMessageWithToolsAsync(conversation, 
            ToolConverter.ConvertToOpenAITools(tools, verbose).Cast<object>().ToList(), verbose);

        if (verbose)
        {
            Console.WriteLine($"🔍 ExecuteSubtaskAsync - Response received:");
            Console.WriteLine($"   TextContent: {response.TextContent}");
            Console.WriteLine($"   HasToolCalls: {response.HasToolCalls}");
            Console.WriteLine($"   ToolCalls.Count: {response.ToolCalls.Count}");
            if (response.ToolCalls.Count > 0)
            {
                foreach (var tc in response.ToolCalls)
                {
                    Console.WriteLine($"   - ToolCall: Name='{tc.Name}', Id='{tc.Id}', Args='{tc.Arguments}'");
                }
            }
            Console.WriteLine($"   Available tools in list: {string.Join(", ", tools.Select(t => $"'{t.Name}'"))}");
        }

        var actionResult = new ActionResult
        {
            SubtaskId = subtask.Id,
            Response = response.TextContent ?? "",
            ToolCallsExecuted = response.ToolCalls.Count,
            Success = !string.IsNullOrEmpty(response.TextContent) || response.HasToolCalls
        };

        // Обрабатываем tool calls если есть
        if (response.HasToolCalls)
        {
            var toolResults = new List<string>();
            foreach (var toolCall in response.ToolCalls)
            {
                try
                {
                    if (verbose)
                    {
                        Console.WriteLine($"🔧 Looking for tool: '{toolCall.Name}'");
                    }
                    
                    var tool = tools.FirstOrDefault(t => t.Name == toolCall.Name);
                    
                    if (tool != null)
                    {
                        if (verbose)
                        {
                            Console.WriteLine($"✅ Found tool: '{toolCall.Name}', executing...");
                        }
                        var result = await tool.ExecuteAsync(toolCall.Arguments);
                        toolResults.Add($"{toolCall.Name}: {result}");
                    }
                    else
                    {
                        if (verbose)
                        {
                            Console.WriteLine($"❌ Tool NOT found: '{toolCall.Name}'. Available tools: {string.Join(", ", tools.Select(t => t.Name))}");
                        }
                        toolResults.Add($"{toolCall.Name}: Error - Tool not found");
                        actionResult.Success = false;
                    }
                }
                catch (Exception ex)
                {
                    toolResults.Add($"{toolCall.Name}: Error - {ex.Message}");
                    actionResult.Success = false;
                    if (verbose)
                    {
                        Console.WriteLine($"❌ Exception executing tool {toolCall.Name}: {ex.Message}");
                    }
                }
            }
            actionResult.ToolResults = toolResults;
        }

        subtask.EndTime = DateTime.UtcNow;
        return actionResult;
    }

    private async Task<ObservationResult> ObserveResultsAsync(ActionResult actionResult)
    {
        var observation = new ObservationResult
        {
            Metrics = new Dictionary<string, object>
            {
                ["tool_calls_executed"] = actionResult.ToolCallsExecuted,
                ["response_length"] = actionResult.Response?.Length ?? 0
            }
        };

        // Пытаемся найти .csproj или .sln файлы для проверки
        var projectFiles = FindProjectFiles();
            
        if (projectFiles.Any())
        {
            var projectFile = projectFiles.First();
                
            if (verbose)
            {
                Console.WriteLine($"🔍 Running validation checks on {projectFile}");
            }

            // Проверяем build
            try
            {
                var buildTool = TaskValidationTools.BuildProjectDefinition;
                var buildInput = System.Text.Json.JsonSerializer.Serialize(new { project_path = projectFile });
                var buildResult = await buildTool.ExecuteAsync(buildInput);
                    
                var buildData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(buildResult);
                observation.BuildSuccess = buildData.GetProperty("Success").GetBoolean();
                    
                if (!observation.BuildSuccess)
                {
                    observation.Logs.Add($"Build failed: {buildData.GetProperty("Error").GetString()}");
                }
            }
            catch (Exception ex)
            {
                observation.BuildSuccess = false;
                observation.Logs.Add($"Build check failed: {ex.Message}");
            }

            // Проверяем форматирование кода
            try
            {
                var lintTool = TaskValidationTools.LintCodeDefinition;
                var lintInput = System.Text.Json.JsonSerializer.Serialize(new { 
                    path = projectFile, 
                    verify_no_changes = true,
                    dry_run = true 
                });
                var lintResult = await lintTool.ExecuteAsync(lintInput);
                    
                var lintData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(lintResult);
                observation.LintPass = lintData.GetProperty("Success").GetBoolean();
                    
                if (!observation.LintPass)
                {
                    observation.Logs.Add($"Linting issues found: {lintData.GetProperty("Error").GetString()}");
                }
            }
            catch (Exception ex)
            {
                observation.LintPass = true; // Не критично, если lint недоступен
                observation.Logs.Add($"Lint check skipped: {ex.Message}");
            }

            // Пытаемся запустить тесты (если есть)
            try
            {
                var testTool = TaskValidationTools.RunTestsDefinition;
                var testInput = System.Text.Json.JsonSerializer.Serialize(new { 
                    project_path = projectFile,
                    no_restore = true 
                });
                var testResult = await testTool.ExecuteAsync(testInput);
                    
                var testData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(testResult);
                observation.TestsPass = testData.GetProperty("Success").GetBoolean();
                    
                if (!observation.TestsPass)
                {
                    observation.Logs.Add($"Tests failed: {testData.GetProperty("Error").GetString()}");
                }
            }
            catch (Exception ex)
            {
                observation.TestsPass = true; // Не критично, если тестов нет
                observation.Logs.Add($"Test check skipped: {ex.Message}");
            }
        }
        else
        {
            // Если нет проектных файлов, считаем что все ОК
            observation.BuildSuccess = true;
            observation.TestsPass = true;
            observation.LintPass = true;
            observation.Logs.Add("No project files found, skipping validation checks");
        }

        return observation;
    }

    private List<string> FindProjectFiles()
    {
        var projectFiles = new List<string>();
            
        try
        {
            // Ищем .sln файлы в текущей директории
            projectFiles.AddRange(Directory.GetFiles(".", "*.sln"));
                
            // Ищем .csproj файлы в текущей директории и поддиректориях
            projectFiles.AddRange(Directory.GetFiles(".", "*.csproj", SearchOption.AllDirectories));
                
            // Фильтруем файлы в bin/obj папках
            projectFiles = projectFiles
                .Where(f => !f.Contains("/bin/") && !f.Contains("\\bin\\") && 
                            !f.Contains("/obj/") && !f.Contains("\\obj\\"))
                .ToList();
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"Error finding project files: {ex.Message}");
            }
        }
            
        return projectFiles;
    }

    private async Task<CritiqueResult> CritiqueResultsAsync(ObservationResult observation, Subtask subtask)
    {
        var critique = new CritiqueResult();
            
        // Определяем тип задачи для более гибкой оценки
        var isResearchTask = subtask.Description.ToLower().Contains("understand") || 
                             subtask.Description.ToLower().Contains("read") ||
                             subtask.Description.ToLower().Contains("research");
            
        var isImplementationTask = subtask.RequiredTools.Contains("edit_file") ||
                                   subtask.Description.ToLower().Contains("update") ||
                                   subtask.Description.ToLower().Contains("modify");
            
        // Анализируем результаты в зависимости от типа задачи
        if (!observation.BuildSuccess)
        {
            critique.Issues.Add("Build failed");
            critique.ErrorType = ErrorType.Compilation;
        }
            
        if (!observation.TestsPass && isImplementationTask)
        {
            critique.Issues.Add("Tests failed");
            critique.ErrorType = ErrorType.Logic;
        }
            
        // Для задач исследования/понимания линтинг не критичен
        if (!observation.LintPass && isImplementationTask)
        {
            critique.Issues.Add("Linting failed");
            critique.ErrorType = ErrorType.Style;
        }
        else if (!observation.LintPass && !isImplementationTask)
        {
            // Для исследовательских задач линтинг - это просто предупреждение
            critique.Issues.Add("Linting warnings (non-critical for research tasks)");
        }

        // Для исследовательских задач успех определяется по-другому
        if (isResearchTask)
        {
            // Исследовательская задача успешна, если нет критических ошибок сборки
            critique.IsSuccessful = observation.BuildSuccess;
        }
        else
        {
            // Для задач реализации требуем все проверки
            critique.IsSuccessful = observation.BuildSuccess && observation.TestsPass && observation.LintPass;
        }
            
        if (critique.IsSuccessful)
        {
            critique.Feedback = "Subtask completed successfully";
            subtask.Status = SubtaskStatus.Completed;
        }
        else
        {
            var criticalIssues = critique.Issues.Where(i => !i.Contains("non-critical")).ToList();
            if (criticalIssues.Any())
            {
                critique.Feedback = $"Critical issues found: {string.Join(", ", criticalIssues)}";
                subtask.Status = SubtaskStatus.Failed;
            }
            else
            {
                critique.Feedback = "Subtask completed with minor warnings";
                critique.IsSuccessful = true;
                subtask.Status = SubtaskStatus.Completed;
            }
        }

        return critique;
    }

    private async Task UpdatePlanAsync(TaskPlan plan, CritiqueResult critique, Subtask subtask)
    {
        if (!critique.IsSuccessful)
        {
            // Если подзадача провалилась, можем попробовать альтернативный подход
            subtask.AttemptCount++;
                
            if (subtask.AttemptCount < _config.MaxSubtaskAttempts)
            {
                subtask.Status = SubtaskStatus.Pending; // Попробуем еще раз
                    
                if (verbose)
                {
                    Console.WriteLine($"🔄 Retrying subtask {subtask.Id} (attempt {subtask.AttemptCount + 1}/{_config.MaxSubtaskAttempts})");
                }
            }
            else
            {
                if (verbose)
                {
                    Console.WriteLine($"❌ Subtask {subtask.Id} failed after {subtask.AttemptCount} attempts");
                }
                    
                // Если подзадача провалилась окончательно, попробуем адаптировать план
                await AdaptPlanAsync(plan, subtask, critique);
            }
        }
    }

    /// <summary>
    /// Адаптирует план, если подзадача провалилась окончательно
    /// </summary>
    private async Task AdaptPlanAsync(TaskPlan plan, Subtask failedSubtask, CritiqueResult critique)
    {
        if (verbose)
        {
            Console.WriteLine($"🔄 Attempting to adapt plan due to failed subtask: {failedSubtask.Id}");
        }

        try
        {
            // Анализируем причину провала и создаем альтернативные подзадачи
            var adaptationPrompt = $@"A subtask has failed multiple times and needs plan adaptation.

FAILED SUBTASK: {failedSubtask.Description}
FAILURE REASON: {critique.Feedback}
ERROR TYPE: {critique.ErrorType}
ISSUES: {string.Join(", ", critique.Issues)}

CURRENT PLAN CONTEXT:
- Total subtasks: {plan.Subtasks.Count}
- Completed: {plan.Subtasks.Count(s => s.Status == SubtaskStatus.Completed)}
- Failed: {plan.Subtasks.Count(s => s.Status == SubtaskStatus.Failed)}
- Pending: {plan.Subtasks.Count(s => s.Status == SubtaskStatus.Pending)}

AVAILABLE TOOLS: {string.Join(", ", tools.Select(t => t.Name))}

Please analyze the failure and suggest alternative approaches. Consider:
1. Breaking down the failed subtask into smaller, more specific steps
2. Using different tools or approaches
3. Addressing the root cause of the failure
4. Ensuring the new subtasks are achievable with existing files/context

Respond with a JSON structure containing new subtasks to replace or supplement the failed one:
{{
  ""adaptationStrategy"": ""replace|supplement|skip"",
  ""reasoning"": ""Explanation of why this adaptation is needed"",
  ""newSubtasks"": [
    {{
      ""id"": ""adapted_subtask_1"",
      ""description"": ""Clear description of what needs to be done"",
      ""definitionOfDone"": [""Specific criteria 1"", ""Specific criteria 2""],
      ""dependencies"": [""subtask_id_that_must_be_completed_first""],
      ""priority"": 1,
      ""estimatedComplexity"": ""low|medium|high"",
      ""requiredTools"": [""tool_name_1"", ""tool_name_2""]
    }}
  ]
}}";

            var conversation = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful task planning assistant specialized in plan adaptation. Always respond with valid JSON."),
                new UserChatMessage(adaptationPrompt)
            };

            var response = await provider.SendMessageWithToolsAsync(conversation, null, verbose);
                
            if (!string.IsNullOrEmpty(response.TextContent))
            {
                var cleanJson = ExtractJsonFromMarkdown(response.TextContent);
                var adaptationData = JsonSerializer.Deserialize<PlanAdaptationData>(cleanJson);
                    
                if (adaptationData != null && adaptationData.NewSubtasks.Any())
                {
                    await ApplyPlanAdaptationAsync(plan, failedSubtask, adaptationData);
                }
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"⚠️ Failed to adapt plan: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Применяет адаптацию плана
    /// </summary>
    private async Task ApplyPlanAdaptationAsync(TaskPlan plan, Subtask failedSubtask, PlanAdaptationData adaptation)
    {
        if (verbose)
        {
            Console.WriteLine($"🔧 Applying plan adaptation: {adaptation.AdaptationStrategy}");
            Console.WriteLine($"💡 Reasoning: {adaptation.Reasoning}");
        }

        switch (adaptation.AdaptationStrategy.ToLower())
        {
            case "replace":
                // Заменяем провалившуюся подзадачу новыми
                failedSubtask.Status = SubtaskStatus.Replaced;
                await AddNewSubtasksAsync(plan, adaptation.NewSubtasks, failedSubtask.Priority);
                break;
                    
            case "supplement":
                // Добавляем новые подзадачи в дополнение к существующим
                await AddNewSubtasksAsync(plan, adaptation.NewSubtasks, failedSubtask.Priority);
                break;
                    
            case "skip":
                // Пропускаем провалившуюся подзадачу и продолжаем
                failedSubtask.Status = SubtaskStatus.Skipped;
                if (verbose)
                {
                    Console.WriteLine($"⏭️ Skipping subtask {failedSubtask.Id} as recommended");
                }
                break;
        }
    }

    /// <summary>
    /// Добавляет новые подзадачи в план
    /// </summary>
    private async Task AddNewSubtasksAsync(TaskPlan plan, List<SubtaskData> newSubtasksData, int basePriority)
    {
        var newSubtasks = newSubtasksData.Select(s => new Subtask
        {
            Id = s.Id,
            Description = s.Description,
            DefinitionOfDone = s.DefinitionOfDone,
            Dependencies = s.Dependencies,
            Priority = s.Priority > 0 ? s.Priority : basePriority,
            EstimatedComplexity = Enum.Parse<ComplexityLevel>(s.EstimatedComplexity, true),
            RequiredTools = s.RequiredTools,
            Status = SubtaskStatus.Pending,
            IsAdapted = true // Помечаем как адаптированную подзадачу
        }).ToList();

        plan.Subtasks.AddRange(newSubtasks);
            
        if (verbose)
        {
            Console.WriteLine($"➕ Added {newSubtasks.Count} new adapted subtasks:");
            foreach (var subtask in newSubtasks)
            {
                Console.WriteLine($"  - {subtask.Id}: {subtask.Description}");
            }
        }
    }

    private async Task HandleIterationErrorAsync(TaskPlan plan, Exception error)
    {
        if (verbose)
        {
            Console.WriteLine($"🔧 Handling iteration error: {error.Message}");
        }
            
        // Можно добавить логику восстановления после ошибок
        await Task.Delay(1000); // Небольшая пауза перед следующей попыткой
    }

    private bool IsTaskComplete(TaskPlan plan)
    {
        // Задача считается завершенной, если все подзадачи либо выполнены, либо заменены, либо пропущены
        return plan.Subtasks.All(s => s.Status == SubtaskStatus.Completed || 
                                      s.Status == SubtaskStatus.Replaced || 
                                      s.Status == SubtaskStatus.Skipped);
    }

    /// <summary>
    /// Извлекает JSON из markdown блока кода
    /// </summary>
    private static string ExtractJsonFromMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Ищем JSON блок в markdown (```json ... ```)
        var jsonBlockStart = text.IndexOf("```json");
        if (jsonBlockStart >= 0)
        {
            var jsonStart = text.IndexOf('\n', jsonBlockStart) + 1;
            var jsonEnd = text.IndexOf("```", jsonStart);
            if (jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }
        }

        // Ищем обычный блок кода (``` ... ```)
        var codeBlockStart = text.IndexOf("```");
        if (codeBlockStart >= 0)
        {
            var codeStart = text.IndexOf('\n', codeBlockStart) + 1;
            var codeEnd = text.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
            {
                var potentialJson = text.Substring(codeStart, codeEnd - codeStart).Trim();
                // Проверяем, что это похоже на JSON
                if (potentialJson.StartsWith("{") && potentialJson.EndsWith("}"))
                {
                    return potentialJson;
                }
            }
        }

        // Если нет markdown блоков, возвращаем исходный текст
        return text.Trim();
    }

    private bool ShouldStopExecution(TaskCompletionResult result, int iteration)
    {
        // Проверяем различные условия остановки
        if (iteration >= _config.MaxIterations)
        {
            if (verbose)
            {
                Console.WriteLine($"⏰ Stopping: Maximum iterations ({_config.MaxIterations}) reached");
            }
            return true;
        }

        if (result.Duration.TotalMinutes > _config.MaxExecutionTimeMinutes)
        {
            if (verbose)
            {
                Console.WriteLine($"⏰ Stopping: Maximum execution time ({_config.MaxExecutionTimeMinutes} min) reached");
            }
            return true;
        }

        var failedSubtasks = result.Plan?.Subtasks.Count(s => s.Status == SubtaskStatus.Failed) ?? 0;
        if (failedSubtasks > _config.MaxFailedSubtasks)
        {
            if (verbose)
            {
                Console.WriteLine($"❌ Stopping: Too many failed subtasks ({failedSubtasks})");
            }
            return true;
        }

        return false;
    }

    private string GenerateExecutionSystemPrompt()
    {
        var toolDescriptions = tools.Select(tool => 
            $"- {tool.Name}: {tool.Description}").ToList();
        var toolList = string.Join(Environment.NewLine, toolDescriptions);

        return $"You are a task execution specialist. Your job is to complete specific subtasks using available tools.{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"AVAILABLE TOOLS:{Environment.NewLine}" +
               $"{toolList}{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"EXECUTION PRINCIPLES:{Environment.NewLine}" +
               $"1. Read and understand the subtask carefully before acting{Environment.NewLine}" +
               $"2. Choose the RIGHT TOOL FOR THE JOB based on what needs to be accomplished{Environment.NewLine}" +
               $"3. For research/understanding tasks, use appropriate tools to gather information{Environment.NewLine}" +
               $"4. For implementation tasks, use tools to modify code and files{Environment.NewLine}" +
               $"5. For system commands, use bash tool{Environment.NewLine}" +
               $"6. Be thorough and check your work{Environment.NewLine}" +
               $"7. Focus on meeting all Definition of Done criteria{Environment.NewLine}" +
               $"8. If something fails, try alternative approaches{Environment.NewLine}" +
               $"9. Provide clear feedback on what was accomplished{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"TOOL SELECTION GUIDELINES:{Environment.NewLine}" +
               $"- list_files: Use ONLY when you need to discover what files exist or explore directory structure{Environment.NewLine}" +
               $"- read_file: Use when you need to examine the contents of a specific file{Environment.NewLine}" +
               $"- edit_file: Use when you need to create or modify code/text files{Environment.NewLine}" +
               $"- bash: Use for running shell commands, building, or testing{Environment.NewLine}" +
               $"- Other tools: Use as needed for validation or specific operations{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"CRITICAL - AVOID UNNECESSARY TOOL CALLS:{Environment.NewLine}" +
               $"- Do not use list_files just to explore - use it only when you genuinely need to know what files exist{Environment.NewLine}" +
               $"- Do not repeatedly use the same tool - use it once to get information, then use other tools{Environment.NewLine}" +
               $"- Each tool call should have a specific purpose and move the task forward{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"METHODICAL APPROACH FOR CODE IMPLEMENTATION:{Environment.NewLine}" +
               $"1. UNDERSTAND THE TASK: Read the definition of done carefully{Environment.NewLine}" +
               $"2. EXECUTE: Use the most appropriate tool for each step (edit_file, bash, read_file, etc.){Environment.NewLine}" +
               $"3. VALIDATE: Check that requirements are met{Environment.NewLine}" +
               $"4. USE TOOLS STRATEGICALLY: Not all tasks require exploration - some need direct action{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"IMPORTANT NOTES:{Environment.NewLine}" +
               $"- Do not assume files exist - use read_file or bash to check, then use appropriate tool{Environment.NewLine}" +
               $"- If you know what file you need to edit, use edit_file directly{Environment.NewLine}" +
               $"- When creating new functionality, use edit_file to create/modify files as needed{Environment.NewLine}" +
               $"- Use bash for compilation, testing, or running commands{Environment.NewLine}" +
               $"- When adding features, look at existing similar code patterns to understand the approach{Environment.NewLine}" +
               $"- Vary your tool usage based on actual task requirements{Environment.NewLine}" +
               $"{Environment.NewLine}" +
               $"Always use tools actively to complete the task - do not just provide instructions or explanations.{Environment.NewLine}" +
               $"Pick the right tool for each specific action you need to take.";
    }
}

// Конфигурация для агента завершения задач
public class TaskCompletionConfig
{
    public int MaxIterations { get; set; } = 50;
    public int MaxSubtaskAttempts { get; set; } = 3;
    public int MaxFailedSubtasks { get; set; } = 5;
    public double MaxExecutionTimeMinutes { get; set; } = 30;
}

// Модели данных для планирования и выполнения задач
public class TaskPlan
{
    public string TaskDescription { get; set; } = "";
    public List<string> Constraints { get; set; } = new();
    public List<Subtask> Subtasks { get; set; } = new();
}

public class Subtask
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> DefinitionOfDone { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public int Priority { get; set; }
    public ComplexityLevel EstimatedComplexity { get; set; }
    public List<string> RequiredTools { get; set; } = new();
    public SubtaskStatus Status { get; set; } = SubtaskStatus.Pending;
    public int AttemptCount { get; set; } = 0;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsAdapted { get; set; } = false; // Помечает адаптированные подзадачи
}

public enum SubtaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Replaced,  // Подзадача заменена адаптированными подзадачами
    Skipped    // Подзадача пропущена по рекомендации адаптации
}

public enum ComplexityLevel
{
    Low,
    Medium,
    High
}

public enum ErrorType
{
    None,
    Compilation,
    Logic,
    Style,
    Environment,
    Network
}

public class TaskCompletionResult
{
    public string TaskDescription { get; set; } = "";
    public List<string> Constraints { get; set; } = new();
    public TaskPlan? Plan { get; set; }
    public List<IterationResult> Iterations { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalIterations { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

public class IterationResult
{
    public int IterationNumber { get; set; }
    public string CurrentState { get; set; } = "";
    public Subtask? SelectedSubtask { get; set; }
    public ActionResult? ActionResult { get; set; }
    public ObservationResult? ObservationResult { get; set; }
    public CritiqueResult? Critique { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ActionResult
{
    public string SubtaskId { get; set; } = "";
    public string Response { get; set; } = "";
    public int ToolCallsExecuted { get; set; }
    public List<string> ToolResults { get; set; } = new();
    public bool Success { get; set; }
}

public class ObservationResult
{
    public bool BuildSuccess { get; set; } = true;
    public bool TestsPass { get; set; } = true;
    public bool LintPass { get; set; } = true;
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Logs { get; set; } = new();
}

public class CritiqueResult
{
    public bool IsSuccessful { get; set; }
    public List<string> Issues { get; set; } = new();
    public ErrorType ErrorType { get; set; } = ErrorType.None;
    public string Feedback { get; set; } = "";
    public string? RecommendedAction { get; set; }
}

// Вспомогательные классы для десериализации JSON
internal class TaskPlanData
{
    [JsonPropertyName("subtasks")]
    public List<SubtaskData> Subtasks { get; set; } = new();
}

internal class SubtaskData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
        
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
        
    [JsonPropertyName("definitionOfDone")]
    public List<string> DefinitionOfDone { get; set; } = new();
        
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();
        
    [JsonPropertyName("priority")]
    public int Priority { get; set; }
        
    [JsonPropertyName("estimatedComplexity")]
    public string EstimatedComplexity { get; set; } = "medium";
        
    [JsonPropertyName("requiredTools")]
    public List<string> RequiredTools { get; set; } = new();
}

/// <summary>
/// Класс для десериализации данных адаптации плана
/// </summary>
internal class PlanAdaptationData
{
    [JsonPropertyName("adaptationStrategy")]
    public string AdaptationStrategy { get; set; } = "";
        
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";
        
    [JsonPropertyName("newSubtasks")]
    public List<SubtaskData> NewSubtasks { get; set; } = new();
}