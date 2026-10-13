$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -Target Windows
& (Join-Path $PSScriptRoot 'build.ps1') -Target Web
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs\Inter-LICENSE.txt') -Destination (Join-Path $taskRoot 'Builds\Web\Inter-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs\Inter-LICENSE.txt') -Destination (Join-Path $taskRoot 'Builds\Windows\Inter-LICENSE.txt') -Force
Compress-Archive -Path (Join-Path $taskRoot 'Builds\Web\*') -DestinationPath (Join-Path $taskRoot 'Builds\DustAndPowder-itch.zip') -Force
Compress-Archive -Path (Join-Path $taskRoot 'Builds\Windows\*') -DestinationPath (Join-Path $taskRoot 'Builds\DustAndPowder-Windows.zip') -Force
Write-Output 'BANG_ALL_BUILDS_AND_ARCHIVES_READY'
