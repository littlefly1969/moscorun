using System.Buffers;

namespace MoscoRun.Domain.Workouts;

/// <summary>Interi a lunghezza variabile (LEB128) con codifica zig-zag per i valori con segno.</summary>
internal static class Varint
{
    public static void WriteUnsigned(IBufferWriter<byte> writer, ulong value)
    {
        var span = writer.GetSpan(10);
        var i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[i++] = (byte)value;
        writer.Advance(i);
    }

    public static void WriteSigned(IBufferWriter<byte> writer, long value) =>
        WriteUnsigned(writer, (ulong)((value << 1) ^ (value >> 63)));

    public static ulong ReadUnsigned(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("Dati troncati: varint incompleto.");
            }

            var b = data[offset++];
            if (shift == 63 && b > 1)
            {
                throw new InvalidDataException("Varint oltre 64 bit.");
            }

            result |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80)
            {
                return result;
            }
        }

        throw new InvalidDataException("Varint oltre 64 bit.");
    }

    public static long ReadSigned(ReadOnlySpan<byte> data, ref int offset)
    {
        var raw = ReadUnsigned(data, ref offset);
        return (long)(raw >> 1) ^ -(long)(raw & 1);
    }

    /// <summary>Somma con controllo di overflow: dati corrotti non devono produrre valori silenziosamente sbagliati.</summary>
    public static long AddDelta(long previous, long delta)
    {
        try
        {
            return checked(previous + delta);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Delta fuori intervallo.", ex);
        }
    }
}
