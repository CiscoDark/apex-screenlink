param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Directory)
$manifest=Get-Content (Join-Path $root 'dependency-hashes.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest.PSObject.Properties){
    $path=Join-Path (Join-Path $root 'engine') $entry.Name
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value){throw "Hash mismatch: $path"}
}
foreach($path in @((Join-Path $root 'ApexScreenLink.exe'),(Join-Path $root 'engine/scrcpy.exe'),(Join-Path $root 'engine/adb.exe'))){
    $bytes=[IO.File]::ReadAllBytes($path)
    $offset=[BitConverter]::ToInt32($bytes,0x3c)
    $machine=[BitConverter]::ToUInt16($bytes,$offset+4)
    if([IO.Path]::GetFileName($path) -eq 'adb.exe' -and $machine -eq 0x14c){Write-Output 'PASS official ADB x86 helper (runs under Windows x64 WoW64)'}
    elseif($machine -eq 0x8664){Write-Output "PASS x64 PE: $([IO.Path]::GetFileName($path))"}
    else{throw "Unexpected executable architecture: $path"}
}
Write-Output 'PASS engine hashes'
& (Join-Path $root 'engine/scrcpy.exe') --version
if($LASTEXITCODE){throw 'Engine DLL loading failed'}
& (Join-Path $root 'engine/adb.exe') version
if($LASTEXITCODE){throw 'ADB DLL loading failed'}
