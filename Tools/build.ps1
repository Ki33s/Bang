param([ValidateSet('Prepare','Windows','Web','TurnWindows','TurnWeb')][string]$Target = 'Windows', [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskLogs = Join-Path $taskRoot 'Logs'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
$taskArguments = '-batchmode -quit -projectPath "' + $taskRoot + '" -executeMethod BuildProject.' + $Target + ' -logFile "' + (Join-Path $taskLogs "$Target.log") + '"'
$taskProcess = Start-Process -FilePath $Unity -ArgumentList $taskArguments -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) { throw "Unity завершился с кодом $($taskProcess.ExitCode). См. Logs/$Target.log" }
