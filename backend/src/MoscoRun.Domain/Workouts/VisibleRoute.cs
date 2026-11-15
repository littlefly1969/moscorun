namespace MoscoRun.Domain.Workouts;

/// <summary>Tracciato da mostrare, dopo aver nascosto partenza e arrivo dentro le zone private.</summary>
/// <param name="Points">Punti visibili, nell'ordine originale.</param>
/// <param name="HiddenStart">Punti iniziali nascosti.</param>
/// <param name="HiddenEnd">Punti finali nascosti.</param>
public sealed record VisibleRoute(IReadOnlyList<TrackPoint> Points, int HiddenStart, int HiddenEnd);
