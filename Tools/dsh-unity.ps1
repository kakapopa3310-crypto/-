<#
.SYNOPSIS
    CLI-обвязка DSH Bridge для Unity-проекта «бумага».

.DESCRIPTION
    Два режима:
      * живой  — редактор Unity открыт, скрипт стучится в HTTP-мост (127.0.0.1:8787);
      * batch  — редактор закрыт, скрипт запускает Unity.exe -batchmode -executeMethod.

.EXAMPLE
    .\dsh-unity.ps1 ping
    .\dsh-unity.ps1 state
    .\dsh-unity.ps1 console -Count 30 -Type error
    .\dsh-unity.ps1 exec -Arg DshScripts.Examples.Ping -Params "привет"
    .\dsh-unity.ps1 menu -Arg "Window/General/Console"
    .\dsh-unity.ps1 screenshot
    .\dsh-unity.ps1 batch -Arg Compile
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command = 'help',

    [string]$Arg = '',
    [string]$Params = '',
    [string]$ProjectPath = '',
    [string]$UnityPath = 'D:\6000.6.7f1\Editor\Unity.exe',
    [int]$Port = 0,
    [int]$Count = 40,
    [string]$Type = 'all',
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)

# ------------------------------------------------------------------ helpers

function Get-BridgePort {
    if ($Port -gt 0) { return $Port }
    $file = Join-Path $ProjectPath 'Library\DshBridge\bridge.json'
    if (Test-Path $file) {
        try {
            $json = Get-Content -Path $file -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($json.port) { return [int]$json.port }
        } catch { }
    }
    return 8787
}

function Invoke-Bridge {
    param([string]$Path, [hashtable]$Payload)

    $bridgePort = Get-BridgePort
    $uri = "http://127.0.0.1:$bridgePort$Path"

    try {
        if ($null -ne $Payload) {
            $json = $Payload | ConvertTo-Json -Compress -Depth 8
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
            return Invoke-RestMethod -Uri $uri -Method Post -Body $bytes -ContentType 'application/json; charset=utf-8' -TimeoutSec 300
        }
        return Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 300
    } catch {
        $details = $_.Exception.Message
        $response = $null
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $response = $reader.ReadToEnd()
            } catch { }
        }
        if ($response) {
            try {
                $parsed = $response | ConvertFrom-Json
                if ($parsed.error) { $details = $parsed.error }
            } catch { $details = $response }
        }
        throw "Мост недоступен на порту $bridgePort ($details). Открой проект в Unity — мост стартует сам, либо Tools → DeepSeek Harness → Start Bridge."
    }
}

function Show-Result {
    param($Result)

    if ($null -eq $Result) { return }

    if ($Result.PSObject.Properties.Name -contains 'ok' -and -not $Result.ok) {
        Write-Error ("Ошибка: " + $Result.error)
        exit 1
    }

    $Result | ConvertTo-Json -Depth 10
    exit 0
}

