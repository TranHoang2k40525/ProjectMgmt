param(
    [string]$Path = (Join-Path $PSScriptRoot '..\data\ai-evaluation\v1\evaluation.jsonl')
)

$ErrorActionPreference = "Stop"

$resolvedPath = [IO.Path]::GetFullPath($Path)
if (-not [IO.File]::Exists($resolvedPath)) {
    throw "Evaluation file was not found: $resolvedPath"
}

$records = [Collections.Generic.List[object]]::new()
$lineNumber = 0
foreach ($line in [IO.File]::ReadLines($resolvedPath)) {
    $lineNumber++
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    # ConvertFrom-Json only gained -Depth in newer PowerShell versions. Keeping
    # the validator compatible with Windows PowerShell 5.1 makes the local and
    # CI validation paths equivalent.
    try { $record = $line | ConvertFrom-Json }
    catch { throw "Invalid JSON on line $lineNumber`: $($_.Exception.Message)" }

    foreach ($property in 'id','language','category','input','expected','tags','reviewerStatus') {
        if ($null -eq $record.$property) { throw "Line $lineNumber is missing '$property'." }
    }
    if ($record.language -notin @('vi','en')) { throw "Line $lineNumber has unsupported language '$($record.language)'." }
    if ([string]::IsNullOrWhiteSpace($record.input.title) -or [string]::IsNullOrWhiteSpace($record.input.description)) {
        throw "Line $lineNumber must contain a non-empty input title and description."
    }
    if ($record.expected.subTasks.Count -lt 1) { throw "Line $lineNumber has no expected subTasks." }
    foreach ($subTask in $record.expected.subTasks) {
        if ([string]::IsNullOrWhiteSpace($subTask.summary) -or [string]::IsNullOrWhiteSpace($subTask.description)) {
            throw "Line $lineNumber contains a sub-task without summary/description."
        }
        if ($subTask.acceptanceCriteria.Count -lt 1) { throw "Line $lineNumber contains a sub-task without acceptance criteria." }
    }
    $records.Add($record)
}

if ($records.Count -lt 50 -or $records.Count -gt 100) {
    throw "Expected 50-100 records, found $($records.Count)."
}

$duplicateIds = $records | Group-Object id | Where-Object Count -gt 1
if ($duplicateIds) { throw "Duplicate record IDs: $($duplicateIds.Name -join ', ')" }

$languages = $records | Group-Object language | ForEach-Object { "$($_.Name)=$($_.Count)" }
$categories = $records | Group-Object category | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
Write-Host "Valid evaluation set: $($records.Count) records"
Write-Host "Languages: $($languages -join ', ')"
Write-Host "Categories: $($categories -join ', ')"
