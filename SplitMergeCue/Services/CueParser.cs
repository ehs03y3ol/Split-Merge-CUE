using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SplitMergeCue.Models;

namespace SplitMergeCue.Services;

public static partial class CueParser
{
    // FILE "filename.bin" BINARY o FILE filename.bin BINARY
    [GeneratedRegex(@"^FILE\s+(?:""(?<name>[^""]+)""|(?<name>\S+))\s+(?<type>\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FileLineRegex();

    // TRACK 01 MODE2/2352
    [GeneratedRegex(@"^TRACK\s+(?<num>\d+)\s+(?<mode>\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TrackLineRegex();

    // INDEX 01 00:00:00
    [GeneratedRegex(@"^INDEX\s+(?<num>\d+)\s+(?<time>\d{2,}:\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexLineRegex();

    public static CueSheet ParseFile(string cueFilePath)
    {
        if (!File.Exists(cueFilePath))
            throw new FileNotFoundException($"No se encontró el archivo CUE: {cueFilePath}", cueFilePath);

        string fullPath = Path.GetFullPath(cueFilePath);
        string[] lines = ReadAllLinesAutoEncoding(fullPath);
        return ParseLines(lines, fullPath);
    }

    public static CueSheet ParseLines(IEnumerable<string> lines, string sourceFilePath = "")
    {
        var sheet = new CueSheet { SourceFilePath = sourceFilePath };
        CueFileEntry? currentFile = null;
        CueTrack? currentTrack = null;

        int lineNumber = 0;
        foreach (string rawLine in lines)
        {
            lineNumber++;
            string trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var fileMatch = FileLineRegex().Match(trimmed);
            if (fileMatch.Success)
            {
                currentFile = new CueFileEntry
                {
                    FileName = fileMatch.Groups["name"].Value,
                    FileType = fileMatch.Groups["type"].Value.ToUpperInvariant()
                };
                sheet.Files.Add(currentFile);
                currentTrack = null;
                continue;
            }

            var trackMatch = TrackLineRegex().Match(trimmed);
            if (trackMatch.Success)
            {
                if (currentFile is null)
                {
                    throw new FormatException(
                        $"Línea {lineNumber}: Se encontró una directiva TRACK antes de cualquier directiva FILE.");
                }

                int trackNum = int.Parse(trackMatch.Groups["num"].Value, CultureInfo.InvariantCulture);
                string trackMode = trackMatch.Groups["mode"].Value.ToUpperInvariant();

                currentTrack = new CueTrack
                {
                    Number = trackNum,
                    TrackType = trackMode
                };
                currentFile.Tracks.Add(currentTrack);
                continue;
            }

            var indexMatch = IndexLineRegex().Match(trimmed);
            if (indexMatch.Success)
            {
                if (currentTrack is null)
                {
                    throw new FormatException(
                        $"Línea {lineNumber}: Se encontró una directiva INDEX fuera de un bloque TRACK.");
                }

                int indexNum = int.Parse(indexMatch.Groups["num"].Value, CultureInfo.InvariantCulture);
                CueTime indexTime = CueTime.Parse(indexMatch.Groups["time"].Value);

                currentTrack.Indices.Add(new CueIndex
                {
                    Number = indexNum,
                    Time = indexTime
                });
                continue;
            }

            // Metadatos adicionales (REM, TITLE, PERFORMER, FLAGS, PREGAP, POSTGAP, ISRC, CATALOG, etc.)
            if (currentTrack is not null)
            {
                currentTrack.MetadataLines.Add(trimmed);
            }
            else
            {
                sheet.GlobalLines.Add(trimmed);
            }
        }

        if (sheet.Files.Count == 0)
        {
            throw new FormatException("El archivo CUE no contiene ninguna entrada FILE válida.");
        }

        return sheet;
    }

    public static string Serialize(CueSheet sheet)
    {
        var sb = new StringBuilder();

        foreach (string globalLine in sheet.GlobalLines)
        {
            sb.AppendLine(globalLine);
        }

        foreach (CueFileEntry file in sheet.Files)
        {
            sb.AppendLine($"FILE \"{file.FileName}\" {file.FileType}");

            foreach (CueTrack track in file.Tracks)
            {
                sb.AppendLine($"  TRACK {track.Number:D2} {track.TrackType}");

                foreach (string meta in track.MetadataLines)
                {
                    sb.AppendLine($"    {meta}");
                }

                foreach (CueIndex index in track.Indices.OrderBy(i => i.Number))
                {
                    sb.AppendLine($"    INDEX {index.Number:D2} {index.Time}");
                }
            }
        }

        return sb.ToString();
    }

    private static string[] ReadAllLinesAutoEncoding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        // Detectar BOM UTF-8 / UTF-16
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return utf8Strict.GetString(bytes).Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        }
        catch (DecoderFallbackException)
        {
            // Fallback a Latin1 (ISO-8859-1) para archivos CUE antiguos con acentos en ANSI
            return Encoding.Latin1.GetString(bytes).Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        }
    }
}
