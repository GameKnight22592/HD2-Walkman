$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 compiler is missing.' }
$source = Join-Path $PSScriptRoot '..\Source'
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'bin') | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$PSScriptRoot\app.manifest" "/out:$PSScriptRoot\bin\WalkmanAudio.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $source 'NativeAudio.cs') (Join-Path $source 'SongSpectrum.cs') (Join-Path $source 'ShuffleQueue.cs') (Join-Path $source 'Companion.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Compiled WalkmanAudio.exe.'
