<#
.SYNOPSIS
    Command-line helpers for the Unity AI NPC project (Windows PowerShell 5.1+).

.EXAMPLE
    ./scripts/unity.ps1 test               # EditMode + PlayMode tests, results in TestResults/
    ./scripts/unity.ps1 test -Platform EditMode
    ./scripts/unity.ps1 scene              # regenerate Assets/AiNpc/Scenes/NpcDemo.unity
    ./scripts/unity.ps1 build              # Builds/Windows/UnityAiNpc.exe
    ./scripts/unity.ps1 screenshot         # build if needed, play the scripted demo, save docs/screenshot.png

.NOTES
    Close the project in the Unity Editor first; batch mode can't open a project that's already open.
    The editor is found via $env:UNITY_EDITOR, or the Unity Hub default path for the version in
    ProjectSettings/ProjectVersion.txt.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('test', 'scene', 'build', 'screenshot')]
    [string]$Command,

    [ValidateSet('All', 'EditMode', 'PlayMode')]
    [string]$Platform = 'All'
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-UnityEditor {
    if ($env:UNITY_EDITOR) { return $env:UNITY_EDITOR }
    $versionLine = Get-Content (Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
    $version = ($versionLine -split ':', 2)[1].Trim()
    $path = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path $path)) { throw "Unity $version not found at '$path'. Set `$env:UNITY_EDITOR to Unity.exe." }
    return $path
}

function Invoke-Unity([string[]]$UnityArgs, [string]$LogFile) {
    $editor = Get-UnityEditor
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    $allArgs = @('-batchmode', '-projectPath', "`"$ProjectRoot`"", '-logFile', "`"$LogFile`"") + $UnityArgs
    Write-Host "> Unity $($UnityArgs -join ' ')" -ForegroundColor DarkGray
    $proc = Start-Process -FilePath $editor -ArgumentList $allArgs -Wait -PassThru -NoNewWindow
    return $proc.ExitCode
}

function Show-TestResults([string]$XmlPath) {
    if (-not (Test-Path $XmlPath)) { Write-Host "  no results file (see the .log next to it)" -ForegroundColor Red; return }
    [xml]$xml = Get-Content $XmlPath
    $run = $xml.DocumentElement
    Write-Host ("  total {0}, passed {1}, failed {2}, skipped {3}" -f $run.total, $run.passed, $run.failed, $run.skipped)
    foreach ($case in $xml.SelectNodes('//test-case[@result="Failed"]')) {
        Write-Host "  FAILED $($case.fullname)" -ForegroundColor Red
        $message = $case.SelectSingleNode('failure/message')
        if ($message) { Write-Host "    $($message.InnerText.Trim())" }
    }
}

$results = Join-Path $ProjectRoot 'TestResults'
$exe = Join-Path $ProjectRoot 'Builds/Windows/UnityAiNpc.exe'

switch ($Command) {
    'test' {
        $platforms = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }
        $failed = $false
        foreach ($p in $platforms) {
            $xmlPath = Join-Path $results "$($p.ToLower()).xml"
            if (Test-Path $xmlPath) { Remove-Item $xmlPath }
            $code = Invoke-Unity @('-runTests', '-testPlatform', $p, '-testResults', "`"$xmlPath`"") (Join-Path $results "$($p.ToLower()).log")
            Write-Host "$p tests: exit code $code"
            Show-TestResults $xmlPath
            if ($code -ne 0) { $failed = $true }
        }
        if ($failed) { exit 1 }
    }
    'scene' {
        $code = Invoke-Unity @('-quit', '-executeMethod', 'UnityAiNpc.Editor.SceneBuilder.BuildFromCommandLine') (Join-Path $results 'scene.log')
        exit $code
    }
    'build' {
        $code = Invoke-Unity @('-quit', '-executeMethod', 'UnityAiNpc.Editor.BuildScript.BuildWindows') (Join-Path $results 'build.log')
        if ($code -eq 0) { Write-Host "Built $exe" }
        exit $code
    }
    'screenshot' {
        if (-not (Test-Path $exe)) {
            $code = Invoke-Unity @('-quit', '-executeMethod', 'UnityAiNpc.Editor.BuildScript.BuildWindows') (Join-Path $results 'build.log')
            if ($code -ne 0) { exit $code }
        }
        # The player reads .env from its own folder; reuse the project's.
        $projectEnv = Join-Path $ProjectRoot '.env'
        if (Test-Path $projectEnv) { Copy-Item $projectEnv (Join-Path (Split-Path $exe) '.env') -Force }

        $shot = Join-Path $ProjectRoot 'docs/screenshot.png'
        Write-Host "> Playing the scripted conversation..."
        $playerArgs = @('-aiNpcDemo', '-aiNpcScreenshot', "`"$shot`"", '-aiNpcQuit',
            '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
            '-logFile', "`"$(Join-Path $results 'player.log')`"")
        $proc = Start-Process -FilePath $exe -ArgumentList $playerArgs -Wait -PassThru
        if (Test-Path $shot) { Write-Host "Saved $shot" } else { Write-Host "No screenshot written; see TestResults/player.log" -ForegroundColor Red; exit 1 }
    }
}
