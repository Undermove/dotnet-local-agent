using System.Text.Json;
using OpenAI.Chat;

namespace CodingAgent.Core;

public static class ToolConverter
{
    public static List<ChatTool> ConvertToOpenAITools(List<ToolDefinition> tools)
    {
        var openAITools = new List<ChatTool>();
        
        foreach (var tool in tools)
        {
            try
            {
                // Создаём ChatTool с правильными параметрами
                var parameters = BinaryData.FromString(JsonSerializer.Serialize(tool.InputSchema));
                var chatTool = ChatTool.CreateFunctionTool(tool.Name, tool.Description, parameters);
                openAITools.Add(chatTool);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to convert tool {tool.Name}: {ex.Message}");
                // Создаём упрощённую версию без параметров
                var simpleTool = ChatTool.CreateFunctionTool(tool.Name, tool.Description);
                openAITools.Add(simpleTool);
            }
        }
        
        return openAITools;
    }
}