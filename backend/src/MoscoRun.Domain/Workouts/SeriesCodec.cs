using System.Buffers;

namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Formato binario versionato di una serie temporale: tempo in millisecondi e valore in millesimi
/// dell'unità della grandezza, entrambi come differenze varint zig-zag rispetto al campione precedente.
/// </summary>
public static class SeriesCodec
{
    public const byte Version = 1;
    private const double ValueScale = 1000;

    public static byte[] Encode(IReadOnlyList<SeriesSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var writer = new ArrayBufferWriter<byte>(16 + (samples.Count * 4));
        writer.GetSpan(1)[0] = Version;
        writer.Advance(1);
        Varint.WriteUnsigned(writer, (ulong)samples.Count);

        long time = 0, value = 0;
        foreach (var s in samples)
        {
            var t = s.Time.ToUnixTimeMilliseconds();
            var v = ToFixed(s.Value);
            Varint.WriteSigned(writer, t - time);
            Varint.WriteSigned(writer, v - value);
            (time, value) = (t, v);
        }

        return writer.WrittenSpan.ToArray();
    }

    public static IReadOnlyList<SeriesSample> Decode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data[0] != Version)
        {
            throw new InvalidDataException("Versione della serie non supportata.");
        }

        var offset = 1;
        var count = Varint.ReadUnsigned(data, ref offset);
        if (count > (ulong)(data.Length - offset) / 2)
        {
            throw new InvalidDataException("Numero di campioni incoerente con la dimensione dei dati.");
        }

        var samples = new SeriesSample[(int)count];
        long time = 0, value = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            time = Varint.AddDelta(time, Varint.ReadSigned(data, ref offset));
            value = Varint.AddDelta(value, Varint.ReadSigned(data, ref offset));
            DateTimeOffset at;
            try
            {
                at = DateTimeOffset.FromUnixTimeMilliseconds(time);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidDataException("Istante fuori intervallo.", ex);
            }

            samples[i] = new SeriesSample(at, value / ValueScale);
        }

        if (offset != data.Length)
        {
            throw new InvalidDataException("Byte in eccesso dopo l'ultimo campione.");
        }

        return samples;
    }

    public static SeriesSample Normalize(SeriesSample s) => new(
        DateTimeOffset.FromUnixTimeMilliseconds(s.Time.ToUnixTimeMilliseconds()),
        ToFixed(s.Value) / ValueScale);

    private static long ToFixed(double value) => (long)Math.Round(value * ValueScale, MidpointRounding.AwayFromZero);
}
