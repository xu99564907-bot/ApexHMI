# One-shot data migration: replace Name="Application" placeholder tags in
# tags.json / appsettings.json with real leaves expanded from PLC XML.
# Mirrors Services/PlcVariableImportService.cs logic. Skips DB8050_DriveControl
# (already covered by prior import; full expansion of arrays would balloon
# leaf count past 30k).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$XmlPath,
    [Parameter(Mandatory = $true)] [string]$TagsPath,
    [Parameter(Mandatory = $true)] [string]$AppSettingsPath
)

$ErrorActionPreference = 'Stop'

# ----- 1. Parse TypeList -----
[xml]$xml = Get-Content -Raw -Encoding UTF8 -LiteralPath $XmlPath
$ns  = 'http://www.3s-software.com/schemas/Symbolconfiguration.xsd'
$nsm = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$nsm.AddNamespace('s', $ns)

$types = @{}
foreach ($el in $xml.SelectNodes('/s:Symbolconfiguration/s:TypeList/*', $nsm)) {
    $tname = $el.GetAttribute('name')
    if ([string]::IsNullOrEmpty($tname)) { continue }
    $iec = $el.GetAttribute('iecname'); if ([string]::IsNullOrEmpty($iec)) { $iec = $tname }
    switch ($el.LocalName) {
        'TypeSimple'  { $types[$tname] = @{ Kind = 'Simple'; IecName = $iec; Class = $el.GetAttribute('typeclass') } }
        'TypeArray'   {
            $dim = $el.SelectSingleNode('s:ArrayDim', $nsm)
            $min = 0; $max = -1
            if ($dim) { [void][int]::TryParse($dim.GetAttribute('minrange'), [ref]$min); [void][int]::TryParse($dim.GetAttribute('maxrange'), [ref]$max) }
            $types[$tname] = @{ Kind = 'Array'; IecName = $iec; BaseType = $el.GetAttribute('basetype'); Min = $min; Max = $max }
        }
        'TypeUserDef' {
            $members = @()
            foreach ($m in $el.SelectNodes('s:UserDefElement', $nsm)) {
                $mn = $m.GetAttribute('iecname'); $mt = $m.GetAttribute('type')
                if ($mn -and $mt) { $members += ,@($mn, $mt) }
            }
            $types[$tname] = @{ Kind = 'Struct'; IecName = $iec; Members = $members }
        }
    }
}
Write-Host "Parsed types: $($types.Count)"

# ----- 2. Helpers (mirror PlcVariableImportService) -----
function Normalize-Iec([string]$iec) {
    if ([string]::IsNullOrWhiteSpace($iec)) { return 'STRING' }
    switch ($iec.Trim().ToUpperInvariant()) {
        'BOOL'  { 'Boolean'; break }
        'BYTE'  { 'Byte'; break }
        'WORD'  { 'UInt16'; break }
        'DWORD' { 'UInt32'; break }
        'INT'   { 'Int16'; break }
        'DINT'  { 'Int32'; break }
        'UINT'  { 'UInt16'; break }
        'UDINT' { 'UInt32'; break }
        'REAL'  { 'Single'; break }
        'LREAL' { 'Double'; break }
        'TIME'  { 'TimeSpan'; break }
        default { $iec }
    }
}
function Resolve-Category([string]$path) {
    if ($path -match 'Fault|Alarm') { return 'Alarm' }
    if ($path -match 'CylCtrl|VacCtrl') { return 'Cylinder' }
    if ($path -match 'AxisCtrl') { return 'Axis' }
    if ($path -match 'Sensor') { return 'IO' }
    if ($path -match 'Recipe') { return 'Recipe' }
    if ($path -match 'Communication') { return 'Communication' }
    if ($path -match 'Count') { return 'Count' }
    return 'General'
}
function Top-Group([string]$path) { ($path -split '\.')[0] }

# ----- 3. Scalar-only expansion -----
# We only emit leaves for direct scalar children of a target DB node. Struct
# and array roots that previously sat as broken placeholders are dropped --
# they cannot be read as scalar values via OPC UA anyway, and a full struct
# expansion exploded to 22k+ leaves (T_Str_TcpClient alone yields ~1k each).
$leaves = New-Object System.Collections.Generic.List[object]

function Is-ScalarType([string]$typeName) {
    if (-not $types.ContainsKey($typeName)) { return $false }
    return $types[$typeName].Kind -eq 'Simple'
}

