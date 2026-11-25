param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference='Stop'
function Get-PeImports([string]$Path) {
    $b=[IO.File]::ReadAllBytes($Path)
    if($b.Length -lt 64 -or $b[0] -ne 77 -or $b[1] -ne 90){return}
    $pe=[BitConverter]::ToInt32($b,60)
    $count=[BitConverter]::ToUInt16($b,$pe+6)
    $optionalSize=[BitConverter]::ToUInt16($b,$pe+20)
    $optional=$pe+24
    $magic=[BitConverter]::ToUInt16($b,$optional)
    $dataDir=if($magic -eq 0x20b){$optional+112}else{$optional+96}
    $importRva=[BitConverter]::ToUInt32($b,$dataDir+8)
    if(!$importRva){return}
    $sections=@()
    for($i=0;$i -lt $count;$i++) {
        $offset=$optional+$optionalSize+40*$i
        $sections+=@{Rva=[BitConverter]::ToUInt32($b,$offset+12);Size=[Math]::Max([BitConverter]::ToUInt32($b,$offset+8),[BitConverter]::ToUInt32($b,$offset+16));Raw=[BitConverter]::ToUInt32($b,$offset+20)}
    }
    function Resolve-Rva([uint32]$Rva) {foreach($s in $sections){if($Rva -ge $s.Rva -and $Rva -lt $s.Rva+$s.Size){return [int]($Rva-$s.Rva+$s.Raw)}};throw "Invalid RVA in $Path"}
    $offset=Resolve-Rva $importRva
    while($true) {
        $nameRva=[BitConverter]::ToUInt32($b,$offset+12)
        if(!$nameRva){break}
        $start=Resolve-Rva $nameRva
        $end=$start
        while($b[$end] -ne 0){$end++}
        [Text.Encoding]::ASCII.GetString($b,$start,$end-$start)
        $offset+=20
    }
}
$missing=@();$files=Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object {$_.Extension -in '.exe','.dll'}
foreach($file in $files){
    foreach($import in Get-PeImports $file.FullName){
        if($import -match '^(api-ms-win-|ext-ms-win-)'){continue}
        $candidates=@((Join-Path $file.DirectoryName $import),(Join-Path $Directory $import),(Join-Path "$env:WINDIR/System32" $import))
        if(!($candidates | Where-Object {Test-Path -LiteralPath $_})){$missing+="$($file.Name) -> $import"}
    }
}
if($missing.Count){$missing;throw 'Unresolved PE imports'}
Write-Output "PASS import scan: $($files.Count) PE candidates; no unresolved direct imports on this Windows host. API-set imports are provided by Windows. Delay-load and clean-VM behavior require separate testing."
