using System.Text.Json;
using OpenAI.Chat;

namespace CodingAgent.Core;

public static class ToolConverter
{
    public static List<ChatTool> ConvertToOpenAITools(List<ToolDefinition> tools, bool verbose = false)
    {
        var openAITools = new List<ChatTool>();
        
        if (verbose)
        {
            // Console.WriteLine($"🔄 ToolConverter - Converting {tools.Count} tools to OpenAI format");
        }
        
        foreach (var tool in tools)
        {
            try
            {
                // Создаём ChatTool с правильными параметрами
                var parameters = BinaryData.FromString(JsonSerializer.Serialize(tool.InputSchema));
                var chatTool = ChatTool.CreateFunctionTool(tool.Name, tool.Description, parameters);
                openAITools.Add(chatTool);
                
                if (verbose)
                {
                    //Console.WriteLine($"✅ ToolConverter - Converted tool: '{tool.Name}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning: Failed to convert tool '{tool.Name}': {ex.Message}");
                // Создаём упрощённую версию без параметров
                var simpleTool = ChatTool.CreateFunctionTool(tool.Name, tool.Description);
                openAITools.Add(simpleTool);
                
                if (verbose)
                {
                    Console.WriteLine($"   Fallback: Created simplified tool '{tool.Name}'");
                }
            }
        }
        
        if (verbose)
        {
            // Console.WriteLine($"🔄 ToolConverter - Total tools ready: {openAITools.Count}");
        }
        
        return openAITools;
    }
}