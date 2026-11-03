using System.Text;
using SplitMergeCue.Models;

namespace SplitMergeCue.Services;

public enum OperationMode
{
    Auto,
    Merge,
    Split
}

public sealed record ConversionOptions
{
    public required string InputCuePath { get; init; }
    public string? OutputDirectory { get; init; }
    public OperationMode Mode { get; init; } = OperationMode.Auto;
    public bool DeleteOriginals { get; init; }
    public Action<string>? Log { get; init; }
    public Action<double, string>? Progress { get; init; }
}

public sealed record ConversionResult(
    bool IsMerge,
    string OutputCuePath,
    IReadOnlyList<string> OutputBinPaths,
    long TotalBytesProcessed);

public static class CueBinProcessor
{
    private const int BufferSize = 1024 * 1024; // 1 MB buffer para copia rápida de flujos

    public static ConversionResult Process(ConversionOptions options)
    {
        CueSheet sheet = CueParser.ParseFile(options.InputCuePath);
        string sourceDir = Path.GetDirectoryName(sheet.SourceFilePath)
            ?? throw new InvalidOperationException("No se pudo determinar el directorio de origen.");

        bool shouldMerge = options.Mode switch
        {
            OperationMode.Merge => true,
            OperationMode.Split => false,
            OperationMode.Auto => DetermineAutoMode(sheet),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };

        if (shouldMerge)
        {
            return MergeBins(sheet, sourceDir, options);
        }
        else
        {
            return SplitBin(sheet, sourceDir, options);
        }
    }

    private static bool DetermineAutoMode(CueSheet sheet)
    {
        if (sheet.Files.Count > 1)
            return true; // Múltiples archivos BIN -> Unificar en uno solo

        if (sheet.Files.Count == 1 && sheet.Files[0].Tracks.Count > 1)
            return false; // Un solo archivo BIN con varias pistas -> Separar en pistas

        if (sheet.Files.Count == 1 && sheet.Files[0].Tracks.Count == 1)
        {
            throw new InvalidOperationException(
                "El archivo CUE solo tiene 1 archivo BIN y 1 sola pista. No requiere unificación ni división.");
        }

        throw new InvalidOperationException("El archivo CUE no contiene pistas válidas para procesar.");
    }

