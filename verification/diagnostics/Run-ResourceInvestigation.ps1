[CmdletBinding()]
param(
    [ValidateSet('Window', 'XamlSource', 'Menu', 'Composite', 'Docking')]
    [string[]]$Scenarios = @('Window', 'XamlSource', 'Menu', 'Composite', 'Docking'),
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Debug',
    [ValidateRange(12, 100000)] [int]$Cycles = 312,
    [ValidateRange(1, 100000)] [int]$Batch = 100,
    [ValidateRange(0, 3600)] [int]$IdleSeconds = 30,
    [string]$ResultsDirectory,
    [switch]$CollectManagedHeap
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $repoRoot 'artifacts\resource-investigation' }
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
if (!('AvalonDock.Verification.Diagnostics.NativeHandleSnapshot' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'NativeHandleSnapshot.cs')
}
$runtimeSwitches = @{Window='runtime-lifetime'; XamlSource='xaml-source-lifetime'; Menu='menu-lifetime'; Composite='composite-lifetime'}
foreach ($scenario in $Scenarios) {
    $stem = "$Configuration-$scenario-$Cycles"
    $log = Join-Path $ResultsDirectory "$stem.log"
    $checkpoint = Join-Path $ResultsDirectory "$stem.csv"
    $indexLog = Join-Path $ResultsDirectory "$stem-handle-snapshots.csv"
    foreach ($oldOutput in @($log, $checkpoint, $indexLog)) {
        if (Test-Path -LiteralPath $oldOutput) { Remove-Item -LiteralPath $oldOutput }
    }
    if ($scenario -eq 'Docking') {
        $executable = Join-Path $repoRoot "sources\Experiments\Docking\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\Docking.exe"
        $runSwitch = 'boundary-smoke'
    } else {
        $executable = Join-Path $repoRoot "verification\PackageConsumer\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\PackageConsumer.exe"
        $runSwitch = $runtimeSwitches[$scenario]
    }
    if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Build the scenario first: $executable" }
    $arguments = @("--$runSwitch", ('"' + $log + '"'), '--lifetime-cycles', $Cycles,
        '--lifetime-batch', $Batch, '--lifetime-idle-seconds', $IdleSeconds,
        '--lifetime-checkpoint', ('"' + $checkpoint + '"'))
    $child = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "$scenario started: PID=$($child.Id), cycles=$Cycles, idleSeconds=$IdleSeconds"
    $deadline = [DateTime]::UtcNow.AddSeconds($Cycles * 3 + $IdleSeconds + 120)
    $seenStages = @{}
    $snapshotNumber = 0
    while (!$child.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { $child.Kill(); throw "$scenario exceeded its diagnostic deadline." }
        if (!(Test-Path -LiteralPath $checkpoint)) { continue }
        $rows = @(Import-Csv -LiteralPath $checkpoint)
        if ($rows.Count -eq 0) { continue }
        $row = $rows[-1]
        if (!$row.stage -or !$row.cycles -or $seenStages.ContainsKey($row.stage)) { continue }
        try { $entries = [AvalonDock.Verification.Diagnostics.NativeHandleSnapshot]::Capture($child.Id) }
        catch { if ($child.HasExited) { break }; throw }
        $seenStages[$row.stage] = $true
        $snapshotPath = Join-Path $ResultsDirectory ("$stem-handles-{0:D2}.csv" -f $snapshotNumber++)
        $entries | Export-Csv -LiteralPath $snapshotPath -NoTypeInformation
        [PSCustomObject]@{
            pid=$child.Id; utc=[DateTime]::UtcNow.ToString('o'); stage=$row.stage; cycles=$row.cycles;
            observedHandles=$row.handles; snapshotEntries=$entries.Count; snapshotPath=$snapshotPath
        } | Export-Csv -LiteralPath $indexLog -NoTypeInformation -Append
        $topTypes = ($entries | Group-Object Type | Sort-Object Count -Descending | Select-Object -First 3 | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', '
        Write-Output "$scenario $($row.stage): $($entries.Count) captured handles; $topTypes"
        if ($CollectManagedHeap -and ($row.stage -eq 'after 12 warm-up cycles' -or $row.stage -eq "after $Cycles cycles")) {
            # This option changes the run's GC timing. Keep its evidence separate
            # from uninstrumented resource/idle measurements.
            $heapPath = Join-Path $ResultsDirectory "$stem-$($row.cycles).gcdump"
            & dotnet-gcdump collect -p $child.Id -o $heapPath -t 20
            if ($LASTEXITCODE -ne 0) { throw "Managed heap collection failed for $scenario." }
            & dotnet-gcdump report $heapPath *> (Join-Path $ResultsDirectory "$stem-$($row.cycles)-heapstat.txt")
            if ($LASTEXITCODE -ne 0) { throw "Managed heap report failed for $scenario." }
        }
    }
    if (!(Test-Path -LiteralPath $log)) { throw "$scenario exited without a result log ($($child.ExitCode))." }
    $lines = Get-Content -LiteralPath $log
    if ($child.ExitCode -ne 0 -or @($lines | Where-Object { $_ -like 'FAIL:*' }).Count -ne 0) {
        $lines | Select-Object -Last 20 | Write-Output
        throw "$scenario failed ($($child.ExitCode)). Partial evidence remains in $ResultsDirectory."
    }
    if ($scenario -eq 'Docking' -and $lines[-1] -notmatch '^PASS: \d+ checks$') { throw 'Incomplete docking result.' }
    if ($scenario -ne 'Docking' -and @($lines | Where-Object { $_ -like "PASS: $Cycles plain WinUI*" }).Count -ne 1) { throw 'Incomplete runtime result.' }
    Write-Output "$scenario completed successfully. Log: $log"
}
