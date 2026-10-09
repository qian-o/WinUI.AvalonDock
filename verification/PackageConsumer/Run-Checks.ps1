[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RefreshCandidate
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $PSScriptRoot 'PackageConsumer.csproj'
$package = Join-Path $repoRoot 'sources\WinUI.AvalonDock\bin\Release\WinUI.AvalonDock.0.1.0-alpha.nupkg'
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\local-package-cache'))
$candidateCache = [IO.Path]::GetFullPath((Join-Path $cacheRoot 'winui.avalondock'))
$results = Join-Path $repoRoot 'artifacts\package-consumer'
if (!(Test-Path -LiteralPath $package -PathType Leaf)) {
    throw "Build the library in Release to create the candidate package first: $package"
}
if ($RefreshCandidate -and (Test-Path -LiteralPath $candidateCache)) {
    # This cache belongs only to this consumer. Never delete a global NuGet cache.
    if ($candidateCache -ne (Join-Path $cacheRoot 'winui.avalondock') -or
        !($candidateCache.StartsWith($cacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing cache cleanup outside the consumer cache: $candidateCache"
    }
    Remove-Item -LiteralPath $candidateCache -Recurse -Force
}
New-Item -ItemType Directory -Path $results -Force | Out-Null
& dotnet restore $project --force-evaluate
if ($LASTEXITCODE -ne 0) { throw "Consumer restore failed: $LASTEXITCODE" }
& dotnet build $project -c $Configuration --no-restore -v minimal
if ($LASTEXITCODE -ne 0) { throw "Consumer build failed: $LASTEXITCODE" }

$restoredPackage = Join-Path $candidateCache '0.1.0-alpha\winui.avalondock.0.1.0-alpha.nupkg'
$packageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
$restoredHash = (Get-FileHash -LiteralPath $restoredPackage -Algorithm SHA256).Hash
if ($packageHash -ne $restoredHash) {
    throw 'The restored package differs from the current candidate. Run again with -RefreshCandidate.'
}
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
$library = $assets.libraries.'WinUI.AvalonDock/0.1.0-alpha'
if ($library.type -ne 'package') { throw 'WinUI.AvalonDock was not restored as a package.' }
if (@($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' }).Count -gt 0) {
    throw 'The consumer unexpectedly restored a project dependency.'
}
if ($assets.project.restore.packagesPath.TrimEnd('\') -ne $cacheRoot.TrimEnd('\')) {
    throw 'The consumer did not use its isolated NuGet cache.'
}
$metadata = Get-Content -LiteralPath (Join-Path $candidateCache '0.1.0-alpha\.nupkg.metadata') -Raw | ConvertFrom-Json
$localSource = [IO.Path]::GetDirectoryName($package)
if ($metadata.source -ne $localSource) { throw "Candidate came from unexpected source: $($metadata.source)" }
$executable = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\PackageConsumer.exe"
$assembly = Join-Path ([IO.Path]::GetDirectoryName($executable)) 'WinUI.AvalonDock.dll'
$assemblyAsset = @($library.files | Where-Object { $_.EndsWith('/WinUI.AvalonDock.dll', [StringComparison]::Ordinal) })
if ($assemblyAsset.Count -ne 1) { throw 'The candidate package lacks its product assembly.' }
$restoredAssembly = Join-Path (Join-Path $candidateCache '0.1.0-alpha') $assemblyAsset[0]
$assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
if ($assemblyHash -ne (Get-FileHash -LiteralPath $restoredAssembly -Algorithm SHA256).Hash) {
    throw 'The consumer output assembly differs from the restored candidate.'
}
$identityLog = Join-Path $results "$Configuration-package-identity.log"
@(
    "Package: $package"
    'Version: 0.1.0-alpha'
    "SHA256: $packageHash"
    "RestoredPackageSHA256: $restoredHash"
    "RestorePackagesPath: $($assets.project.restore.packagesPath)"
    "PackageSource: $($metadata.source)"
    "AssetType: $($library.type)"
    'ProjectReferences: 0'
    "ProductAssemblySHA256: $assemblyHash"
) | Set-Content -LiteralPath $identityLog

foreach ($check in @('package-smoke', 'boundary-smoke', 'overlay-lifetime')) {
    $log = Join-Path $results "$Configuration-$check.log"
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
    $process = Start-Process -FilePath $executable -ArgumentList "--$check", "`"$log`"" -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) {
            $process.Kill()
            throw "Consumer $check timed out. Test process $($process.Id) was stopped."
        }
    }
    if (!(Test-Path -LiteralPath $log)) {
        throw "Consumer $check exited with $($process.ExitCode) and produced no result log."
    }
    $lines = Get-Content -LiteralPath $log
    if ($process.ExitCode -ne 0 -or @($lines | Where-Object { $_ -like 'FAIL:*' }).Count -gt 0) {
        $lines | Write-Output
        throw "Consumer $check failed with exit code $($process.ExitCode)."
    }
    if ($check -eq 'overlay-lifetime') {
        if ($lines[-1] -ne 'RESULT: PASS') { throw "Incomplete overlay result log: $log" }
    }
    elseif ($lines[-1] -notmatch '^PASS: \d+ checks$') { throw "Incomplete result log: $log" }
    Write-Output "$Configuration $check`: $($lines[-1])"
}
if ($packageHash -ne (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -or
    $restoredHash -ne (Get-FileHash -LiteralPath $restoredPackage -Algorithm SHA256).Hash -or
    $assemblyHash -ne (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash) {
    throw 'The candidate, cache, or consumer assembly changed during validation. Freeze the candidate and run the checks again.'
}
Write-Output "Candidate SHA256: $packageHash"
Write-Output "Results: $results"
