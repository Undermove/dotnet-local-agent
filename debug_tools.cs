using System;
using System.Text.Json;
using CodingAgent.Core;

// Простой тест для проверки генерации схем
var readFileTool = ReadFileDefinition.Instance;
Console.WriteLine("=== READ FILE TOOL ===");
Console.WriteLine($"Name: {readFileTool.Name}");
Console.WriteLine($"Description: {readFileTool.Description}");
Console.WriteLine($"Schema: {JsonSerializer.Serialize(readFileTool.InputSchema, new JsonSerializerOptions { WriteIndented = true })}");

// Проверим конвертацию в OpenAI формат
var tools = new List<ToolDefinition> { readFileTool };
var openAITools = ToolConverter.ConvertToOpenAITools(tools);

Console.WriteLine("\n=== CONVERTED TO OPENAI FORMAT ===");
foreach (var tool in openAITools)
{
    Console.WriteLine($"Tool Kind: {tool.Kind}");
    Console.WriteLine($"Function Name: {tool.FunctionName}");
    Console.WriteLine($"Function Description: {tool.FunctionDescription}");
    Console.WriteLine($"Function Parameters: {tool.FunctionParameters}");
}