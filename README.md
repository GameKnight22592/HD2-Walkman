# Walkman for Helldivers 2

Full source for Walkman v0.3.7: the in-game Lua music player, Windows audio companion, installer and asset build tools. This repository is prepared for Nexus Mods' source-code and build review.

## Source map

| Path | Purpose |
| --- | --- |
| `Source/walkman.lua` | Complete in-game GUI, input handling, companion launch and shared-memory bridge |
| `Source/Companion.cs` | Entry point, library window, file scanning, settings and playback controller |
| `Source/NativeAudio.cs` | Windows Media Foundation/Media Engine audio playback interop |
| `Source/SongSpectrum.cs` | Local audio decoding and frequency analysis for the visualizer |
| `Source/ShuffleQueue.cs` | Shuffle order and playback history |
| `Companion/app.manifest` | Application manifest with `asInvoker` execution level |
| `Companion/build.ps1` | Companion compilation with Microsoft's .NET Framework compiler |
| `Source/build_assets.py` | Texture atlas generation and Lua/texture/material mod packaging |
| `Source/ArchiveTemplates.json` | Binary resource-format templates for the asset builder |
| `Source/*.png`, `Source/*.ttf` | Bundled art and font build inputs |
| `Source/FontLicense.txt` | DejaVu font license |
| `Install.cmd`, `Support/Install.ps1` | Companion installer |
| `Support/BuiltIn/` | Included Super Earth National Anthem audio asset |
| `Build.ps1` | Build and package both components as separate downloads |
| `Source/*Tests.cs`, `Source/validate.py`, `Test.ps1` | Optional offline tests |
| `docs/REVIEW.md`, `docs/Release_Hashes.json` | Reviewer notes and reference hashes |

All application C# and Lua source is included as ordinary text files. No prebuilt companion executable is needed to build it. AudioDrop is a separate application and is not a Walkman dependency.

## Build the companion only

Use 64-bit Windows with .NET Framework 4.8 and Windows PowerShell 5.1. The compiler must be present at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Visual Studio, Python and third-party .NET packages are not required for this build.

Clone the repository or download and extract its source ZIP. Open PowerShell in the directory containing this README:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Companion\build.ps1
```

Output: `Companion\bin\WalkmanAudio.exe`.

The script compiles `NativeAudio.cs`, `SongSpectrum.cs`, `ShuffleQueue.cs` and `Companion.cs` into an optimized x64 Windows executable, embeds `Companion\app.manifest`, and references only `System.dll`, `System.Core.dll`, `System.Drawing.dll` and `System.Windows.Forms.dll`.

Windows Media Foundation is required for playback. Windows N editions need their Media Feature Pack. To inspect the library without installing, run `Companion\bin\WalkmanAudio.exe`; this opens the library and creates its per-user data directory. The `--doctor` argument checks Media Engine initialization. `--game PID` is normally supplied by the mod and requires a running Helldivers 2 process with the matching shared-memory mapping.

## Build both release downloads

In addition to the requirements above, install Python 3.10 or later with the Windows `py` launcher and pip. Run from the repository root:

```powershell
py -3 -m pip install -r .\requirements-build.txt
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Outputs:

- `dist\Walkman_Companion.zip`: installer, companion executable, included anthem and installation instructions.
- `dist\Walkman_Mod.zip`: five-file Arsenal mod archive.

The archives are separate; neither contains the other. Intermediate outputs are `Mod\`, root `Walkman_Mod.zip` and `Source\WalkmanAtlas.png`. The asset builder recalculates the glyph-width table in `Source\walkman.lua` using the bundled font.

Build commands do not run the installer or launch the game. `ExecutionPolicy Bypass` applies only to the launched PowerShell process; these scripts do not persistently change execution-policy settings.

To rebuild the in-game resources without compiling the companion:

```powershell
py -3 -m pip install -r .\requirements-build.txt
py -3 .\Source\build_assets.py
```

## Optional tests

After building, run the Lua/archive/UI checks:

```powershell
py -3 -m pip install -r .\requirements-test.txt
New-Item -ItemType Directory -Force .\Notes | Out-Null
py -3 .\Source\validate.py
```

The validator requires `lupa.luajit21` and NumPy. It tests the real Lua and assets with substituted game/Win32 APIs and writes `Notes\Validation.json` plus preview images. Its optional external shared-loader fixture is skipped when absent. These tests do not run Helldivers 2 or the Windows audio engine.

Run the supplied C# tests on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test.ps1
```

The script compiles the tests and creates a temporary file-scanning fixture. Host tests substitute the audio boundary. Live game loading, GUI ordering and actual Windows codec playback require separate testing. See `docs/REVIEW.md` for checks performed while preparing this repository.

## Installation

Runtime requirements: Helldivers 2 on 64-bit Windows, Bingus Shared Loader, Mod Options Menu, Mod Bindings Menu, .NET Framework 4.8 and Windows Media Foundation.

Close the game and companion, extract the companion ZIP completely, and run `Install.cmd`. Import the separate mod ZIP into HD2 Arsenal and deploy it. Use the Music Library shortcut to add songs or choose an existing folder, then enable Show Walkman in the game's MODS settings. The mod launches the companion automatically when needed. Run the installer again for companion updates; imported songs and the selected music folder are retained.

Installation targets `%LOCALAPPDATA%\HD2Walkman` and creates a desktop shortcut without requesting administrator privileges. MP3, WAV, FLAC and M4A are accepted; playback depends on installed codecs. Adjust the game's Music volume separately. Some game interfaces can draw above the Walkman.

## Credits

Thanks to FAL9000 for permission to use [SuperPhone](https://www.nexusmods.com/helldivers2/mods/17148), credited to Monica Webster on its Nexus page, as a code reference. Thanks to CowboyBingus for Bingus Shared Loader, Mod Options Menu and Mod Bindings Menu.

Bundled DejaVu fonts retain their license in `Source/FontLicense.txt`. Artwork and anthem audio are included as build inputs. This review package introduces no new blanket license for the project or third-party assets.
