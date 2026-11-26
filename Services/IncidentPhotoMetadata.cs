using System.Buffers.Binary;
using System.Globalization;

namespace PoliceBackend.Services;

public static class IncidentPhotoMetadata
{
    public static string? GetExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 4 && bytes[..3].SequenceEqual(new byte[] { 255, 216, 255 }) &&
            bytes[^2] == 255 && bytes[^1] == 217) return ".jpg";
        if (bytes.Length >= 45 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.Slice(12, 4).SequenceEqual("IHDR"u8) && bytes.Slice(bytes.Length - 8, 4).SequenceEqual("IEND"u8)) return ".png";
        if (bytes.Length >= 20 && bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8) &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) == bytes.Length - 8) return ".webp";
        return null;
    }

    public static string? GetCaptureTime(ReadOnlySpan<byte> bytes, string extension)
    {
        if (extension == ".jpg")
        {
            for (var pos = 2; pos + 4 <= bytes.Length && bytes[pos] == 255;)
            {
                var marker = bytes[pos + 1];
                if (marker == 255) { pos++; continue; }
                if (marker is 218 or 217) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(pos + 2)..]);
                if (length < 2 || length > bytes.Length - pos - 2) break;
                var payload = bytes.Slice(pos + 4, length - 2);
                if (marker == 225 && payload.StartsWith("Exif\0\0"u8)) return ReadTiff(payload[6..]);
                pos += 2 + length;
            }
        }
        else
        {
            var png = extension == ".png";
            for (var pos = png ? 8 : 12; pos + (png ? 12 : 8) <= bytes.Length;)
            {
                var length = png ? BinaryPrimitives.ReadUInt32BigEndian(bytes[pos..])
                    : BinaryPrimitives.ReadUInt32LittleEndian(bytes[(pos + 4)..]);
                var overhead = png ? 12 : 8;
                if (length > bytes.Length - pos - overhead) break;
                var name = bytes.Slice(pos + (png ? 4 : 0), 4);
                if (name.SequenceEqual(png ? "eXIf"u8 : "EXIF"u8))
                {
                    var payload = bytes.Slice(pos + 8, (int)length);
                    return ReadTiff(payload.StartsWith("Exif\0\0"u8) ? payload[6..] : payload);
                }
                pos += overhead + (int)length + (png ? 0 : (int)(length % 2));
            }
        }

        return null;
    }

    private static string? ReadTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8) return null;
        var little = tiff[..2].SequenceEqual("II"u8);
        if (!little && !tiff[..2].SequenceEqual("MM"u8)) return null;
        if (Read16(tiff[2..], little) != 42) return null;
        var primary = Read32(tiff[4..], little);
        var exif = FindTag(tiff, primary, 0x8769, little);
        if (exif.IsEmpty || Read16(exif[2..], little) != 4 || Read32(exif[4..], little) != 1) return null;
        var date = FindTag(tiff, Read32(exif[8..], little), 0x9003, little);
        if (date.IsEmpty || Read16(date[2..], little) != 2 || Read32(date[4..], little) != 20) return null;
        var offset = Read32(date[8..], little);
        if (offset > tiff.Length - 20) return null;
        var raw = System.Text.Encoding.ASCII.GetString(tiff.Slice((int)offset, 19));
        return DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) &&
            time.Year >= 1839 && time <= DateTime.UtcNow.AddDays(1)
            ? time.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : null;
    }

    private static ReadOnlySpan<byte> FindTag(ReadOnlySpan<byte> tiff, uint offset, int tag, bool little)
    {
        if (offset > tiff.Length - 2) return [];
        var entries = tiff[(int)offset..];
        var count = Read16(entries, little);
        for (var i = 0; i < count && 2 + (i + 1) * 12 <= entries.Length; i++)
        {
            var entry = entries.Slice(2 + i * 12, 12);
            if (Read16(entry, little) == tag) return entry;
        }
        return [];
    }

    private static ushort Read16(ReadOnlySpan<byte> bytes, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    private static uint Read32(ReadOnlySpan<byte> bytes, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
}
