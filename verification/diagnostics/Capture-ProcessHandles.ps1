[CmdletBinding()]
param(
    [Parameter(Mandatory)] [int]$TargetProcessId,
    [Parameter(Mandatory)] [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if (!('AvalonDock.Verification.Diagnostics.NativeHandleSnapshot' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'NativeHandleSnapshot.cs')
}
$capturedUtc = [DateTime]::UtcNow.ToString('o')
$entries = [AvalonDock.Verification.Diagnostics.NativeHandleSnapshot]::Capture($TargetProcessId)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$entries | Select-Object @{Name='ProcessId';Expression={$TargetProcessId}}, @{Name='Utc';Expression={$capturedUtc}}, Handle, Type, Name, Flags, ManualReset, Signaled | Export-Csv -LiteralPath $outputFile -NoTypeInformation
$entries | Group-Object Type | Sort-Object Count -Descending | Select-Object Name, Count
Write-Output "Captured $($entries.Count) handles to $outputFile"
