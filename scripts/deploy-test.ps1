[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackendArtifactPath,

    [string]$FrontendArtifactPath,

    [string]$ApiTargetPath = 'C:\Users\hoang\Downloads\Test-ProjectMgmt\Api',

    [string]$WebTargetPath = 'C:\Users\hoang\Downloads\Test-ProjectMgmt\Web',

    [string]$ConfigPath = 'C:\Users\hoang\Downloads\Test-ProjectMgmt\Config\appsettings.Staging.json',

    [string]$BackupRoot = 'C:\DeployBackup\ProjectMgmt-Test',

    [string]$LogPath = 'C:\Logs\ProjectMgmt\deploy-test.log',

    [string]$HealthUrl = 'http://ProjectMgmt.dev.com/health',

    [ValidateRange(1, 20)]
    [int]$KeepBackups = 5,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourceRepository = [IO.Path]::GetFullPath('C:\Users\hoang\Downloads\ProjectMgmt').TrimEnd('\')

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($Path)).TrimEnd('\')
}

function Assert-SafeMutableDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = Get-FullPath -Path $Path
    $root = [IO.Path]::GetPathRoot($fullPath).TrimEnd('\')

    if ($fullPath -eq $root -or $fullPath.Length -lt 10) {
        throw "Refusing unsafe $Purpose path: $fullPath"
    }

    if ($fullPath -eq $sourceRepository -or
        $fullPath.StartsWith($sourceRepository + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $sourceRepository.StartsWith($fullPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing $Purpose path that overlaps the source repository: $fullPath"
    }

    return $fullPath
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    [void](New-Item -ItemType Directory -Path $Destination -Force)
    foreach ($item in @(Get-ChildItem -LiteralPath $Source -Force)) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Remove-DeploymentItem {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateRange(1, 60)][int]$RetryCount = 30,
        [ValidateRange(1, 10)][int]$DelaySeconds = 2
    )

    $lastError = $null
    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) {
            return
        }

        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            $lastError = $_.Exception.Message
            if ($attempt -lt $RetryCount) {
                Write-Output "Removal attempt $attempt/$RetryCount failed for '$Path': $lastError"
                Start-Sleep -Seconds $DelaySeconds
            }
        }
    }

    throw "Could not remove deployment item '$Path' after $RetryCount attempts: $lastError"
}

function Clear-DeploymentDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string[]]$PreserveNames = @()
    )

    foreach ($item in @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)) {
        if ($PreserveNames -notcontains $item.Name) {
            Remove-DeploymentItem -Path $item.FullName
        }
    }
}

function Write-DeploymentLog {
    param([Parameter(Mandatory = $true)][string]$Message)

    $line = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Write-Output $line
    Add-Content -LiteralPath $script:resolvedLogPath -Value $line -Encoding UTF8
}

function Test-DeploymentHealth {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$RetryCount = 12,
        [int]$DelaySeconds = 5
    )

    $lastError = $null
    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-DeploymentLog -Message "Health check passed: $Url (HTTP $($response.StatusCode))."
                return $true
            }

            $lastError = "Unexpected HTTP status $($response.StatusCode)."
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Write-DeploymentLog -Message "Health attempt $attempt/$RetryCount failed: $lastError"
        if ($attempt -lt $RetryCount) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    return $false
}

$backendSource = Get-FullPath -Path $BackendArtifactPath
if (-not (Test-Path -LiteralPath $backendSource -PathType Container)) {
    throw "Backend artifact directory does not exist: $backendSource"
}

foreach ($requiredFile in @(
    'ProjectMgmt.Solution.dll',
    'ProjectMgmt.Solution.deps.json',
    'ProjectMgmt.Solution.runtimeconfig.json',
    'web.config'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $backendSource $requiredFile) -PathType Leaf)) {
        throw "Backend artifact is missing required file: $requiredFile"
    }
}

$frontendSource = $null
if (-not [string]::IsNullOrWhiteSpace($FrontendArtifactPath)) {
    $frontendSource = Get-FullPath -Path $FrontendArtifactPath
    if (-not (Test-Path -LiteralPath $frontendSource -PathType Container)) {
        throw "Frontend artifact directory does not exist: $frontendSource"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $frontendSource 'index.html') -PathType Leaf)) {
        throw "Frontend artifact is missing index.html: $frontendSource"
    }
}

$resolvedApiTarget = Assert-SafeMutableDirectory -Path $ApiTargetPath -Purpose 'API deployment'
$resolvedWebTarget = Assert-SafeMutableDirectory -Path $WebTargetPath -Purpose 'Web deployment'
$resolvedBackupRoot = Assert-SafeMutableDirectory -Path $BackupRoot -Purpose 'backup root'
$resolvedConfigPath = Get-FullPath -Path $ConfigPath
$script:resolvedLogPath = Get-FullPath -Path $LogPath

if (-not (Test-Path -LiteralPath $resolvedConfigPath -PathType Leaf)) {
    throw "Staging config does not exist: $resolvedConfigPath"
}

try {
    $stagingConfig = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
    $connectionString = [string]$stagingConfig.ConnectionStrings.ProjectMgmt
}
catch {
    throw "Staging config is invalid JSON or is missing ConnectionStrings:ProjectMgmt: $resolvedConfigPath"
}

if ([string]::IsNullOrWhiteSpace($connectionString) -or $connectionString.Contains('<AIVEN_PASSWORD_ROTATED>')) {
    throw 'ConnectionStrings:ProjectMgmt in the Staging config is empty or still contains the password placeholder.'
}

if ($connectionString -notmatch '(?i)SslMode\s*=\s*(Required|VerifyCA|VerifyFull)') {
    throw 'The TEST connection string must require TLS using SslMode=Required, VerifyCA, or VerifyFull.'
}

