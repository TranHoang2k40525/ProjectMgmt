#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$RegistrationToken = $env:PROJECTMGMT_RUNNER_TOKEN,

    [string]$RepositoryUrl = 'https://github.com/TranHoang2k40525/ProjectMgmt',
    [string]$Repository = 'TranHoang2k40525/ProjectMgmt',
    [string]$GitHubCliPath,
    [string]$TokenPipeName,
    [string]$RunnerRoot = 'C:\actions-runner',
    [string]$RunnerName = "$env:COMPUTERNAME-ProjectMgmt-Test",
    [string]$WorkFolder = '_work',
    [string]$DeploymentRoot = 'C:\Users\hoang\Downloads\Test-ProjectMgmt',
    [string]$ConfigRoot = 'C:\Config\ProjectMgmt',
    [string]$BackupRoot = 'C:\DeployBackup\ProjectMgmt-Test',
    [string]$LogRoot = 'C:\Logs\ProjectMgmt'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RegistrationToken)) {
    if (-not [string]::IsNullOrWhiteSpace($TokenPipeName)) {
        $pipe = New-Object IO.Pipes.NamedPipeClientStream(
            '.',
            $TokenPipeName,
            [IO.Pipes.PipeDirection]::In
        )
        try {
            $pipe.Connect(30000)
            $reader = New-Object IO.StreamReader($pipe)
            try {
                $RegistrationToken = $reader.ReadLine()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $pipe.Dispose()
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($GitHubCliPath) -and (Test-Path -LiteralPath $GitHubCliPath -PathType Leaf)) {
        $RegistrationToken = & $GitHubCliPath api `
            --method POST `
            "repos/$Repository/actions/runners/registration-token" `
            --jq '.token'
    }

    if ([string]::IsNullOrWhiteSpace($RegistrationToken)) {
        throw 'Could not obtain a short-lived GitHub Actions runner registration token.'
    }
}

$resolvedRunnerRoot = [IO.Path]::GetFullPath($RunnerRoot).TrimEnd('\')
if ($resolvedRunnerRoot -ne 'C:\actions-runner') {
    throw "Unexpected runner root. Expected C:\actions-runner, received: $resolvedRunnerRoot"
}

if (Test-Path -LiteralPath (Join-Path $resolvedRunnerRoot '.runner') -PathType Leaf) {
    throw "A GitHub runner is already configured at $resolvedRunnerRoot. Refusing to overwrite it."
}

[void](New-Item -ItemType Directory -Path $resolvedRunnerRoot -Force)

$headers = @{ 'User-Agent' = 'ProjectMgmt-Runner-Setup' }
$release = Invoke-RestMethod `
    -Headers $headers `
    -Uri 'https://api.github.com/repos/actions/runner/releases/latest'

$asset = $release.assets | Where-Object {
    $_.name -match '^actions-runner-win-x64-[0-9.]+\.zip$'
} | Select-Object -First 1

if ($null -eq $asset) {
    throw 'Could not find the Windows x64 asset in the latest GitHub Actions runner release.'
}

$archivePath = Join-Path $env:TEMP $asset.name
Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $asset.browser_download_url -OutFile $archivePath

if ($asset.PSObject.Properties.Name -contains 'digest' -and -not [string]::IsNullOrWhiteSpace([string]$asset.digest)) {
    $expectedHash = ([string]$asset.digest -replace '^sha256:', '').ToUpperInvariant()
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) {
        throw "GitHub runner archive hash mismatch. Expected $expectedHash, found $actualHash."
    }
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $resolvedRunnerRoot -Force

$configCommand = Join-Path $resolvedRunnerRoot 'config.cmd'
if (-not (Test-Path -LiteralPath $configCommand -PathType Leaf)) {
    throw "Runner config.cmd was not found after extraction: $configCommand"
}

Push-Location $resolvedRunnerRoot
try {
    & $configCommand `
        --unattended `
        --url $RepositoryUrl `
        --token $RegistrationToken `
        --name $RunnerName `
        --labels 'projectmgmt-test' `
        --work $WorkFolder `
        --runasservice

    if ($LASTEXITCODE -ne 0) {
        throw "GitHub runner configuration failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$runnerService = Get-CimInstance Win32_Service | Where-Object {
    $_.Name -match '^actions\.runner\.' -and $_.PathName -like "*$resolvedRunnerRoot*"
} | Select-Object -First 1

if ($null -eq $runnerService) {
    throw 'Runner was configured, but its Windows service could not be found.'
}

foreach ($path in @($DeploymentRoot, $BackupRoot, $LogRoot)) {
    $resolvedPath = [IO.Path]::GetFullPath($path).TrimEnd('\')
    [void](New-Item -ItemType Directory -Path $resolvedPath -Force)
    & icacls.exe $resolvedPath /grant "$($runnerService.StartName):(OI)(CI)(M)" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant Modify permission to $($runnerService.StartName) on $resolvedPath."
    }
}

$resolvedConfigRoot = [IO.Path]::GetFullPath($ConfigRoot).TrimEnd('\')
[void](New-Item -ItemType Directory -Path $resolvedConfigRoot -Force)
& icacls.exe $resolvedConfigRoot /grant "$($runnerService.StartName):(OI)(CI)(RX)" /T /C | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to grant read permission to $($runnerService.StartName) on $resolvedConfigRoot."
}

$service = Get-Service -Name $runnerService.Name
if ($service.Status -ne 'Running') {
    Start-Service -Name $runnerService.Name
}

Write-Output "RunnerName=$RunnerName"
Write-Output "RunnerVersion=$($release.tag_name)"
Write-Output "ServiceName=$($runnerService.Name)"
Write-Output "ServiceAccount=$($runnerService.StartName)"
Write-Output 'Labels=self-hosted,windows,x64,projectmgmt-test'
