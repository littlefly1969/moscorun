using System.Buffers;

namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Formato binario compatto e versionato del tracciato: tempo in millisecondi, coordinate in 1e-7 gradi
/// (circa 1 cm, la stessa risoluzione di FIT e OpenStreetMap), altitudine e precisione in centimetri.
/// Le differenze tra punti consecutivi sono codificate come varint zig-zag: un'ora di corsa a 1 Hz
/// occupa circa 40 KB. Codifica e decodifica sono deterministiche: <c>Decode(Encode(x))</c> restituisce
/// i valori normalizzati di <c>x</c>, e codificare due volte gli stessi punti produce gli stessi byte.
/// </summary>
public static class TrackCodec
{
    public const byte Version = 1;
    private const byte HasAltitude = 1;
    private const byte HasAccuracy = 2;
    private const double CoordinateScale = 1e7;
    private const double CentimetreScale = 100;

    public static byte[] Encode(IReadOnlyList<TrackPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var writer = new ArrayBufferWriter<byte>(16 + (points.Count * 10));
        writer.GetSpan(1)[0] = Version;
        writer.Advance(1);
        Varint.WriteUnsigned(writer, (ulong)points.Count);

        long time = 0, lat = 0, lon = 0;
        foreach (var p in points)
        {
            byte flags = 0;
            if (p.AltitudeM is not null)
            {
                flags |= HasAltitude;
            }

            if (p.AccuracyM is not null)
            {
                flags |= HasAccuracy;
            }

            writer.GetSpan(1)[0] = flags;
            writer.Advance(1);

            var t = p.Time.ToUnixTimeMilliseconds();
            var la = ToFixed(p.Latitude, CoordinateScale);
            var lo = ToFixed(p.Longitude, CoordinateScale);
            Varint.WriteSigned(writer, t - time);
            Varint.WriteSigned(writer, la - lat);
            Varint.WriteSigned(writer, lo - lon);
            (time, lat, lon) = (t, la, lo);

            if (p.AltitudeM is { } alt)
            {
                Varint.WriteSigned(writer, ToFixed(alt, CentimetreScale));
            }

            if (p.AccuracyM is { } acc)
            {
                Varint.WriteUnsigned(writer, (ulong)ToFixed(acc, CentimetreScale));
            }
        }

        return writer.WrittenSpan.ToArray();
    }

    public static IReadOnlyList<TrackPoint> Decode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data[0] != Version)
        {
            throw new InvalidDataException("Versione del tracciato non supportata.");
        }

        var offset = 1;
        var count = Varint.ReadUnsigned(data, ref offset);

        // Ogni punto occupa almeno 4 byte: un conteggio maggiore indica dati corrotti, non un'allocazione da tentare.
        if (count > (ulong)(data.Length - offset) / 4)
        {
            throw new InvalidDataException("Numero di punti incoerente con la dimensione dei dati.");
        }

        var points = new TrackPoint[(int)count];
        long time = 0, lat = 0, lon = 0;
        for (var i = 0; i < points.Length; i++)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("Dati troncati.");
            }

            var flags = data[offset++];
            if ((flags & ~(HasAltitude | HasAccuracy)) != 0)
            {
                throw new InvalidDataException("Indicatori del punto sconosciuti.");
            }

            time = Varint.AddDelta(time, Varint.ReadSigned(data, ref offset));
            lat = Varint.AddDelta(lat, Varint.ReadSigned(data, ref offset));
            lon = Varint.AddDelta(lon, Varint.ReadSigned(data, ref offset));
            double? alt = (flags & HasAltitude) != 0 ? Varint.ReadSigned(data, ref offset) / CentimetreScale : null;
            double? acc = (flags & HasAccuracy) != 0 ? Varint.ReadUnsigned(data, ref offset) / CentimetreScale : null;

            DateTimeOffset at;
            try
            {
                at = DateTimeOffset.FromUnixTimeMilliseconds(time);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidDataException("Istante fuori intervallo.", ex);
            }

            points[i] = new TrackPoint(at, lat / CoordinateScale, lon / CoordinateScale, alt, acc);
        }

        if (offset != data.Length)
        {
            throw new InvalidDataException("Byte in eccesso dopo l'ultimo punto.");
        }

        return points;
    }

    /// <summary>Il punto come verrà salvato: stessa risoluzione del formato binario.</summary>
    public static TrackPoint Normalize(TrackPoint p) => new(
        DateTimeOffset.FromUnixTimeMilliseconds(p.Time.ToUnixTimeMilliseconds()),
        ToFixed(p.Latitude, CoordinateScale) / CoordinateScale,
        ToFixed(p.Longitude, CoordinateScale) / CoordinateScale,
        p.AltitudeM is { } a ? ToFixed(a, CentimetreScale) / CentimetreScale : null,
        p.AccuracyM is { } c ? ToFixed(c, CentimetreScale) / CentimetreScale : null);

    private static long ToFixed(double value, double scale) =>
        (long)Math.Round(value * scale, MidpointRounding.AwayFromZero);
}
