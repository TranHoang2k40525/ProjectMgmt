[CmdletBinding()]
param(
    [string]$HealthUrl = 'http://localhost:5101/health',
    [ValidateRange(1, 60)]
    [int]$RetryCount = 12,
    [ValidateRange(1, 30)]
    [int]$RetryDelaySeconds = 5,
    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$lastError = $null

for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $HealthUrl -TimeoutSec $TimeoutSeconds
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
            Write-Output "Health check passed: $HealthUrl (HTTP $($response.StatusCode))."
            return
        }

        $lastError = "Unexpected HTTP status $($response.StatusCode)."
    }
    catch {
        $lastError = $_.Exception.Message
    }

    Write-Output "Health check attempt $attempt/$RetryCount failed: $lastError"
    if ($attempt -lt $RetryCount) {
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

throw "Health check failed after $RetryCount attempts: $HealthUrl. Last error: $lastError"
