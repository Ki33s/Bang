$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -Target TurnWindows
& (Join-Path $PSScriptRoot 'build.ps1') -Target TurnWeb
Write-Output 'BANG_ONE_TURN_BUILDS_READY'
