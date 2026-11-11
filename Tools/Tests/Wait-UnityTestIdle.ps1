param(
    [string]$ULoop = 'C:\Users\hirot\AppData\Roaming\npm\uloop.cmd',
    [ValidateRange(1, 60)][int]$TimeoutSeconds = 30
)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$code = 'using UnityEditor; using UnityEditor.TestTools.TestRunner.Api; using UnityEngine.SceneManagement; using System.Reflection; var active=(bool)typeof(TestRunnerApi).GetMethod(""IsRunActive"",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null); return !active && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !SceneManager.GetActiveScene().name.StartsWith(""InitTestScene"");'
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$stable = 0
Push-Location $root
try {
    do {
        $previous = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $raw = (& $ULoop execute-dynamic-code --code $code 2>&1 | ForEach-Object { $_.ToString() } | Out-String)
            $status = $LASTEXITCODE
        } finally { $ErrorActionPreference = $previous }
        $raw = $raw -replace '\x1b\[[0-9;]*m', ''
        if ($status -ne 0) { throw ('Cannot verify Unity test state: ' + $raw) }
        $result = $raw | ConvertFrom-Json
        if (-not $result.Success) { throw ('Cannot verify Unity test state: ' + $result.ErrorMessage) }
        if ($result.Result -eq 'True' -or $result.Result -eq 'true') { $stable++ } else { $stable = 0 }
        if ($stable -ge 2) { return }
        Start-Sleep -Milliseconds 500
    } while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    throw 'Unity is still playing, compiling, testing or restoring its scene. Do not start another test run.'
} finally { Pop-Location }
