param([int]$Port = 8081, [string]$Origins = 'http://localhost:8080,http://127.0.0.1:8080')
$ErrorActionPreference = 'Stop'
$taskDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $taskDotnet) {
    $taskCandidate = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Data\DotNetSdk\dotnet.exe'
    if (Test-Path -LiteralPath $taskCandidate) { $taskDotnet = $taskCandidate }
}
if (-not $taskDotnet) { throw 'Установите .NET 8 SDK или укажите dotnet в PATH.' }
$env:BANG_URL = "http://127.0.0.1:$Port"
$env:BANG_ORIGINS = $Origins
& $taskDotnet run --project (Join-Path $PSScriptRoot 'Bang.Server.csproj')
