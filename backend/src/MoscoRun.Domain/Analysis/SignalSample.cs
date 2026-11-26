using System.Runtime.InteropServices;

namespace MoscoRun.Domain.Analysis;

[StructLayout(LayoutKind.Auto)]
public readonly record struct SignalSample(DateTimeOffset Time, double? Value);
