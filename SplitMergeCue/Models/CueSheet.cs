using System.Globalization;

namespace SplitMergeCue.Models;

/// <summary>
/// Representa un tiempo o desplazamiento en formato MSF (Minutos:Segundos:Frames).
/// 1 segundo = 75 frames (sectores).
/// </summary>
public readonly record struct CueTime(int TotalFrames) : IComparable<CueTime>
{
    public const int FramesPerSecond = 75;
    public const int SecondsPerMinute = 60;
    public const int FramesPerMinute = FramesPerSecond * SecondsPerMinute; // 4500

    public int Minutes => TotalFrames / FramesPerMinute;
    public int Seconds => (TotalFrames % FramesPerMinute) / FramesPerSecond;
    public int Frames => TotalFrames % FramesPerSecond;

    public static CueTime Zero => new(0);

    public static CueTime FromMsf(int minutes, int seconds, int frames)
    {
        if (minutes < 0 || seconds is < 0 or >= 60 || frames is < 0 or >= 75)
        {
            throw new ArgumentOutOfRangeException(
                $"Tiempo MSF inválido: {minutes:D2}:{seconds:D2}:{frames:D2}");
        }

        return new CueTime((minutes * FramesPerMinute) + (seconds * FramesPerSecond) + frames);
    }

    public static bool TryParse(string text, out CueTime result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Trim().Split(':');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int m) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int s) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int f))
        {
            return false;
        }

        if (s >= 60 || f >= 75)
            return false;

        result = FromMsf(m, s, f);
        return true;
    }

    public static CueTime Parse(string text)
    {
        if (!TryParse(text, out var time))
            throw new FormatException($"Formato de tiempo CUE inválido: '{text}'. Se esperaba MM:SS:FF.");
        return time;
    }

    public long ToByteOffset(int sectorSize) => (long)TotalFrames * sectorSize;

    public static CueTime FromByteOffset(long byteOffset, int sectorSize)
    {
        if (sectorSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(sectorSize));

        if (byteOffset % sectorSize != 0)
        {
            throw new InvalidOperationException(
                $"El tamaño en bytes ({byteOffset}) no es múltiplo exacto del tamaño de sector ({sectorSize} bytes).");
        }

        long frames = byteOffset / sectorSize;
        return new CueTime(checked((int)frames));
    }

    public static CueTime operator +(CueTime a, CueTime b) => new(a.TotalFrames + b.TotalFrames);
    public static CueTime operator -(CueTime a, CueTime b) => new(a.TotalFrames - b.TotalFrames);

    public int CompareTo(CueTime other) => TotalFrames.CompareTo(other.TotalFrames);

    public override string ToString() => $"{Minutes:D2}:{Seconds:D2}:{Frames:D2}";
}

public sealed class CueIndex
{
    public required int Number { get; init; }
    public required CueTime Time { get; set; }
}

public sealed class CueTrack
{
    public required int Number { get; init; }
    public required string TrackType { get; init; }
    public List<string> MetadataLines { get; } = [];
    public List<CueIndex> Indices { get; } = [];

    /// <summary>
    /// Obtiene el tamaño en bytes de cada sector según el modo de pista del CUE.
    /// </summary>
    public int GetSectorSize()
    {
        string upper = TrackType.ToUpperInvariant();
        if (upper == "AUDIO")
            return 2352;

        // Formatos tipo MODE1/2352, MODE2/2352, MODE1/2048, MODE2/2336, CDI/2352, etc.
        int slashIdx = upper.IndexOf('/');
        if (slashIdx >= 0 && int.TryParse(upper[(slashIdx + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size > 0)
        {
            return size;
        }

        return 2352;
    }

    public CueIndex? GetIndex(int number) => Indices.FirstOrDefault(i => i.Number == number);
}

public sealed class CueFileEntry
{
    public required string FileName { get; set; }
    public required string FileType { get; init; } // Normalmente "BINARY"
    public List<CueTrack> Tracks { get; } = [];
}

public sealed class CueSheet
{
    public required string SourceFilePath { get; init; }
    public List<string> GlobalLines { get; } = [];
    public List<CueFileEntry> Files { get; } = [];

    public int TotalTrackCount => Files.Sum(f => f.Tracks.Count);
    public bool IsMultiBin => Files.Count > 1;
    public bool IsSingleBin => Files.Count == 1 && Files[0].Tracks.Count > 1;
}
