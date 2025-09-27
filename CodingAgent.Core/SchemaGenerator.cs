using System.Text.Json;
using NJsonSchema.Generation;

namespace CodingAgent.Core
{
    public static class SchemaGenerator
    {
        public static object GenerateSchema<T>()
        {
            var settings = new SystemTextJsonSchemaGeneratorSettings();
            var generator = new JsonSchemaGenerator(settings);
            var schema = generator.Generate(typeof(T));
            
            // Конвертируем в простой формат для OpenAI
            var schemaJson = schema.ToJson();
            var schemaObject = JsonSerializer.Deserialize<JsonElement>(schemaJson);
            
            // Создаем упрощенную схему в формате OpenAI
            var openAISchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = ExtractProperties(schemaObject),
                ["required"] = ExtractRequired(schemaObject)
            };
            
            return openAISchema;
        }
        
        private static Dictionary<string, object> ExtractProperties(JsonElement schema)
        {
            var properties = new Dictionary<string, object>();
            
            if (schema.TryGetProperty("properties", out var propsElement))
            {
                foreach (var prop in propsElement.EnumerateObject())
                {
                    var propSchema = new Dictionary<string, object>();
                    
                    if (prop.Value.TryGetProperty("type", out var typeElement))
                    {
                        propSchema["type"] = typeElement.GetString() ?? "string";
                    }
                    else
                    {
                        propSchema["type"] = "string";
                    }
                    
                    if (prop.Value.TryGetProperty("description", out var descElement))
                    {
                        propSchema["description"] = descElement.GetString() ?? "";
                    }
                    
                    properties[prop.Name] = propSchema;
                }
            }
            
            return properties;
        }
        
        private static string[] ExtractRequired(JsonElement schema)
        {
            var required = new List<string>();
            
            if (schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        required.Add(item.GetString() ?? "");
                    }
                }
            }
            
            return required.ToArray();
        }
    }
}