function Emit-Leaf {
    param([string]$path, [string]$iec, [string]$access, [string]$comment, [string]$directAddr)
    $writable = $access -match 'Write'
    $direction = if ($writable) { 'Output' } else { 'Input' }
    $descParts = New-Object System.Collections.Generic.List[string]
    if ($comment)     { [void]$descParts.Add($comment.Trim()) }
    if ($directAddr)  { [void]$descParts.Add(([string][char]0x5730 + [string][char]0x5740 + ': ' + $directAddr)) }
    [void]$descParts.Add(([string][char]0x7B26 + [string][char]0x53F7 + [string][char]0x8DEF + [string][char]0x5F84 + ': Application.' + $path))
    $tag = [ordered]@{
        Name         = $path
        NodeId       = "Application.$path"
        DataType     = (Normalize-Iec $iec)
        Category     = (Resolve-Category $path)
        Group        = (Top-Group $path)
        Direction    = $direction
        CurrentValue = ''
        Description  = ($descParts -join ' | ')
        IsAlarm      = ($path -match 'Fault|Alarm')
        IsWritable   = $writable
    }
    [void]$leaves.Add($tag)
}

# ----- 4. Walk only DBs that have broken placeholders. Emit scalar-only. -----
# DB8050_DriveControl is excluded because its arrays of structs would balloon
# the leaf count, and the existing 1257 imported tags already cover what the
# UI actually binds against. Struct/array placeholders in the other DBs are
# silently dropped (they were unreadable to begin with).
$targetDbs = @('DB8000_Control','DB8002_Recipe','DB8003_Count','DB8005_Communication','DB8090_Other')
$appNode = $xml.SelectSingleNode('/s:Symbolconfiguration/s:NodeList/s:Node[@name="Application"]', $nsm)
if (-not $appNode) { throw 'Application root node not found in XML' }

$skippedNonScalar = @()
foreach ($dbNode in $appNode.SelectNodes('s:Node', $nsm)) {
    $dbName = $dbNode.GetAttribute('name')
    if ($targetDbs -notcontains $dbName) { continue }

    foreach ($child in $dbNode.SelectNodes('s:Node', $nsm)) {
        $cname = $child.GetAttribute('name')
        $ctype = $child.GetAttribute('type')
        $cacc  = $child.GetAttribute('access')
        $cdir  = $child.GetAttribute('directaddress')
        $cmtNode = $child.SelectSingleNode('s:Comment', $nsm)
        $ccmt = if ($cmtNode) { $cmtNode.InnerText } else { '' }

        if ([string]::IsNullOrEmpty($ctype)) { continue }
        if (Is-ScalarType $ctype) {
            $kind = $types[$ctype]
            if ($kind.Class -eq 'Pointer') { continue }
            Emit-Leaf "$dbName.$cname" $kind.IecName $cacc $ccmt $cdir
        } else {
            $skippedNonScalar += "$dbName.$cname ($ctype)"
        }
    }
}
Write-Host "Generated scalar leaves: $($leaves.Count)"
if ($skippedNonScalar.Count -gt 0) {
    Write-Host "Skipped $($skippedNonScalar.Count) struct/array placeholders (unreadable as scalars):"
    $skippedNonScalar | ForEach-Object { Write-Host "  - $_" }
}

# ----- 5. Patch JSON files -----
function Patch-TagFile {
    param([string]$path, [string]$rootKey)
    $jsonText = Get-Content -Raw -Encoding UTF8 -LiteralPath $path
    $obj = $jsonText | ConvertFrom-Json
    $tags = @($obj.$rootKey)
    $beforeTotal = $tags.Count

    $kept = @($tags | Where-Object { $_.Name -ne 'Application' })
    $removed = $beforeTotal - $kept.Count

    $existing = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($t in $kept) { [void]$existing.Add([string]$t.NodeId) }

    $added = 0
    foreach ($leaf in $leaves) {
        if ($existing.Contains([string]$leaf.NodeId)) { continue }
        $kept += ,([pscustomobject]$leaf)
        [void]$existing.Add([string]$leaf.NodeId)
        $added++
    }

    $obj.$rootKey = $kept
    $newJson = $obj | ConvertTo-Json -Depth 30
    [System.IO.File]::WriteAllText($path, $newJson, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("[{0}] before={1} removedPlaceholders={2} addedLeaves={3} after={4}" -f (Split-Path -Leaf $path), $beforeTotal, $removed, $added, $kept.Count)
}

Patch-TagFile $TagsPath 'Tags'
Patch-TagFile $AppSettingsPath 'Tags'

Write-Host 'Done.'
