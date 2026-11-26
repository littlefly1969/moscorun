using System.Text.Json.Nodes;

namespace MoscoRun.Infrastructure.Persistence;

internal static class ScientificModelJson
{
    private const int MaxLength = 262_144;

    public static void ValidateObject(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > MaxLength || JsonNode.Parse(text) is not JsonObject)
        {
            throw new ArgumentException("JSON oggetto non valido o troppo grande.", nameof(text));
        }
    }

    public static bool Equal(string? left, string? right) =>
        JsonNode.DeepEquals(left is null ? null : JsonNode.Parse(left), right is null ? null : JsonNode.Parse(right));

    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var ticks = value.UtcTicks;
        return new DateTimeOffset(ticks - (ticks % 10), TimeSpan.Zero);
    }
}
