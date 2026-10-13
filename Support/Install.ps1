$ErrorActionPreference = 'Stop'
if (Get-Process helldivers2, WalkmanAudio, WalkmanLibrary -ErrorAction SilentlyContinue) { throw 'Close Helldivers 2 and Walkman before installing.' }
$destination = Join-Path $env:LOCALAPPDATA 'HD2Walkman'
$exe = Join-Path $PSScriptRoot 'WalkmanAudio.exe'
$anthem = Join-Path $PSScriptRoot 'BuiltIn\Super Earth National Anthem.mp3'
if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $anthem)) { throw 'Extract the complete Walkman ZIP before running Install.cmd.' }
New-Item -ItemType Directory -Force $destination | Out-Null
New-Item -ItemType Directory -Force (Join-Path $destination 'Music') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $destination 'BuiltIn') | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $destination 'WalkmanAudio.exe') -Force
Copy-Item -LiteralPath $exe -Destination (Join-Path $destination 'WalkmanLibrary.exe') -Force
Copy-Item -LiteralPath $anthem -Destination (Join-Path $destination 'BuiltIn\Super Earth National Anthem.mp3') -Force
$obsoleteAnthem = Join-Path $destination 'BuiltIn\Super Earth National Anthem.wav'
if (Test-Path -LiteralPath $obsoleteAnthem) { Remove-Item -LiteralPath $obsoleteAnthem -Force }
Write-Host 'Updating the existing installation; imported songs and your selected folder are preserved.'
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'Walkman Music Library.lnk'))
$shortcut.TargetPath = Join-Path $destination 'WalkmanLibrary.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Save()
Write-Host 'SUCCESS: Walkman installed. Your existing music and settings were kept.' -ForegroundColor Green
Write-Host 'The supplied high-quality Super Earth National Anthem is included.'
Write-Host 'Add songs or choose your music folder in the library window.'
Write-Host 'Select a song and use Preview / pause to test audio before launching the game.'
Write-Host 'Next: import Walkman_Mod.zip into Arsenal and apply/deploy.'
Start-Process -FilePath (Join-Path $destination 'WalkmanLibrary.exe')
