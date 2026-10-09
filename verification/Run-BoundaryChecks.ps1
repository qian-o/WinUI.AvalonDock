[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string[]]$Configuration = @('Debug', 'Release'),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$results = Join-Path $repoRoot 'artifacts\upstream-acceptance'
New-Item -ItemType Directory -Path $results -Force | Out-Null

# These checks use actual focus and modal windows. Run them serially.
foreach ($buildConfiguration in $Configuration) {
    if (!$NoBuild) {
        & dotnet build (Join-Path $repoRoot 'WinUI.AvalonDock.slnx') -c $buildConfiguration --no-restore -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed: $LASTEXITCODE" }
    }
    foreach ($sample in @('Docking', 'ToggleDocking', 'Mvvm')) {
        $executable = Join-Path $repoRoot "sources\Experiments\$sample\bin\$buildConfiguration\net10.0-windows10.0.19041.0\win-x64\$sample.exe"
        $log = Join-Path $results "$sample-$buildConfiguration-final.log"
        if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Missing sample: $executable" }
        if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
        $run = Start-Process -FilePath $executable -ArgumentList @('--boundary-smoke', ('"' + $log + '"')) -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddMinutes(3)
        while (!$run.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) {
                # Only stop the exact test process started above.
                $run.Kill()
                throw "$sample $buildConfiguration timed out. See $log"
            }
        }
        if ($run.ExitCode -ne 0) { throw "$sample $buildConfiguration failed ($($run.ExitCode)). See $log" }
        $lines = Get-Content -LiteralPath $log
        if (@($lines | Where-Object { $_ -match '^PASS: \d+ checks$' }).Count -ne 1 -or $lines[-1] -notmatch '^PASS: \d+ checks$') {
            throw "$sample $buildConfiguration did not record a completed run. See $log"
        }
        Write-Output "$sample ${buildConfiguration}: $($lines[-1]) ($log)"
    }
    $overlayExecutable = Join-Path $repoRoot "sources\Experiments\Docking\bin\$buildConfiguration\net10.0-windows10.0.19041.0\win-x64\Docking.exe"
    $overlayLog = Join-Path $results "Overlay-$buildConfiguration-final.log"
    if (Test-Path -LiteralPath $overlayLog) { Remove-Item -LiteralPath $overlayLog }
    $overlayRun = Start-Process -FilePath $overlayExecutable -ArgumentList @('--overlay-lifetime', ('"' + $overlayLog + '"')) -WindowStyle Hidden -PassThru
    $overlayDeadline = [DateTime]::UtcNow.AddMinutes(2)
    while (!$overlayRun.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $overlayDeadline) {
            $overlayRun.Kill()
            throw "Overlay $buildConfiguration timed out. See $overlayLog"
        }
    }
    if ($overlayRun.ExitCode -ne 0 -or !(Test-Path -LiteralPath $overlayLog)) {
        throw "Overlay $buildConfiguration failed ($($overlayRun.ExitCode)). See $overlayLog"
    }
    $overlayLines = Get-Content -LiteralPath $overlayLog
    if ($overlayLines[-1] -ne 'RESULT: PASS' -or @($overlayLines | Where-Object { $_ -like 'FAIL:*' }).Count -ne 0) {
        throw "Overlay $buildConfiguration did not complete successfully. See $overlayLog"
    }
    Write-Output "Overlay ${buildConfiguration}: $($overlayLines[-1]) ($overlayLog)"
}
