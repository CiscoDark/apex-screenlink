param([string]$Dotnet='dotnet',[string]$OutputDirectory='', [string]$EngineArchive='')
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts/ApexScreenLink'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(!$EngineArchive){$EngineArchive=Join-Path $root 'third-party/scrcpy-win64-v3.3.4.zip'}
if(!(Test-Path -LiteralPath $EngineArchive)){
    Invoke-WebRequest 'https://github.com/Genymobile/scrcpy/releases/download/v3.3.4/scrcpy-win64-v3.3.4.zip' -OutFile $EngineArchive
}
if((Get-FileHash -LiteralPath $EngineArchive -Algorithm SHA256).Hash -ne 'd8a155b7c180b7ca4cdadd40712b8750b63f3aab48cb5b8a2a39ac2d0d4c5d38'){throw 'Upstream archive SHA256 mismatch'}
Push-Location $root
try {
    & $Dotnet run --project tests/Apex.Tests/Apex.Tests.csproj -c Release
    if($LASTEXITCODE){throw 'Tests failed'}
    & $Dotnet publish src/Apex.Desktop/Apex.Desktop.csproj -c Release --self-contained true -r win-x64 -o $OutputDirectory
    if($LASTEXITCODE){throw 'Publish failed'}
    $staging=Join-Path $root 'artifacts/vendor'
    Expand-Archive -LiteralPath $EngineArchive -DestinationPath $staging -Force
    New-Item -ItemType Directory -Force (Join-Path $OutputDirectory 'engine') | Out-Null
    Get-ChildItem (Join-Path $staging 'scrcpy-win64-v3.3.4') -File | Where-Object {$_.Extension -in '.exe','.dll' -or $_.Name -eq 'scrcpy-server'} | Copy-Item -Destination (Join-Path $OutputDirectory 'engine')
    Copy-Item -LiteralPath (Join-Path $root 'dependency-hashes.json') -Destination $OutputDirectory
    Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'README.txt'),(Join-Path $root 'BUILD-INFO.txt') -Destination $OutputDirectory
    foreach($folder in 'recordings','screenshots','data'){New-Item -ItemType Directory -Force (Join-Path $OutputDirectory $folder) | Out-Null}
    Write-Output "Portable application: $OutputDirectory"
} finally {Pop-Location}
