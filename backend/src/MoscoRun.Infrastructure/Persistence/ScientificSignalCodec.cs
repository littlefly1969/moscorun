using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Codec lossless, versione 1: tick UTC, flag di presenza, bit IEEE754; payload Brotli.</summary>
internal static class ScientificSignalCodec
{
    public const int Version = 1;
    public const int MaxSamples = 16_384;
    private const int RecordSize = 17;

    public static (byte[] Data, byte[] Hash) Encode(IReadOnlyList<ScientificSignalSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count is < 1 or > MaxSamples)
        {
            throw new ArgumentException("Dimensione chunk non valida.", nameof(samples));
        }

        var raw = new byte[samples.Count * RecordSize];
        long previous = -1;
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var ticks = sample.Time.UtcTicks;
            if (ticks <= previous || (sample.Value is { } number && !double.IsFinite(number)))
            {
                throw new ArgumentException("Campioni non ordinati o valori non finiti.", nameof(samples));
            }

            var record = raw.AsSpan(i * RecordSize, RecordSize);
            BinaryPrimitives.WriteInt64LittleEndian(record, ticks);
            record[8] = sample.Value is null ? (byte)0 : (byte)1;
            BinaryPrimitives.WriteInt64LittleEndian(record[9..], BitConverter.DoubleToInt64Bits(sample.Value ?? 0));
            previous = ticks;
        }

        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressor.Write(raw);
        }

        return (output.ToArray(), SHA256.HashData(raw));
    }

    public static IReadOnlyList<ScientificSignalSample> Decode(byte[] data, byte[] hash, int count, int version)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(hash);
        if (version != Version || count is < 1 or > MaxSamples || hash.Length != 32)
        {
            throw new InvalidDataException("Metadati chunk non validi.");
        }

        var raw = new byte[count * RecordSize];
        using var input = new MemoryStream(data, writable: false);
        using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
        decompressor.ReadExactly(raw);
        if (decompressor.ReadByte() != -1 || !SHA256.HashData(raw).AsSpan().SequenceEqual(hash))
        {
            throw new InvalidDataException("Integrità del chunk non valida.");
        }

        return DecodeRecords(raw, count);
    }

    private static IReadOnlyList<ScientificSignalSample> DecodeRecords(byte[] raw, int count)
    {
        var samples = new ScientificSignalSample[count];
        long previous = -1;
        for (var i = 0; i < count; i++)
        {
            var record = raw.AsSpan(i * RecordSize, RecordSize);
            var ticks = BinaryPrimitives.ReadInt64LittleEndian(record);
            var flag = record[8];
            var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(record[9..]));
            if (ticks <= previous || ticks > DateTimeOffset.MaxValue.UtcTicks || flag > 1 ||
                (flag == 1 && !double.IsFinite(value)))
            {
                throw new InvalidDataException("Campione non valido.");
            }

            samples[i] = new ScientificSignalSample(new DateTimeOffset(ticks, TimeSpan.Zero), flag == 0 ? null : value);
            previous = ticks;
        }

        return Array.AsReadOnly(samples);
    }
}
