$ErrorActionPreference = 'Stop'
& py -3 (Join-Path $PSScriptRoot 'Source\build_assets.py')
if ($LASTEXITCODE -ne 0) { throw 'Asset build failed. Python 3 and Pillow are required.' }
& (Join-Path $PSScriptRoot 'Companion\build.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Companion\bin\WalkmanAudio.exe') -Destination (Join-Path $PSScriptRoot 'Support\WalkmanAudio.exe') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$output = Join-Path $dist 'Walkman_Companion.zip'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$archive = [System.IO.Compression.ZipFile]::Open($output,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('Install.cmd','Support/Install.ps1','Support/WalkmanAudio.exe','Support/BuiltIn/Super Earth National Anthem.mp3','COMPANION_README.txt')) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot $name),$name,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Walkman_Mod.zip') -Destination (Join-Path $dist 'Walkman_Mod.zip') -Force
Write-Host 'Built dist\Walkman_Companion.zip and dist\Walkman_Mod.zip (separate downloads).'
