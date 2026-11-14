using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Domain.Common;

namespace MoscoRun.Api.Features;

internal static class ProblemMapping
{
    /// <summary>Converte gli errori di dominio in un ValidationProblem (RFC 9457) raggruppato per campo.</summary>
    public static ValidationProblem ToValidationProblem(this IReadOnlyList<DomainError> errors) =>
        TypedResults.ValidationProblem(
            errors
                .GroupBy(e => e.Field ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["codes"] = errors.Select(e => e.Code).ToArray(),
            });
}
