using System.Diagnostics;
using System.Text;
using SplitMergeCue.Models;
using SplitMergeCue.Services;

namespace SplitMergeCue;

internal static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            return RunInteractiveMode();
        }

        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintUsage();
            return 0;
        }

        return RunCommandLineMode(args);
    }

    private static int RunInteractiveMode()
    {
        PrintBanner();
        Console.WriteLine("Modo interactivo (también puedes arrastrar archivos .cue sobre el ejecutable).");
        Console.WriteLine();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Introduce o arrastra la ruta del archivo .cue (o escribe 'salir'): ");
            Console.ResetColor();

            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("salir", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (!File.Exists(input))
            {
                WriteError($"No existe el archivo: {input}");
                Console.WriteLine();
                continue;
            }

            try
            {
                CueSheet preview = CueParser.ParseFile(input);
                Console.WriteLine();
                Console.WriteLine($"Archivo CUE : {Path.GetFileName(preview.SourceFilePath)}");
                Console.WriteLine($"Archivos BIN: {preview.Files.Count}");
                Console.WriteLine($"Total Pistas: {preview.TotalTrackCount}");

                string detectedAction = preview.IsMultiBin
                    ? "UNIFICAR varios archivos BIN en un solo BIN"
                    : preview.IsSingleBin
                        ? "DIVIDIR un archivo BIN en múltiples archivos BIN por pista"
                        : "Sin cambios necesarios (1 BIN, 1 pista)";

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Acción auto : {detectedAction}");
                Console.ResetColor();

                Console.Write("¿Eliminar los archivos originales al terminar? (s/N): ");
                string? delResp = Console.ReadLine()?.Trim();
                bool deleteOriginals = delResp is not null &&
                    (delResp.Equals("s", StringComparison.OrdinalIgnoreCase) ||
                     delResp.Equals("si", StringComparison.OrdinalIgnoreCase) ||
                     delResp.Equals("sí", StringComparison.OrdinalIgnoreCase) ||
                     delResp.Equals("y", StringComparison.OrdinalIgnoreCase));

                ExecuteConversion(input, outputDir: null, OperationMode.Auto, deleteOriginals);
            }
            catch (Exception ex)
            {
                WriteError($"Error: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static int RunCommandLineMode(string[] args)
    {
        PrintBanner();

        var cueFiles = new List<string>();
        string? outputDir = null;
        OperationMode mode = OperationMode.Auto;
        bool deleteOriginals = false;
        bool pauseOnFinish = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "-o" or "--output":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("Falta la ruta del directorio después de --output / -o.");
                        return 1;
                    }
                    outputDir = args[++i];
                    break;

                case "-m" or "--merge":
                    mode = OperationMode.Merge;
                    break;

                case "-s" or "--split":
                    mode = OperationMode.Split;
                    break;

                case "-d" or "--delete-originals":
                    deleteOriginals = true;
                    break;

                case "-p" or "--pause":
                    pauseOnFinish = true;
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        WriteError($"Opción desconocida: {arg}");
                        PrintUsage();
                        return 1;
                    }
                    cueFiles.Add(arg.Trim('"'));
                    break;
            }
        }

        if (cueFiles.Count == 0)
        {
            WriteError("No se especificó ningún archivo .cue de entrada.");
            PrintUsage();
            return 1;
        }

        // Si el usuario arrastró un archivo desde el Explorador de Windows (sin flags), pausar al final para que vea el resultado
        if (args.Length == cueFiles.Count && !Console.IsInputRedirected)
        {
            pauseOnFinish = true;
        }

        int errorCount = 0;
        foreach (string cuePath in cueFiles)
        {
            // Permitir también pasar un directorio para procesar todos los .cue dentro
            if (Directory.Exists(cuePath))
            {
                string[] foundCues = Directory.GetFiles(cuePath, "*.cue", SearchOption.TopDirectoryOnly);
                if (foundCues.Length == 0)
                {
                    WriteError($"No se encontraron archivos .cue en el directorio: {cuePath}");
                    errorCount++;
                    continue;
                }

                foreach (string found in foundCues)
                {
                    if (!ExecuteConversion(found, outputDir, mode, deleteOriginals))
                        errorCount++;
                }
            }
            else
            {
                if (!ExecuteConversion(cuePath, outputDir, mode, deleteOriginals))
                    errorCount++;
            }
        }

        if (pauseOnFinish)
        {
            Console.WriteLine();
            Console.WriteLine("Presiona cualquier tecla para cerrar...");
            Console.ReadKey(intercept: true);
        }

        return errorCount == 0 ? 0 : 1;
    }

    private static bool ExecuteConversion(
        string cuePath,
        string? outputDir,
        OperationMode mode,
        bool deleteOriginals)
    {
        Console.WriteLine(new string('─', 68));
        Console.WriteLine($"Procesando: {Path.GetFullPath(cuePath)}");

        var sw = Stopwatch.StartNew();
        int lastPercent = -1;

        try
        {
            var result = CueBinProcessor.Process(new ConversionOptions
            {
                InputCuePath = cuePath,
                OutputDirectory = outputDir,
                Mode = mode,
                DeleteOriginals = deleteOriginals,
                Log = msg => Console.WriteLine(msg),
                Progress = (ratio, currentFile) =>
                {
                    int pct = (int)Math.Clamp(ratio * 100, 0, 100);
                    if (pct != lastPercent && (pct % 5 == 0 || pct == 100))
                    {
                        lastPercent = pct;
                        if (!Console.IsOutputRedirected)
                        {
                            Console.Write($"\r  Progreso: [{pct,3}%] ({currentFile})   ");
                            if (pct == 100)
                                Console.WriteLine();
                        }
                    }
                }
            });

            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            string actionDone = result.IsMerge ? "Unificación completada" : "División completada";
            Console.WriteLine($"✔ {actionDone} con éxito en {sw.Elapsed.TotalSeconds:0.00} s ({CueBinProcessor.FormatBytes(result.TotalBytesProcessed)}).");
            Console.WriteLine($"  Archivo CUE generado : {result.OutputCuePath}");
            Console.WriteLine($"  Archivos BIN creados : {result.OutputBinPaths.Count}");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            WriteError($"✘ Error al procesar '{Path.GetFileName(cuePath)}': {ex.Message}");
            return false;
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("====================================================================");
        Console.WriteLine("  Split-Merge CUE/BIN Tool (.NET 10 / Visual Studio 2026)");
        Console.WriteLine("  Unifica múltiples BIN en 1 o divide 1 BIN en pistas automáticamente");
        Console.WriteLine("====================================================================");
        Console.ResetColor();
    }

    private static void PrintUsage()
    {
        PrintBanner();
        Console.WriteLine();
        Console.WriteLine("USO:");
        Console.WriteLine("  SplitMergeCue <archivo1.cue> [archivo2.cue ...] [opciones]");
        Console.WriteLine("  (También puedes arrastrar y soltar uno o varios archivos .cue sobre el ejecutable)");
        Console.WriteLine();
        Console.WriteLine("COMPORTAMIENTO AUTOMÁTICO (por defecto):");
        Console.WriteLine("  - Si el .cue referencia varios archivos .bin -> Los unifica en un único .bin y recalcula tiempos.");
        Console.WriteLine("  - Si el .cue referencia un solo archivo .bin -> Separa cada pista en su propio archivo .bin.");
        Console.WriteLine();
        Console.WriteLine("OPCIONES:");
        Console.WriteLine("  -o, --output <carpeta>     Carpeta de salida personalizada.");
        Console.WriteLine("                             (Por defecto crea '<Nombre>_Merged' o '<Nombre>_Split').");
        Console.WriteLine("  -m, --merge                Forzar modo unificación (Multi-BIN -> Single-BIN).");
        Console.WriteLine("  -s, --split                Forzar modo división (Single-BIN -> Multi-BIN).");
        Console.WriteLine("  -d, --delete-originals     Eliminar los archivos CUE y BIN originales tras completar sin errores.");
        Console.WriteLine("  -p, --pause                Pausar antes de cerrar la consola al finalizar.");
        Console.WriteLine("  -h, --help                 Mostrar esta ayuda.");
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }
}
