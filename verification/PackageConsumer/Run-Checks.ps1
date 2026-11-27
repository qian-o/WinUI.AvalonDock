[CmdletBinding()]
param([ValidateSet('Debug', 'Release')] [string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$consumer = Join-Path $PSScriptRoot 'PackageConsumer.csproj'
$library = Join-Path $root 'sources\WinUI.AvalonDock\WinUI.AvalonDock.csproj'
$version = ([xml](Get-Content -Raw -LiteralPath $consumer)).SelectSingleNode("//PackageReference[@Include='WinUI.AvalonDock']").GetAttribute('Version')
& dotnet build $library -c Release --nologo -v quiet
if ($LASTEXITCODE) { throw 'Release package build failed.' }

$package = Join-Path $root "sources\WinUI.AvalonDock\bin\Release\WinUI.AvalonDock.$version.nupkg"
$hash = (Get-FileHash -LiteralPath $package).Hash
$cacheRoot = Join-Path $root 'artifacts\local-package-cache\winui.avalondock'
$cached = [IO.Path]::GetFullPath((Join-Path $cacheRoot $version))
if ([IO.Path]::GetDirectoryName($cached) -ne $cacheRoot) { throw 'Invalid package cache path.' }
$cachedPackage = Join-Path $cached "winui.avalondock.$version.nupkg"
if ((Test-Path -LiteralPath $cached) -and
    (!(Test-Path -LiteralPath $cachedPackage) -or (Get-FileHash -LiteralPath $cachedPackage).Hash -ne $hash)) {
    Remove-Item -LiteralPath $cached -Recurse -Force
}

& dotnet build $consumer -c $Configuration --nologo -v quiet
if ($LASTEXITCODE) { throw 'Package consumer build failed.' }
if ((Get-FileHash -LiteralPath $cachedPackage).Hash -ne $hash) { throw 'The restored package differs from the candidate.' }
$assembly = & dotnet msbuild $consumer -nologo "-p:Configuration=$Configuration" -getProperty:TargetPath
if ($LASTEXITCODE) { throw 'Could not locate the package consumer.' }
$assets = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'obj\project.assets.json') | ConvertFrom-Json
$product = $assets.libraries."WinUI.AvalonDock/$version"
$dll = @($product.files | Where-Object { $_ -like 'lib/*/WinUI.AvalonDock.dll' })
$loaded = Join-Path (Split-Path $assembly) 'WinUI.AvalonDock.dll'
if ($product.type -ne 'package' -or $dll.Count -ne 1 -or (Get-FileHash -LiteralPath $loaded).Hash -ne (Get-FileHash -LiteralPath (Join-Path $cached $dll[0])).Hash) {
    throw 'The test output does not contain the packaged assembly.'
}

$log = Join-Path $root "artifacts\package-consumer-$Configuration.log"
if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
$process = Start-Process -FilePath ([IO.Path]::ChangeExtension($assembly, '.exe')) -ArgumentList ('"' + $log + '"') -PassThru
if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Package check timed out.' }
if (!(Test-Path -LiteralPath $log)) { throw "Package consumer exited without a result ($($process.ExitCode))." }
$result = Get-Content -Raw -LiteralPath $log
if ($process.ExitCode -ne 0 -or $result -ne 'PASS') { throw "Package check failed ($($process.ExitCode)): $result" }
Write-Output "$Configuration package check passed ($log)"
