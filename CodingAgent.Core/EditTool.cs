using System.Text.Json;
using Newtonsoft.Json;
using static CodingAgent.Core.SchemaGenerator;

namespace CodingAgent.Core;

public class EditToolProgram
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

            // Используем универсальный агент с инструментами для всех провайдеров
            var tools = new List<ToolDefinition> 
            { 
                ReadFileDefinition.Instance,
                ListFilesDefinition.Instance,
                BashDefinition.Instance,
                EditFileDefinition.Instance
            };
                
            if (cmdArgs.Verbose)
            {
                Console.WriteLine($"Initialized {tools.Count} tools for {cmdArgs.Provider} provider");
                if (cmdArgs.Provider == AIProviderType.LMStudio)
                {
                    Console.WriteLine("Note: Tool support depends on the model. Llama 3.1 8B Instruct should work well.");
                }
            }

            var agent = new UniversalAgentWithTools(provider, GetUserMessage, tools, cmdArgs.Verbose);
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

public static class EditFileDefinition
{
    public static ToolDefinition Instance = new ToolDefinition
    {
        Name = "edit_file",
        Description = @"Create new files or edit existing text files.

CREATING NEW FILES:
- Set 'old_str' to empty string """"
- Set 'new_str' to the complete file content you want to create
- Example: {""path"": ""test.txt"", ""old_str"": """", ""new_str"": ""Hello World!""}

EDITING EXISTING FILES:
- Set 'old_str' to the exact text you want to replace (must exist in file)
- Set 'new_str' to the replacement text
- 'old_str' and 'new_str' MUST be different
- 'old_str' must appear exactly once in the file
- Example: {""path"": ""config.txt"", ""old_str"": ""debug=false"", ""new_str"": ""debug=true""}

IMPORTANT: Always provide meaningful content in 'new_str' - never leave it empty unless you want to delete text.",
        InputSchema = GenerateSchema<EditFileInput>(),
        ExecuteAsync = EditFileAsync
    };

    private static async Task<string> EditFileAsync(string input)
    {
        Console.WriteLine($"Raw input JSON: {input}");
            
        // Попробуем System.Text.Json для лучшей поддержки Unicode
        EditFileInput editFileInput;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            editFileInput = System.Text.Json.JsonSerializer.Deserialize<EditFileInput>(input, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"System.Text.Json failed: {ex.Message}, trying Newtonsoft.Json");
            // Fallback to Newtonsoft.Json
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.Default
            };
            editFileInput = JsonConvert.DeserializeObject<EditFileInput>(input, settings);
        }
            
        Console.WriteLine($"Editing file: {editFileInput.Path}");
        Console.WriteLine($"OldStr: '{editFileInput.OldStr}' (length: {editFileInput.OldStr?.Length ?? 0})");
        Console.WriteLine($"NewStr: '{editFileInput.NewStr}' (length: {editFileInput.NewStr?.Length ?? 0})");
            
        try
        {
            string content;
            bool fileExists = File.Exists(editFileInput.Path);
                
            // Special case: creating a new file
            if (!fileExists && string.IsNullOrEmpty(editFileInput.OldStr))
            {
                if (string.IsNullOrEmpty(editFileInput.NewStr))
                {
                    throw new ArgumentException("When creating a new file, 'new_str' cannot be empty. Please provide the content you want to write to the file.");
                }
                Console.WriteLine($"Creating new file: {editFileInput.Path}");
                await File.WriteAllTextAsync(editFileInput.Path, editFileInput.NewStr);
                return $"Successfully created file '{editFileInput.Path}' with {editFileInput.NewStr.Length} characters of content.";
            }
                
            // Check if trying to edit non-existent file
            if (!fileExists && !string.IsNullOrEmpty(editFileInput.OldStr))
            {
                throw new ArgumentException($"File '{editFileInput.Path}' does not exist. To create a new file, set 'old_str' to empty string and provide content in 'new_str'.");
            }

            // Validate inputs for editing existing files
            if (editFileInput.OldStr == editFileInput.NewStr)
            {
                throw new ArgumentException("old_str and new_str must be different");
            }

            if (fileExists)
            {
                content = await File.ReadAllTextAsync(editFileInput.Path);
                    
                // Check if old_str exists in the file
                if (!content.Contains(editFileInput.OldStr))
                {
                    throw new ArgumentException($"old_str '{editFileInput.OldStr}' not found in file");
                }
                    
                // Count occurrences to ensure it's unique
                int count = 0;
                int index = 0;
                while ((index = content.IndexOf(editFileInput.OldStr, index)) != -1)
                {
                    count++;
                    index += editFileInput.OldStr.Length;
                }
                    
                if (count > 1)
                {
                    throw new ArgumentException($"old_str '{editFileInput.OldStr}' appears {count} times in file. It must appear exactly once.");
                }
                    
                // Replace the string
                content = content.Replace(editFileInput.OldStr, editFileInput.NewStr);
            }
            else
            {
                // Create new file with new_str content
                content = editFileInput.NewStr;
                    
                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(editFileInput.Path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
                
            await File.WriteAllTextAsync(editFileInput.Path, content);
                
            var action = fileExists ? "modified" : "created";
            var result = $"File {editFileInput.Path} {action} successfully";
            Console.WriteLine(result);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to edit file {editFileInput.Path}: {ex.Message}");
            throw;
        }
    }
}

public class EditFileInput
{
    [JsonProperty("path")]
    [System.Text.Json.Serialization.JsonPropertyName("path")]
    public string Path { get; set; }
        
    [JsonProperty("old_str")]
    [System.Text.Json.Serialization.JsonPropertyName("old_str")]
    public string OldStr { get; set; }
        
    [JsonProperty("new_str")]
    [System.Text.Json.Serialization.JsonPropertyName("new_str")]
    public string NewStr { get; set; }
}