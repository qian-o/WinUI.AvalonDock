[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('Window', 'XamlSource', 'Menu', 'Composite')]
    [string[]]$Baselines = @('Window', 'XamlSource', 'Menu'),
    [ValidateRange(12, 100000)]
    [int]$Cycles = 312,
    [ValidateRange(1, 100000)]
    [int]$Batch = 100,
    [ValidateRange(0, 3600)]
    [int]$IdleSeconds = 30,
    [string]$ResultsDirectory,
    [switch]$SkipBuild,
    [switch]$CompositeNoMenu,
    [switch]$CompositeNoRegions
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $PSScriptRoot 'PackageConsumer.csproj'
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $repoRoot 'artifacts\package-consumer\runtime-extended' }
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
if (!$SkipBuild) {
    & dotnet build $project -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Consumer build failed: $LASTEXITCODE" }
}
$executable = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\PackageConsumer.exe"
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Build the consumer first: $executable" }
$switches = @{ Window = 'runtime-lifetime'; XamlSource = 'xaml-source-lifetime'; Menu = 'menu-lifetime'; Composite = 'composite-lifetime' }
foreach ($baseline in $Baselines) {
    $log = Join-Path $ResultsDirectory "$Configuration-$baseline-$Cycles.log"
    $checkpoint = Join-Path $ResultsDirectory "$Configuration-$baseline-$Cycles.csv"
    $arguments = @(
        "--$($switches[$baseline])", "`"$log`"",
        '--lifetime-cycles', $Cycles,
        '--lifetime-batch', $Batch,
        '--lifetime-idle-seconds', $IdleSeconds,
        '--lifetime-checkpoint', "`"$checkpoint`""
    )
    if ($baseline -eq 'Composite' -and $CompositeNoMenu) { $arguments += '--composite-no-menu' }
    if ($baseline -eq 'Composite' -and $CompositeNoRegions) { $arguments += '--composite-no-regions' }
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru -WindowStyle Hidden
    Write-Output "$baseline baseline started: PID=$($process.Id), cycles=$Cycles, batch=$Batch, idleSeconds=$IdleSeconds, checkpoint=$checkpoint"
    # Three seconds is each asynchronous content-state wait's failure limit.
    # This deadline stops only the child process created for this baseline.
    $deadline = [DateTime]::UtcNow.AddSeconds($Cycles * 3 + $IdleSeconds + 60)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) {
            $process.Kill()
            throw "$baseline runtime baseline timed out. Partial samples remain in $log and $checkpoint."
        }
    }
    if (!(Test-Path -LiteralPath $log)) { throw "$baseline exited with $($process.ExitCode) without a log." }
    $lines = Get-Content -LiteralPath $log
    if ($process.ExitCode -ne 0 -or @($lines | Where-Object { $_ -like 'FAIL:*' }).Count -gt 0) {
        $lines | Write-Output
        throw "$baseline runtime baseline failed with exit code $($process.ExitCode)."
    }
    $completion = @($lines | Where-Object { $_ -like "PASS: $Cycles plain WinUI*" })
    if ($completion.Count -ne 1) { throw "Incomplete baseline result: $log" }
    Write-Output "$baseline`: $($completion[0])"
    Write-Output "Log: $log"
}
