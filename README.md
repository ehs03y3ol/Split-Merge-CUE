# Split-Merge CUE/BIN Tool

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![IDE](https://img.shields.io/badge/Visual%20Studio-2026-5C2D91?logo=visualstudio)](https://visualstudio.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](#)

*[English](#english) | [Español](#español)*

---

<a name="english"></a>
## 🌐 English

**Split-Merge CUE** is a fast, lossless command-line and drag-and-drop utility built with **C# (.NET 10)** that automatically merges multi-track `.bin` disc images into a single `.bin` file or splits a single `.bin` image into individual track `.bin` files while accurately recalculating all `.cue` sheet `INDEX` timestamps.

### ✨ Key Features

- **Smart Auto-Detection**:
  - **Multi-BIN -> Single-BIN (Merge)**: When the `.cue` sheet references multiple `.bin` files, it concatenates them into a single `.bin` and recalculates all `INDEX` offsets (`MM:SS:FF`, 75 frames/sec) based on exact sector sizes.
  - **Single-BIN -> Multi-BIN (Split)**: When the `.cue` sheet references a single `.bin` file with multiple tracks, it extracts each track into its own `.bin` file and resets each track's relative `INDEX` timestamps to `00:00:00`.
- **100% Lossless & Sector-Accurate**:
  - Supports `AUDIO` (2352 bytes/sector), `MODE1/2352`, `MODE2/2352`, `MODE1/2048`, `MODE2/2336`, `CDI/2352`, and mixed-mode CDs (e.g., PlayStation, Sega Saturn, Sega CD, TurboGrafx-CD, PC Engine).
  - Properly preserves `INDEX 00` (pregap) and `INDEX 01+` offsets as well as track metadata (`REM`, `TITLE`, `PERFORMER`, `FLAGS`, `PREGAP`, `POSTGAP`, `ISRC`, `CATALOG`).
- **Safe by Default**:
  - Outputs converted files into a dedicated subfolder (`<Name>_Merged` or `<Name>_Split`) without modifying or deleting your original files unless `--delete-originals` is explicitly specified.
- **Flexible Workflow**:
  - **Drag & Drop**: Drag one or more `.cue` files (or an entire folder) directly onto `SplitMergeCue.exe`.
  - **Interactive Mode**: Double-click `SplitMergeCue.exe` to inspect `.cue` files and convert them interactively.
  - **CLI / Batch Processing**: Full command-line argument support for scripts and batch automation.

---

### 🚀 Usage

#### 1. Drag and Drop
Simply drag one or multiple `.cue` files (or a directory containing `.cue` files) onto `SplitMergeCue.exe`. The tool will automatically detect whether to merge or split each image and create the output subfolder next to the original file.

#### 2. Command Line (CLI)
```bash
SplitMergeCue <file1.cue> [file2.cue ...] [options]
```

**Options:**

| Option | Description |
| :--- | :--- |
| `-o`, `--output <dir>` | Custom output directory (defaults to `<Name>_Merged` or `<Name>_Split`). |
| `-m`, `--merge` | Force **Merge** mode (Multi-BIN to Single-BIN). |
| `-s`, `--split` | Force **Split** mode (Single-BIN to Multi-BIN). |
| `-d`, `--delete-originals` | Delete original `.cue` and `.bin` files after a successful conversion. |
| `-p`, `--pause` | Wait for a key press before closing the console window. |
| `-h`, `--help` | Display the help message. |

**Examples:**
```powershell
# Auto-detect (Merge or Split) and output to default subfolder
SplitMergeCue.exe "Castlevania - Symphony of the Night.cue"

# Process all .cue files in a directory
SplitMergeCue.exe "D:\Roms\PS1"

# Specify a custom output folder and remove original files after completion
SplitMergeCue.exe "Game.cue" -o "D:\Converted\Game" --delete-originals
```

---

### 🛠️ Building from Source

#### Prerequisites
- **Visual Studio 2026** (or newer) with the **.NET Desktop Development** workload, OR
- **[.NET 10.0 SDK](https://dotnet.microsoft.com/download)**

#### Build Commands
```bash
# Clone the repository
git clone https://github.com/your-username/Split-Merge-CUE.git
cd Split-Merge-CUE

# Build Release binaries
dotnet build SplitMergeCue.slnx -c Release
```
The compiled executable will be generated in `SplitMergeCue/bin/Release/net10.0/SplitMergeCue.exe`.

---

### 📂 Example Conversion

````carousel
```cue
REM GENRE "Action"
FILE "Game (Track 01).bin" BINARY
  TRACK 01 MODE2/2352
    INDEX 01 00:00:00
FILE "Game (Track 02).bin" BINARY
  TRACK 02 AUDIO
    INDEX 00 00:00:00
    INDEX 01 00:01:00
FILE "Game (Track 03).bin" BINARY
  TRACK 03 AUDIO
    INDEX 00 00:00:00
    INDEX 01 00:02:00
```
<!-- slide -->
```cue
REM GENRE "Action"
FILE "Game.bin" BINARY
  TRACK 01 MODE2/2352
    INDEX 01 00:00:00
  TRACK 02 AUDIO
    INDEX 00 00:02:00
    INDEX 01 00:03:00
  TRACK 03 AUDIO
    INDEX 00 00:05:00
    INDEX 01 00:07:00
```
````

---

<a name="español"></a>
## 🌐 Español

**Split-Merge CUE** es una herramienta rápida y sin pérdida de datos desarrollada en **C# (.NET 10)** para línea de comandos y arrastrar y soltar (*Drag & Drop*). Detecta automáticamente si un archivo `.cue` hace referencia a múltiples archivos `.bin` para unificarlos en uno solo, o si apunta a un único archivo `.bin` con varias pistas para dividirlo en múltiples archivos `.bin`, recalculando todos los tiempos `INDEX` con precisión de sector.

### ✨ Características Principales

- **Detección Automática Inteligente**:
  - **Multi-BIN -> Single-BIN (Unificar / Merge)**: Si el `.cue` referencia varios archivos `.bin`, los une en un único archivo `.bin` y recalcula los desplazamientos `INDEX` (`MM:SS:FF`, 75 sectores por segundo) según el tamaño exacto de cada archivo.
  - **Single-BIN -> Multi-BIN (Dividir / Split)**: Si el `.cue` referencia un solo archivo `.bin` con múltiples pistas, extrae cada pista en su propio archivo `.bin` y reajusta los tiempos `INDEX` de cada pista para comenzar desde `00:00:00`.
- **100% Sin Pérdidas y Preciso al Sector**:
  - Compatible con pistas `AUDIO` (2352 bytes/sector), `MODE1/2352`, `MODE2/2352`, `MODE1/2048`, `MODE2/2336`, `CDI/2352` y discos de modo mixto (PlayStation, Sega Saturn, Sega CD, PC Engine, etc.).
  - Conserva intactos los pregaps (`INDEX 00`), índices adicionales (`INDEX 01+`) y metadatos del disco (`REM`, `TITLE`, `PERFORMER`, `FLAGS`, `PREGAP`, `POSTGAP`, `ISRC`, `CATALOG`).
- **Seguro por Defecto**:
  - Crea los archivos convertidos en una subcarpeta dedicada (`<Nombre>_Merged` o `<Nombre>_Split`) sin sobrescribir ni borrar los archivos originales, a menos que se indique explícitamente `--delete-originals`.
- **Múltiples Modos de Uso**:
  - **Arrastrar y Soltar**: Arrastra uno o varios archivos `.cue` (o una carpeta completa) directamente sobre `SplitMergeCue.exe`.
  - **Modo Interactivo**: Haz doble clic en `SplitMergeCue.exe` para analizar y convertir archivos paso a paso.
  - **Línea de Comandos (CLI)**: Soporte completo de parámetros para automatización y procesamiento por lotes.

---

### 🚀 Uso

#### 1. Arrastrar y Soltar (Drag & Drop)
Arrastra uno o varios archivos `.cue` (o una carpeta con archivos `.cue`) directamente sobre `SplitMergeCue.exe`. La aplicación detectará automáticamente si debe unificar o dividir cada imagen y guardará el resultado en una subcarpeta junto al archivo original.

#### 2. Línea de Comandos (CLI)
```bash
SplitMergeCue <archivo1.cue> [archivo2.cue ...] [opciones]
```

**Opciones:**

| Opción | Descripción |
| :--- | :--- |
| `-o`, `--output <carpeta>` | Carpeta de salida personalizada (por defecto crea `<Nombre>_Merged` o `<Nombre>_Split`). |
| `-m`, `--merge` | Forzar el modo **Unificar** (Multi-BIN a Single-BIN). |
| `-s`, `--split` | Forzar el modo **Dividir** (Single-BIN a Multi-BIN). |
| `-d`, `--delete-originals` | Eliminar los archivos `.cue` y `.bin` originales tras completar la conversión sin errores. |
| `-p`, `--pause` | Pausar antes de cerrar la consola al finalizar. |
| `-h`, `--help` | Mostrar la ayuda en pantalla. |

**Ejemplos:**
```powershell
# Detección automática (Unificar o Dividir) en subcarpeta por defecto
SplitMergeCue.exe "Castlevania - Symphony of the Night.cue"

# Procesar todos los archivos .cue dentro de una carpeta
SplitMergeCue.exe "D:\Roms\PS1"

# Especificar carpeta de salida y eliminar los archivos originales al terminar
SplitMergeCue.exe "Juego.cue" -o "D:\Convertidos\Juego" --delete-originals
```

---

### 🛠️ Compilación desde el Código Fuente

#### Requisitos
- **Visual Studio 2026** (o superior) con la carga de trabajo de desarrollo de escritorio .NET, O
- **[SDK de .NET 10.0](https://dotnet.microsoft.com/download)**

#### Comandos de Compilación
```bash
# Clonar el repositorio
git clone https://github.com/tu-usuario/Split-Merge-CUE.git
cd Split-Merge-CUE

# Compilar en modo Release
dotnet build SplitMergeCue.slnx -c Release
```
El ejecutable compilado se generará en `SplitMergeCue/bin/Release/net10.0/SplitMergeCue.exe`.

---

## 📄 License / Licencia

This project is released under the [MIT License](https://opensource.org/licenses/MIT).