if ($ValidateOnly) {
    Write-Output 'Deployment inputs and safety checks are valid. No deployment changes were made.'
    return
}

[void](New-Item -ItemType Directory -Path $resolvedApiTarget -Force)
[void](New-Item -ItemType Directory -Path $resolvedWebTarget -Force)
[void](New-Item -ItemType Directory -Path $resolvedBackupRoot -Force)
[void](New-Item -ItemType Directory -Path (Split-Path -Parent $script:resolvedLogPath) -Force)

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$currentBackup = Join-Path $resolvedBackupRoot $timestamp
$apiBackup = Join-Path $currentBackup 'Api'
$webBackup = Join-Path $currentBackup 'Web'
$offlinePath = Join-Path $resolvedApiTarget 'app_offline.htm'
$apiHadContent = @(Get-ChildItem -LiteralPath $resolvedApiTarget -Force -ErrorAction SilentlyContinue).Count -gt 0
$webHadContent = @(Get-ChildItem -LiteralPath $resolvedWebTarget -Force -ErrorAction SilentlyContinue).Count -gt 0
$deploymentSucceeded = $false

try {
    Write-DeploymentLog -Message "Starting TEST deployment from backend artifact: $backendSource"

    if ($apiHadContent -or $webHadContent) {
        [void](New-Item -ItemType Directory -Path $currentBackup -Force)
        if ($apiHadContent) {
            Copy-DirectoryContent -Source $resolvedApiTarget -Destination $apiBackup
        }
        if ($webHadContent) {
            Copy-DirectoryContent -Source $resolvedWebTarget -Destination $webBackup
        }
        Write-DeploymentLog -Message "Current deployment backed up to: $currentBackup"
    }

    Set-Content -LiteralPath $offlinePath -Value 'ProjectMgmt TEST is being deployed.' -Encoding ASCII
    Start-Sleep -Seconds 2

    Clear-DeploymentDirectory -Path $resolvedApiTarget -PreserveNames @('app_offline.htm')
    Copy-DirectoryContent -Source $backendSource -Destination $resolvedApiTarget
    Copy-Item -LiteralPath $resolvedConfigPath -Destination (Join-Path $resolvedApiTarget 'appsettings.Staging.json') -Force

    if ($null -ne $frontendSource) {
        Clear-DeploymentDirectory -Path $resolvedWebTarget
        Copy-DirectoryContent -Source $frontendSource -Destination $resolvedWebTarget
    }

    Remove-DeploymentItem -Path $offlinePath

    if (-not (Test-DeploymentHealth -Url $HealthUrl)) {
        throw "The new deployment failed its health check: $HealthUrl"
    }

    $deploymentSucceeded = $true
    Write-DeploymentLog -Message 'TEST deployment completed successfully.'

    $oldBackups = @(Get-ChildItem -LiteralPath $resolvedBackupRoot -Directory | Sort-Object Name -Descending | Select-Object -Skip $KeepBackups)
    foreach ($oldBackup in $oldBackups) {
        $oldBackupPath = Get-FullPath -Path $oldBackup.FullName
        if (-not $oldBackupPath.StartsWith($resolvedBackupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing unexpected backup cleanup target: $oldBackupPath"
        }
        Remove-Item -LiteralPath $oldBackupPath -Recurse -Force
        Write-DeploymentLog -Message "Removed old backup: $oldBackupPath"
    }
}
catch {
    $deploymentError = $_.Exception.Message
    Write-DeploymentLog -Message "Deployment failed: $deploymentError"

    try {
        Set-Content -LiteralPath $offlinePath -Value 'ProjectMgmt TEST rollback is running.' -Encoding ASCII
        Start-Sleep -Seconds 2

        if (Test-Path -LiteralPath $apiBackup -PathType Container) {
            Clear-DeploymentDirectory -Path $resolvedApiTarget -PreserveNames @('app_offline.htm')
            Copy-DirectoryContent -Source $apiBackup -Destination $resolvedApiTarget
            Copy-Item -LiteralPath $resolvedConfigPath -Destination (Join-Path $resolvedApiTarget 'appsettings.Staging.json') -Force
        }

        if ($null -ne $frontendSource) {
            Clear-DeploymentDirectory -Path $resolvedWebTarget
            if (Test-Path -LiteralPath $webBackup -PathType Container) {
                Copy-DirectoryContent -Source $webBackup -Destination $resolvedWebTarget
            }
        }

        Remove-DeploymentItem -Path $offlinePath

        if (Test-Path -LiteralPath $apiBackup -PathType Container) {
            $rollbackHealthy = Test-DeploymentHealth -Url $HealthUrl -RetryCount 6 -DelaySeconds 5
            if ($rollbackHealthy) {
                Write-DeploymentLog -Message 'Deployment failed - rollback executed and health check passed.'
            }
            else {
                Write-DeploymentLog -Message 'Deployment failed - rollback executed but health check also failed.'
            }
        }
        else {
            Write-DeploymentLog -Message 'Deployment failed and no previous API backup was available.'
        }
    }
    catch {
        Write-DeploymentLog -Message "Rollback failed: $($_.Exception.Message)"
    }

    throw "TEST deployment failed: $deploymentError"
}
finally {
    if (Test-Path -LiteralPath $offlinePath -PathType Leaf) {
        try {
            Remove-DeploymentItem -Path $offlinePath -RetryCount 5
        }
        catch {
            Write-Output "Final app_offline cleanup failed: $($_.Exception.Message)"
        }
    }
}

if (-not $deploymentSucceeded) {
    throw 'TEST deployment did not reach a successful terminal state.'
}
