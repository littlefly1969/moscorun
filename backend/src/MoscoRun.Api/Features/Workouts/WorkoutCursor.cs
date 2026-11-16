using System.Buffers.Binary;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MoscoRun.Api.Features.Workouts;

/// <summary>
/// Posizione nella lista degli allenamenti ordinata per (inizio, id) decrescenti. Per il client è una stringa
/// opaca; non contiene nulla che il client non abbia già ricevuto e non allarga i permessi, perché la query
/// resta sempre filtrata sul proprietario autenticato.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WorkoutCursor(DateTimeOffset StartedAt, Guid Id)
{
    private const int Size = sizeof(long) + 16;

    public static bool TryParse([NotNullWhen(true)] string? text, out WorkoutCursor cursor)
    {
        cursor = default;
        Span<byte> bytes = stackalloc byte[Size];
        if (text is null || text.Length != Base64Url.GetEncodedLength(Size)
            || !Base64Url.TryDecodeFromChars(text, bytes, out var written) || written != Size)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        cursor = new WorkoutCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[sizeof(long)..], bigEndian: true));
        return true;
    }

    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[Size];
        BinaryPrimitives.WriteInt64BigEndian(bytes, StartedAt.UtcTicks);
        Id.TryWriteBytes(bytes[sizeof(long)..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }
}
