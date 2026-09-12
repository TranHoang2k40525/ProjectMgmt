#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SiteName = 'ProjectMgmt.dev.com',
    [string]$ExpectedPhysicalPath = 'C:\Users\hoang\Downloads\Test-ProjectMgmt\Api',
    [string]$EnvironmentName = 'Staging'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($null -eq $site) {
    throw "IIS site does not exist: $SiteName. This script will not create a duplicate site."
}

$expectedPath = [IO.Path]::GetFullPath($ExpectedPhysicalPath).TrimEnd('\')
$actualPath = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables([string]$site.PhysicalPath)
).TrimEnd('\')

if (-not $actualPath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "IIS site path mismatch. Expected '$expectedPath', found '$actualPath'."
}

$appPoolName = [string]$site.ApplicationPool
$appPoolPath = "IIS:\AppPools\$appPoolName"
if (-not (Test-Path -LiteralPath $appPoolPath)) {
    throw "IIS app pool does not exist: $appPoolName"
}

if ($PSCmdlet.ShouldProcess($appPoolName, 'Set No Managed Code, Integrated pipeline, and Staging environment')) {
    Set-ItemProperty -LiteralPath $appPoolPath -Name managedRuntimeVersion -Value ''
    Set-ItemProperty -LiteralPath $appPoolPath -Name managedPipelineMode -Value 'Integrated'

    $environmentFilter = "system.applicationHost/applicationPools/add[@name='$appPoolName']/environmentVariables"
    $existingEnvironment = Get-WebConfigurationProperty `
        -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter "$environmentFilter/add[@name='ASPNETCORE_ENVIRONMENT']" `
        -Name '.' `
        -ErrorAction SilentlyContinue

    if ($null -eq $existingEnvironment) {
        Add-WebConfigurationProperty `
            -PSPath 'MACHINE/WEBROOT/APPHOST' `
            -Filter $environmentFilter `
            -Name '.' `
            -Value @{ name = 'ASPNETCORE_ENVIRONMENT'; value = $EnvironmentName }
    }
    else {
        Set-WebConfigurationProperty `
            -PSPath 'MACHINE/WEBROOT/APPHOST' `
            -Filter "$environmentFilter/add[@name='ASPNETCORE_ENVIRONMENT']" `
            -Name 'value' `
            -Value $EnvironmentName
    }

    $poolIdentity = "IIS AppPool\$appPoolName"
    & icacls.exe $expectedPath /grant "${poolIdentity}:(OI)(CI)(RX)" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant read access to $poolIdentity on $expectedPath."
    }

    Restart-WebAppPool -Name $appPoolName
}

$updatedPool = Get-Item -LiteralPath $appPoolPath
$bindings = @($site.Bindings.Collection | ForEach-Object {
    "{0}://{1}" -f $_.protocol, $_.bindingInformation
})

Write-Output "Site=$SiteName"
Write-Output "PhysicalPath=$actualPath"
Write-Output "AppPool=$appPoolName"
Write-Output "ManagedRuntimeVersion=$($updatedPool.managedRuntimeVersion)"
Write-Output "ManagedPipelineMode=$($updatedPool.managedPipelineMode)"
Write-Output "ASPNETCORE_ENVIRONMENT=$EnvironmentName"
Write-Output "Bindings=$($bindings -join ',')"