    private static ConversionResult MergeBins(CueSheet sheet, string sourceDir, ConversionOptions options)
    {
        if (sheet.Files.Count < 2)
        {
            throw new InvalidOperationException(
                "El modo unificar (Merge) requiere que el CUE haga referencia a al menos 2 archivos BIN.");
        }

        string baseName = Path.GetFileNameWithoutExtension(sheet.SourceFilePath);
        string outputDir = ResolveOutputDirectory(sourceDir, options.OutputDirectory, $"{baseName}_Merged");
        Directory.CreateDirectory(outputDir);

        // Validar que todos los archivos BIN de origen existen antes de empezar
        var sourceBins = new List<(CueFileEntry Entry, FileInfo Info)>(sheet.Files.Count);
        long totalBytesExpected = 0;

        foreach (CueFileEntry fileEntry in sheet.Files)
        {
            string binPath = ResolveBinPath(sourceDir, fileEntry.FileName);
            var info = new FileInfo(binPath);
            if (!info.Exists)
            {
                throw new FileNotFoundException(
                    $"No se encontró el archivo BIN referenciado: '{fileEntry.FileName}' en '{sourceDir}'.", binPath);
            }

            if (!string.Equals(fileEntry.FileType, "BINARY", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Tipo de archivo '{fileEntry.FileType}' en '{fileEntry.FileName}' no soportado para fusión binaria directa (solo BINARY).");
            }

            sourceBins.Add((fileEntry, info));
            totalBytesExpected += info.Length;
        }

        string mergedBinName = $"{baseName}.bin";
        string mergedBinPath = Path.Combine(outputDir, mergedBinName);
        string mergedCuePath = Path.Combine(outputDir, $"{baseName}.cue");

        // Evitar sobrescribir un archivo de entrada si el usuario eligió el mismo directorio
        foreach (var (_, info) in sourceBins)
        {
            if (string.Equals(info.FullName, Path.GetFullPath(mergedBinPath), StringComparison.OrdinalIgnoreCase))
            {
                mergedBinName = $"{baseName} (Merged).bin";
                mergedBinPath = Path.Combine(outputDir, mergedBinName);
                break;
            }
        }

        options.Log?.Invoke($"[MODO UNIFICAR] Se detectaron {sheet.Files.Count} archivos BIN ({sheet.TotalTrackCount} pistas).");
        options.Log?.Invoke($"Carpeta de salida: {outputDir}");

        var mergedFileEntry = new CueFileEntry
        {
            FileName = mergedBinName,
            FileType = "BINARY"
        };

        CueTime accumulatedTime = CueTime.Zero;
        long bytesWrittenTotal = 0;

        using (var outputStream = new FileStream(mergedBinPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
        {
            byte[] buffer = new byte[BufferSize];

            for (int i = 0; i < sourceBins.Count; i++)
            {
                var (fileEntry, fileInfo) = sourceBins[i];
                if (fileEntry.Tracks.Count == 0)
                    continue;

                // Verificar tamaño de sector de las pistas dentro de este archivo BIN
                int sectorSize = fileEntry.Tracks[0].GetSectorSize();
                if (fileInfo.Length % sectorSize != 0)
                {
                    throw new InvalidOperationException(
                        $"El archivo '{fileInfo.Name}' ({fileInfo.Length} bytes) no está alineado al tamaño de sector ({sectorSize} bytes).");
                }

                long fileFrames = fileInfo.Length / sectorSize;

                // Desplazar cada pista e índice por el tiempo acumulado de los archivos BIN anteriores
                foreach (CueTrack track in fileEntry.Tracks)
                {
                    var clonedTrack = new CueTrack
                    {
                        Number = track.Number,
                        TrackType = track.TrackType
                    };
                    clonedTrack.MetadataLines.AddRange(track.MetadataLines);

                    foreach (CueIndex idx in track.Indices)
                    {
                        clonedTrack.Indices.Add(new CueIndex
                        {
                            Number = idx.Number,
                            Time = idx.Time + accumulatedTime
                        });
                    }

                    mergedFileEntry.Tracks.Add(clonedTrack);
                }

                options.Log?.Invoke($"  -> Uniendo [{i + 1}/{sourceBins.Count}] {fileInfo.Name} ({FormatBytes(fileInfo.Length)})...");

                using (var inputStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                {
                    int read;
                    while ((read = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outputStream.Write(buffer, 0, read);
                        bytesWrittenTotal += read;
                        if (totalBytesExpected > 0)
                        {
                            options.Progress?.Invoke((double)bytesWrittenTotal / totalBytesExpected, fileInfo.Name);
                        }
                    }
                }

                accumulatedTime += new CueTime(checked((int)fileFrames));
            }
        }

        var mergedSheet = new CueSheet
        {
            SourceFilePath = mergedCuePath
        };
        mergedSheet.GlobalLines.AddRange(sheet.GlobalLines);
        mergedSheet.Files.Add(mergedFileEntry);

        string cueContent = CueParser.Serialize(mergedSheet);
        File.WriteAllText(mergedCuePath, cueContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        options.Log?.Invoke($"CUE actualizado guardado en: {mergedCuePath}");

        if (options.DeleteOriginals)
        {
            DeleteOriginalFiles(sheet.SourceFilePath, sourceBins.Select(b => b.Info.FullName), mergedCuePath, [mergedBinPath], options.Log);
        }

        return new ConversionResult(
            IsMerge: true,
            OutputCuePath: mergedCuePath,
            OutputBinPaths: [mergedBinPath],
            TotalBytesProcessed: bytesWrittenTotal);
    }

    private static ConversionResult SplitBin(CueSheet sheet, string sourceDir, ConversionOptions options)
    {
        if (sheet.Files.Count != 1)
        {
            throw new InvalidOperationException(
                $"El modo dividir (Split) requiere que el CUE haga referencia a 1 solo archivo BIN (actualmente referencia {sheet.Files.Count}).");
        }

        CueFileEntry singleFile = sheet.Files[0];
        if (singleFile.Tracks.Count < 2)
        {
            throw new InvalidOperationException(
                "El archivo CUE solo contiene 1 pista, por lo que no se puede dividir en múltiples pistas.");
        }

        string sourceBinPath = ResolveBinPath(sourceDir, singleFile.FileName);
        var sourceBinInfo = new FileInfo(sourceBinPath);
        if (!sourceBinInfo.Exists)
        {
            throw new FileNotFoundException(
                $"No se encontró el archivo BIN referenciado: '{singleFile.FileName}' en '{sourceDir}'.", sourceBinPath);
        }

        string baseName = Path.GetFileNameWithoutExtension(sheet.SourceFilePath);
        string outputDir = ResolveOutputDirectory(sourceDir, options.OutputDirectory, $"{baseName}_Split");
        Directory.CreateDirectory(outputDir);

        string splitCuePath = Path.Combine(outputDir, $"{baseName}.cue");

        options.Log?.Invoke($"[MODO DIVIDIR] Se detectó 1 archivo BIN con {singleFile.Tracks.Count} pistas ({FormatBytes(sourceBinInfo.Length)}).");
        options.Log?.Invoke($"Carpeta de salida: {outputDir}");

        // Calcular el desplazamiento en bytes del inicio de cada pista y validar estructura
        var tracks = singleFile.Tracks;
        var trackStartFrames = new int[tracks.Count];

        for (int i = 0; i < tracks.Count; i++)
        {
            CueTrack track = tracks[i];
            if (track.Indices.Count == 0)
            {
                throw new FormatException($"La pista {track.Number:D2} no contiene ningún INDEX.");
            }

            // El inicio del archivo BIN de cada pista corresponde a su primer índice (INDEX 00 si tiene pregap en el archivo, o INDEX 01)
            CueIndex firstIndex = track.Indices.OrderBy(idx => idx.Number).First();
            trackStartFrames[i] = firstIndex.Time.TotalFrames;

            if (i > 0 && trackStartFrames[i] < trackStartFrames[i - 1])
            {
                throw new InvalidOperationException(
                    $"Los tiempos de índice no son crecientes en la pista {track.Number:D2} ({firstIndex.Time}).");
            }
        }

        // Calcular los offsets en bytes teniendo en cuenta el tamaño de sector de cada tramo de pista
        var trackByteOffsets = new long[tracks.Count + 1];
        trackByteOffsets[0] = 0;

        for (int i = 0; i < tracks.Count; i++)
        {
            int sectorSize = tracks[i].GetSectorSize();
            if (i == 0 && trackStartFrames[0] > 0)
            {
                trackByteOffsets[0] = (long)trackStartFrames[0] * sectorSize;
            }

            if (i + 1 < tracks.Count)
            {
                int frameDelta = trackStartFrames[i + 1] - trackStartFrames[i];
                trackByteOffsets[i + 1] = trackByteOffsets[i] + ((long)frameDelta * sectorSize);
            }
            else
            {
                trackByteOffsets[i + 1] = sourceBinInfo.Length;
            }
        }

        if (trackByteOffsets[^1] > sourceBinInfo.Length)
        {
            throw new InvalidOperationException(
                $"Los índices del CUE superan el tamaño real del archivo BIN ({sourceBinInfo.Length} bytes).");
        }

        var splitSheet = new CueSheet
        {
            SourceFilePath = splitCuePath
        };
        splitSheet.GlobalLines.AddRange(sheet.GlobalLines);

        var outputBinPaths = new List<string>(tracks.Count);
        long bytesWrittenTotal = 0;
        long totalBytesToRead = sourceBinInfo.Length - trackByteOffsets[0];

        using (var inputStream = new FileStream(sourceBinInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
        {
            byte[] buffer = new byte[BufferSize];

            for (int i = 0; i < tracks.Count; i++)
            {
                CueTrack srcTrack = tracks[i];
                int startFrame = trackStartFrames[i];
                CueTime trackStartTime = new(startFrame);

                long byteStart = trackByteOffsets[i];
                long byteEnd = trackByteOffsets[i + 1];
                long trackByteLength = byteEnd - byteStart;

                int sectorSize = srcTrack.GetSectorSize();
                if (trackByteLength % sectorSize != 0)
                {
                    throw new InvalidOperationException(
                        $"La longitud calculada para la pista {srcTrack.Number:D2} ({trackByteLength} bytes) no es múltiplo del tamaño de sector ({sectorSize} bytes).");
                }

                string trackBinName = $"{baseName} (Track {srcTrack.Number:D2}).bin";
                string trackBinPath = Path.Combine(outputDir, trackBinName);
                outputBinPaths.Add(trackBinPath);

                options.Log?.Invoke($"  -> Extrayendo Pista {srcTrack.Number:D2} [{srcTrack.TrackType}] -> {trackBinName} ({FormatBytes(trackByteLength)})...");

                inputStream.Position = byteStart;
                long remaining = trackByteLength;

                using (var outputStream = new FileStream(trackBinPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        int read = inputStream.Read(buffer, 0, toRead);
                        if (read == 0)
                        {
                            throw new EndOfStreamException(
                                $"Fin de archivo inesperado al leer la pista {srcTrack.Number:D2} desde '{sourceBinInfo.Name}'.");
                        }

                        outputStream.Write(buffer, 0, read);
                        remaining -= read;
                        bytesWrittenTotal += read;

                        if (totalBytesToRead > 0)
                        {
                            options.Progress?.Invoke((double)bytesWrittenTotal / totalBytesToRead, trackBinName);
                        }
                    }
                }

                // Construir la entrada FILE y TRACK con sus tiempos relativos a 00:00:00
                var newFileEntry = new CueFileEntry
                {
                    FileName = trackBinName,
                    FileType = singleFile.FileType
                };

                var newTrack = new CueTrack
                {
                    Number = srcTrack.Number,
                    TrackType = srcTrack.TrackType
                };
                newTrack.MetadataLines.AddRange(srcTrack.MetadataLines);

                foreach (CueIndex idx in srcTrack.Indices)
                {
                    newTrack.Indices.Add(new CueIndex
                    {
                        Number = idx.Number,
                        Time = idx.Time - trackStartTime
                    });
                }

                newFileEntry.Tracks.Add(newTrack);
                splitSheet.Files.Add(newFileEntry);
            }
        }

        string cueContent = CueParser.Serialize(splitSheet);
        File.WriteAllText(splitCuePath, cueContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        options.Log?.Invoke($"CUE actualizado guardado en: {splitCuePath}");

        if (options.DeleteOriginals)
        {
            DeleteOriginalFiles(sheet.SourceFilePath, [sourceBinInfo.FullName], splitCuePath, outputBinPaths, options.Log);
        }

        return new ConversionResult(
            IsMerge: false,
            OutputCuePath: splitCuePath,
            OutputBinPaths: outputBinPaths,
            TotalBytesProcessed: bytesWrittenTotal);
    }

    private static string ResolveOutputDirectory(string sourceDir, string? customOutputDir, string defaultSubfolderName)
    {
        if (!string.IsNullOrWhiteSpace(customOutputDir))
            return Path.GetFullPath(customOutputDir);

        return Path.Combine(sourceDir, defaultSubfolderName);
    }

    private static string ResolveBinPath(string sourceDir, string fileNameInCue)
    {
        if (Path.IsPathRooted(fileNameInCue) && File.Exists(fileNameInCue))
            return fileNameInCue;

        string candidate = Path.Combine(sourceDir, fileNameInCue);
        if (File.Exists(candidate))
            return candidate;

        // Intentar buscar solo por el nombre de archivo si el CUE tenía una ruta relativa o absoluta antigua
        string onlyName = Path.GetFileName(fileNameInCue);
        return Path.Combine(sourceDir, onlyName);
    }

    private static void DeleteOriginalFiles(
        string originalCuePath,
        IEnumerable<string> originalBinPaths,
        string outputCuePath,
        IReadOnlyCollection<string> outputBinPaths,
        Action<string>? log)
    {
        var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(outputCuePath)
        };
        foreach (string outBin in outputBinPaths)
        {
            protectedPaths.Add(Path.GetFullPath(outBin));
        }

        foreach (string bin in originalBinPaths)
        {
            string fullBin = Path.GetFullPath(bin);
            if (!protectedPaths.Contains(fullBin) && File.Exists(fullBin))
            {
                File.Delete(fullBin);
                log?.Invoke($"  [Eliminado original] {fullBin}");
            }
        }

        string fullCue = Path.GetFullPath(originalCuePath);
        if (!protectedPaths.Contains(fullCue) && File.Exists(fullCue))
        {
            File.Delete(fullCue);
            log?.Invoke($"  [Eliminado original] {fullCue}");
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.##} {units[unitIndex]}";
    }
}
