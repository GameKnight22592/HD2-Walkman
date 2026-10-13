$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler is missing.' }
$source = Join-Path $PSScriptRoot 'Source'
$bin = Join-Path $PSScriptRoot 'Companion\bin'
New-Item -ItemType Directory -Force $bin | Out-Null
$refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
$runtime = @((Join-Path $source 'Companion.cs'),(Join-Path $source 'ShuffleQueue.cs'),(Join-Path $source 'SongSpectrum.cs'))
$fileTests = Join-Path $bin 'CompanionTests.exe'
& $compiler /nologo /target:exe /platform:x64 /main:HD2Walkman.CompanionTests "/out:$fileTests" @refs @runtime (Join-Path $source 'NativeAudio.cs') (Join-Path $source 'CompanionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Companion test compilation failed.' }
$hostTests = Join-Path $bin 'HostLogicTests.exe'
& $compiler /nologo /target:exe /platform:x64 /define:WALKMAN_HOST_TESTS /main:HD2Walkman.HostLogicTests "/out:$hostTests" @refs @runtime (Join-Path $source 'HostLogicTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Host test compilation failed.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('WalkmanTests-' + [Guid]::NewGuid().ToString('N'))
try {
    $nested = Join-Path $fixture ('nested-' + [char]0x97F3 + [char]0x697D)
    New-Item -ItemType Directory -Force $nested | Out-Null
    foreach ($file in @((Join-Path $fixture 'song.MP3'),(Join-Path $nested 'mix.wav'),(Join-Path $nested 'album.FLAC'),(Join-Path $fixture 'ignore.txt'),(Join-Path $fixture 'song.mp3.exe'))) {
        [IO.File]::WriteAllBytes($file,[byte[]]@())
    }
    & $fileTests $fixture
    if ($LASTEXITCODE -ne 0) { throw 'Companion tests failed.' }
    & $hostTests
    if ($LASTEXITCODE -ne 0) { throw 'Host tests failed.' }
} finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
Write-Host 'Offline C# tests passed. Native playback and live game loading are separate checks.'
