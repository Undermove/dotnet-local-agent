using Newtonsoft.Json;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;

namespace CodingAgent.Core
{
    public static class SchemaGenerator
    {
        public static object GenerateSchema<T>()
        {
            var settings = new SystemTextJsonSchemaGeneratorSettings();
            var generator = new JsonSchemaGenerator(settings);
            var schema = generator.Generate(typeof(T));
            return JsonConvert.DeserializeObject(schema.ToJson()) ?? new object();
        }
    }
}