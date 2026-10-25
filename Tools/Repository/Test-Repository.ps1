param(
    [string]$BaseRef,
    [switch]$Staged
)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Push-Location $root
try {
    $problems = [System.Collections.Generic.List[string]]::new()
    $branch = (git branch --show-current).Trim()
    if ($branch -eq 'main') { $problems.Add('Do not implement directly on main.') }
    if ($branch -notmatch '^issue/[1-9][0-9]*-[a-z0-9]+(?:-[a-z0-9]+)*$') { $problems.Add('Branch does not match issue/<number>-<kebab-case>.') }
    $tracked = @(git -c core.quotepath=false ls-files)
    foreach ($path in $tracked) {
        if ($path -match '^(Library|Temp|Obj|Logs|UserSettings|Builds?|\.uloop/outputs)/' -or $path -match '(^|/)__pycache__/') {
            $problems.Add('Generated output is tracked: ' + $path)
        }
    }
    $arguments = @('-c', 'core.quotepath=false', 'diff', '--name-status', '--diff-filter=ACMR')
    if ($Staged) { $arguments += '--cached' }
    elseif ($BaseRef) { $arguments += ($BaseRef + '...HEAD') }
    $changes = @(& git @arguments)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect changes.' }
    foreach ($change in $changes) {
        $parts = $change -split "`t"; $path = $parts[-1]
        if ($path.StartsWith('Assets/') -and -not $path.EndsWith('.meta')) {
            if (-not ($tracked -contains ($path + '.meta'))) { $problems.Add('Asset .meta missing from index: ' + $path) }
            if ($parts[0] -match '^[AC]') {
                $parent = Split-Path $path -Parent
                while ($parent -and $parent.Replace('\', '/') -ne 'Assets') {
                    $folderMeta = $parent.Replace('\', '/') + '.meta'
                    if (-not ($tracked -contains $folderMeta)) { $problems.Add('Folder .meta missing from index: ' + $folderMeta) }
                    $parent = Split-Path $parent -Parent
                }
            }
        }
        if ($path.StartsWith('Assets/') -and $path.EndsWith('.meta')) {
            $asset = $path.Substring(0, $path.Length - 5)
            $isFolder = @($tracked | Where-Object { $_.StartsWith($asset + '/') }).Count -gt 0
            if (-not ($tracked -contains $asset) -and -not $isFolder) { $problems.Add('Orphan .meta in index: ' + $path) }
        }
    }
    if ($BaseRef) {
        $messages = @(git log --format=%s ($BaseRef + '..HEAD'))
        foreach ($message in $messages) {
            if ($message -notmatch '^(feat|fix|refactor|perf|test|docs|chore)\([a-z0-9-]+\): .+ \(#[0-9]+(?:, #[0-9]+)*\)$') {
                $problems.Add('Commit convention: ' + $message)
            }
        }
    }
    $rules = Get-Content -LiteralPath '.github/main-ruleset.json' -Raw | ConvertFrom-Json
    if ($rules.conditions.ref_name.include -notcontains 'refs/heads/main') { $problems.Add('Ruleset must target main.') }
    if (@($rules.rules | Where-Object type -eq 'required_status_checks').Count -gt 0) { $problems.Add('CI checks must not be required before CI is stable.') }
    $examples = @('Assets/Scenes/PrototypeCombatValidation.unity', 'Assets/Art/test.aseprite')
    $attributes = @(git check-attr merge -- $examples)
    $ignored = @(git check-ignore -- .uloop/outputs/example.json Library/example.bin .uloop/launch-restart-guard.json)
    if ($ignored.Count -ne 3) { $problems.Add('Expected generated files are not ignored.') }
    [pscustomobject]@{ branch = $branch; base = $BaseRef; staged = [bool]$Staged; changedFiles = $changes.Count; attributes = $attributes; problems = @($problems); success = $problems.Count -eq 0 } | ConvertTo-Json -Depth 6
    if ($problems.Count -gt 0) { throw 'Repository validation failed; review the listed problems.' }
} finally { Pop-Location }
