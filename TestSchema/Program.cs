using System.Text.Json;
using CodingAgent.Core;

Console.WriteLine("=== TESTING SCHEMA GENERATION ===");

// Тестируем ReadFileInput
var readFileSchema = SchemaGenerator.GenerateSchema<ReadFileInput>();
Console.WriteLine("ReadFile Schema:");
Console.WriteLine(JsonSerializer.Serialize(readFileSchema, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("\n=== TESTING TOOL CONVERSION ===");

// Создаем инструмент и конвертируем
var readFileTool = ReadFileDefinition.Instance;
var tools = new List<ToolDefinition> { readFileTool };
var openAITools = ToolConverter.ConvertToOpenAITools(tools);

Console.WriteLine($"Original tool schema type: {readFileSchema.GetType()}");
Console.WriteLine($"Converted tools count: {openAITools.Count}");

if (openAITools.Count > 0)
{
    var tool = openAITools[0];
    Console.WriteLine($"Function Name: {tool.FunctionName}");
    Console.WriteLine($"Function Description: {tool.FunctionDescription}");
    Console.WriteLine($"Function Parameters: {tool.FunctionParameters}");
    Console.WriteLine($"Parameters as string: {tool.FunctionParameters.ToString()}");
}