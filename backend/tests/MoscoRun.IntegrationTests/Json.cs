using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoscoRun.IntegrationTests;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
