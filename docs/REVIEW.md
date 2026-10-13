# Nexus Mods review notes

## Review target and provenance

Walkman v0.3.7 consists of the Lua game mod, a C# companion, installer scripts, an audio asset and resource-building tools. The application sources were recovered from the saved `Walkman_Source.zip`; release references were recovered from `Walkman.zip` and `Walkman_Mod.zip`.

The recovered companion executable has SHA-256:

```text
d067a2b88c75d0690ec36e963c8467188f376274243028402db45b4a5e599c21
```

It matches the companion executable previously examined during release preparation. The exact quarantined companion ZIP reported by the uploader was not available for byte comparison; its reported hash and size are recorded separately in `Release_Hashes.json`. Do not interpret the reference executable hash as a verification of that entire ZIP.

## Runtime behavior

### Windows companion

- Creates `%LOCALAPPDATA%\HD2Walkman` and its default Music directory.
- Reads the saved music-folder setting and scans that directory recursively for MP3, WAV, FLAC and M4A files. Reparse points are skipped. Imported files are copied with collision-safe names; originals are retained.
- Uses Windows Media Foundation and COM (`mfplat.dll`, `mfreadwrite.dll`, `ole32.dll`) for local audio playback and spectrum decoding. Unmanaged delegate/vtable calls in `NativeAudio.cs` and `SongSpectrum.cs` implement those APIs.
- Opens an existing 4096-byte mapping named `Local\HD2Walkman_<PID>` to exchange playback commands, state, titles and spectrum values. This is inter-process communication, not arbitrary process-memory access by the companion.
- Verifies that the supplied PID belongs to `helldivers2`, and exits its game-mode context when the game exits, requests shutdown or stops updating its heartbeat.
- Offers explicit UI actions to open the music folder in Explorer and the log in Notepad.
- Saves log entries, selected-folder configuration and refresh notifications under its per-user directory.
- Requests `asInvoker` privileges. The supplied companion source contains no networking client, credential-reading code or security-setting modification.

### Lua mod

- Runs inside the game through the separately installed Bingus Shared Loader.
- Wraps the existing update/shutdown lifecycle, creates a Stingray GUI, and integrates with Mod Options Menu and Mod Bindings Menu.
- Creates the named mapping, launches the installed `WalkmanAudio.exe` with `--game <PID>`, and exchanges playback/UI data with it.
- Uses Win32 APIs through LuaJIT FFI. These include mapping APIs, `CreateProcessW`, module lookup and `ReadProcessMemory` against the current game process.
- Reads selected game structures and verifies known function bytes before calling native game routines for input inhibition/restoration while the cursor-controlled player is open. The native input-handling offsets are game-build dependent.
- Restores input/cursor ownership and releases its handles during shutdown.
- Writes its game-side log under `%LOCALAPPDATA%\CowboyBingus\Helldivers2\Logs\Walkman.log` when that log directory is available.

The companion and Lua mod have different native-call responsibilities; review both. No claim is made that the entire mod avoids process-memory inspection.

### Installer

`Install.cmd` launches `Support/Install.ps1` in Windows PowerShell with a process-scoped execution-policy argument. The installer checks that the game and companion are closed, creates the per-user installation directories, copies the compiled companion under the names `WalkmanAudio.exe` and `WalkmanLibrary.exe`, copies the anthem, creates a desktop library shortcut and opens the library. The two installed executables are copies of the same build.

The installer preserves songs and the selected-folder setting. It removes the obsolete bundled WAV anthem if present. It does not install a service or scheduled task, request elevation, add a system startup entry, or configure antivirus exclusions.

## Build dependencies

The companion uses Microsoft's .NET Framework compiler and system framework assemblies, with no downloaded NuGet dependency. The asset builder needs Python and Pillow. The original fonts, artwork, anthem and resource-format templates are included. Optional validation additionally requires Lupa with LuaJIT 2.1 support and NumPy.

`Source/build_win32_resource.py` is an optional manifest-resource generator for alternative compiler workflows; the documented Windows build embeds the XML manifest directly and does not require that script. `Source/SpectrumDump.cs` is a development utility, not a companion compilation input.

## Changes made for this source-review package

- Added the README, reviewer notes, dependency lists, reference hashes, Git attributes/ignore rules, companion instructions and a wrapper for the existing C# tests.
- Changed `Build.ps1` to package the companion and Arsenal mod as separate downloads.
- Made the asset builder's Lua and manifest text writes explicitly use UTF-8 and LF newlines for build portability.
- Did not edit the runtime C# or Lua application code, native application manifest, installer behavior, fonts or supplied artwork/audio.

## Validation performed while preparing this package

- Rebuilt the in-game mod using Python and Pillow 12.3.0.
- Compared every rebuilt archive entry with the saved release mod: the main patch, GPU resources, stream file, icon and manifest matched byte for byte.
- Confirmed that all four companion runtime C# files and `walkman.lua` are unchanged from the saved source package.
- Confirmed that the recovered release companion matches the previously recorded companion SHA-256.

The current preparation environment does not have a Windows/.NET Framework compiler or Windows Media Engine. Companion compilation, PowerShell build/installer execution, the C# tests, and actual audio playback were not rerun here. LuaJIT validation was also not rerun because its optional dependency was unavailable. The supplied tests and build commands are available for the reviewer to run on Windows.

Compiler versions, assembly timestamps/MVIDs and archive metadata can change rebuilt executable/ZIP hashes. Reference hashes identify the recovered release files; byte-identical companion recompilation is not promised.
