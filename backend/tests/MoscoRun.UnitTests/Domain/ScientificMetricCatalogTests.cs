using System.Text.Json;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class ScientificMetricCatalogTests
{
    [Fact]
    public void Catalog_matches_research_json()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "scientific-catalog.json")));
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(ScientificMetricCatalog.SchemaVersion);
        var metrics = root.GetProperty("metrics").EnumerateArray().ToDictionary(
            metric => metric.GetProperty("metric").GetString()!,
            metric => metric.GetProperty("unit").GetString()!, StringComparer.Ordinal);
        metrics.Count.Should().Be(ScientificMetricCatalog.MetricCount);
        foreach (var metric in metrics)
        {
            ScientificMetricCatalog.IsKnown(metric.Key).Should().BeTrue();
            ScientificMetricCatalog.AcceptsUnit(metric.Key, metric.Value).Should().BeTrue();
        }
    }
}
