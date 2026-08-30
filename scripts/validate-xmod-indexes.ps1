param(
    [string]$DdlPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DdlPath)) {
    $downloadsPath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $ddlFile = Get-ChildItem -LiteralPath $downloadsPath -Directory |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter 'projectmgmt_schema_mysql.sql' -File } |
        Select-Object -First 1
    if ($null -eq $ddlFile) { throw 'Could not locate projectmgmt_schema_mysql.sql under Downloads.' }
    $DdlPath = $ddlFile.FullName
}

$resolvedPath = [IO.Path]::GetFullPath($DdlPath)
if (-not [IO.File]::Exists($resolvedPath)) {
    throw "DDL file was not found: $resolvedPath"
}

$sql = [IO.File]::ReadAllText($resolvedPath, [Text.Encoding]::UTF8)
$tablePattern = '(?ms)^CREATE TABLE `(?<table>[^`]+)` \((?<body>.*?)^\) ENGINE='
$columnPattern = '(?m)^\s*.(?<column>[A-Za-z0-9_]+).[^\r\n]*XMOD'
$keyPattern = '(?m)^\s*(?:PRIMARY KEY|UNIQUE KEY `[^`]+`|KEY `[^`]+`)\s*\((?<columns>[^\r\n\)]*)\)'

$results = [Collections.Generic.List[object]]::new()
foreach ($tableMatch in [regex]::Matches($sql, $tablePattern)) {
    $table = $tableMatch.Groups['table'].Value
    $body = $tableMatch.Groups['body'].Value
    $leftmostIndexedColumns = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($keyMatch in [regex]::Matches($body, $keyPattern)) {
        $firstColumn = [regex]::Match($keyMatch.Groups['columns'].Value, '`(?<column>[^`]+)`')
        if ($firstColumn.Success) { [void]$leftmostIndexedColumns.Add($firstColumn.Groups['column'].Value) }
    }

    foreach ($columnMatch in [regex]::Matches($body, $columnPattern)) {
        $column = $columnMatch.Groups['column'].Value
        $results.Add([pscustomobject]@{
            Table = $table
            Column = $column
            HasUsableLeftPrefixIndex = $leftmostIndexedColumns.Contains($column)
        })
    }
}

$missing = $results | Where-Object { -not $_.HasUsableLeftPrefixIndex }
Write-Host "DDL: $resolvedPath"
Write-Host "XMOD columns: $($results.Count)"
Write-Host "Missing usable left-prefix index: $($missing.Count)"

if ($missing) {
    $missing | Format-Table -AutoSize
    Write-Error "XMOD index audit found missing indexes. This script is read-only and made no database/DDL changes."
    exit 1
}

Write-Host "All XMOD columns have a usable left-prefix index."