function Invoke-UnityBatch {
    param([string]$Method, [string]$CommandFile, [string]$OutFile)

    if (-not (Test-Path $UnityPath)) {
        throw "Unity не найден: $UnityPath. Укажи путь через -UnityPath."
    }

    $lock = Join-Path $ProjectPath 'Library\UnityLockfile'
    if (Test-Path $lock) {
        throw "Проект уже открыт в редакторе. Используй живой режим: .\dsh-unity.ps1 ping"
    }

    $logDir = Join-Path $ProjectPath 'Logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $log = Join-Path $logDir 'dsh-batch.log'
    if (Test-Path $log) { Remove-Item $log -Force }

    if ([string]::IsNullOrWhiteSpace($OutFile)) {
        $OutFile = Join-Path $ProjectPath 'Library\DshBridge\batch-result.json'
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutFile) | Out-Null
    if (Test-Path $OutFile) { Remove-Item $OutFile -Force }

    $argStr = '-batchmode -quit -projectPath "' + $ProjectPath + '" -logFile "' + $log + '"'
    if ($Method) { $argStr += ' -executeMethod "' + $Method + '"' }
    if ($CommandFile) { $argStr += ' -dshCmd "' + $CommandFile + '"' }
    $argStr += ' -dshOut "' + $OutFile + '"'

    Write-Host "[dsh] Unity.exe $argStr" -ForegroundColor DarkGray
    $process = Start-Process -FilePath $UnityPath -ArgumentList $argStr -PassThru

    $limit = $TimeoutMinutes * 60 * 1000
    if (-not $process.WaitForExit($limit)) {
        throw "Unity не завершился за $TimeoutMinutes мин (PID $($process.Id), лог: $log)"
    }

    $exitCode = $process.ExitCode
    $errors = @()
    if (Test-Path $log) {
        $errors = Select-String -Path $log -Pattern 'error CS\d+|Unhandled exception|Fatal|Aborting batchmode' -Encoding UTF8 -ErrorAction SilentlyContinue |
            Select-Object -First 40 |
            ForEach-Object { $_.Line.Trim() }
    }

    $result = $null
    if (Test-Path $OutFile) {
        try { $result = Get-Content -Path $OutFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
    }

    $failed = $exitCode -ne 0 -or @($errors | Where-Object { $_ -match 'error CS' }).Count -gt 0 -or ($result -and -not $result.ok)

    [pscustomobject]@{
        ok      = -not $failed
        exit    = $exitCode
        log     = $log
        errors  = $errors
        result  = $result
    } | ConvertTo-Json -Depth 10

    if ($failed) { exit 1 }
    exit 0
}

function Show-Help {
    @'
DSH Bridge — управление Unity-проектом из DeepSeek Harness.

Живой режим (Unity открыт):
  .\dsh-unity.ps1 ping                         проверить связь
  .\dsh-unity.ps1 state                        сцена, сборка, выделение
  .\dsh-unity.ps1 console -Count 30 -Type error
  .\dsh-unity.ps1 exec -Arg DshScripts.Examples.Ping -Params "привет"
  .\dsh-unity.ps1 menu -Arg "Window/General/Console"
  .\dsh-unity.ps1 play | stop | pause
  .\dsh-unity.ps1 compile                      реимпорт + компиляция скриптов
  .\dsh-unity.ps1 screenshot [-Arg Assets/Shot.png]
  .\dsh-unity.ps1 hierarchy [-Arg Assets/Scenes/SampleScene.unity]
  .\dsh-unity.ps1 scene -Arg Assets/Scenes/Main.unity
  .\dsh-unity.ps1 save
  .\dsh-unity.ps1 build -Arg win64|linux64|webgl|android

Batchmode (Unity закрыт):
  .\dsh-unity.ps1 batch -Arg Compile           компиляция без редактора
  .\dsh-unity.ps1 batch -Arg State             состояние
  .\dsh-unity.ps1 batch -Arg Run -Params DshScripts.Examples.Ping   свой сценарий

Прочее:
  .\dsh-unity.ps1 open                         открыть проект в редакторе (с -force-d3d11)

Проект: {0}
'@ -f $ProjectPath
}

# -------------------------------------------------------------------- main

$cmd = $Command.ToLowerInvariant().Trim()

switch ($cmd) {
    'ping'   { Show-Result (Invoke-Bridge -Path '/ping') }
    'state'  { Show-Result (Invoke-Bridge -Path '/state') }
    'console' {
        $encodedType = [uri]::EscapeDataString($Type)
        Show-Result (Invoke-Bridge -Path "/console?count=$Count&type=$encodedType")
    }
    'hierarchy' { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'hierarchy'; arg = $Arg }) }
    'exec' {
        if ([string]::IsNullOrWhiteSpace($Arg)) { throw 'Укажи -Arg DshScripts.Класс.Метод' }
        Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'exec'; method = $Arg; args = $Params })
    }
    'menu' {
        if ([string]::IsNullOrWhiteSpace($Arg)) { throw 'Укажи -Arg "Пункт/Меню"' }
        Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'menu'; arg = $Arg })
    }
    'play'    { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'play' }) }
    'stop'    { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'stop' }) }
    'pause'   { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'pause' }) }
    'compile' { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'compile' }) }
    'screenshot' { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'screenshot'; arg = $Arg }) }
    'scene'   { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'scene'; arg = $Arg }) }
    'save'    { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'save' }) }
    'build'   { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'build'; arg = $Arg }) }
    'clear-log' { Show-Result (Invoke-Bridge -Path '/command' -Payload @{ cmd = 'clear-log' }) }

    'batch' {
        $method = if ([string]::IsNullOrWhiteSpace($Arg)) { 'DshBridge.DshBatch.State' } else { 'DshBridge.DshBatch.' + $Arg }

        if ($method -like '*.Run' -and $Params) {
            $cmdFile = Join-Path $ProjectPath 'Library\DshBridge\batch-command.json'
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $cmdFile) | Out-Null
            @{ cmd = 'exec'; method = $Params } | ConvertTo-Json -Compress | Set-Content -Path $cmdFile -Encoding UTF8
            Invoke-UnityBatch -Method $method -CommandFile $cmdFile
        } else {
            Invoke-UnityBatch -Method $method
        }
        break
    }

    'open' {
        if (Test-Path (Join-Path $ProjectPath 'Library\UnityLockfile')) {
            Write-Host 'Проект уже открыт в Unity.' -ForegroundColor Yellow
        } else {
            Start-Process -FilePath $UnityPath -ArgumentList ('-projectPath "' + $ProjectPath + '" -force-d3d11')
            Write-Host "Открываю $ProjectPath в Unity..." -ForegroundColor Green
        }
        exit 0
    }

    default { Show-Help; exit 0 }
}
