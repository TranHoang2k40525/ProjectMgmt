param(
    [string]$BaseUrl = "http://localhost:11434",
    [string]$Model = "qwen2.5:3b"
)

$ErrorActionPreference = "Stop"

$body = @{ model = $Model; stream = $false } | ConvertTo-Json
Write-Host "Pulling Ollama model '$Model' from $BaseUrl ..."
$response = Invoke-RestMethod -Method Post -Uri ($BaseUrl.TrimEnd('/') + '/api/pull') -ContentType 'application/json' -Body $body

if ($response.status -ne 'success') {
    throw "Ollama did not report success. Status: $($response.status)"
}

Write-Host "Model '$Model' is ready."
