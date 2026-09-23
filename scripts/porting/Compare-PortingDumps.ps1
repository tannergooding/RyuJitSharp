[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $NativeDump,
    [Parameter(Mandatory)][string] $ManagedDump,
    [Parameter(Mandatory)][string] $OutputPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-Methods([string] $Path) {
    $text = [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false, $true))
    $headers = [regex]::Matches($text, '(?m)^\*{6} START compiling (?<method>.+?) \(MethodHash=[0-9a-f]+\)\r?$')
    if ($headers.Count -eq 0) {
        throw "No compilation headers in $Path"
    }
    $methods = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
    for ($i = 0; $i -lt $headers.Count; $i++) {
        $header = $headers[$i]
        $name = $header.Groups["method"].Value
        if ($methods.Contains($name)) {
            throw "Duplicate compilation header for $name; specify a single-tier corpus."
        }
        $end = if ($i + 1 -lt $headers.Count) { $headers[$i + 1].Index } else { $text.Length }
        $methods.Add($name, $text.Substring($header.Index, $end - $header.Index))
    }
    return $methods
}

$nativeMethods = Get-Methods $NativeDump
$managedMethods = Get-Methods $ManagedDump
$names = @($nativeMethods.Keys)
if ($names.Count -ne $managedMethods.Count) {
    throw "Compilation counts differ: native $($names.Count), managed $($managedMethods.Count)."
}
$managedNames = @($managedMethods.Keys)
for ($i = 0; $i -lt $names.Count; $i++) {
    if (-not [string]::Equals($names[$i], $managedNames[$i], [StringComparison]::Ordinal)) {
        throw "Compilation order or identity differs at entry $($i + 1): '$($names[$i])' vs '$($managedNames[$i])'."
    }
}
$results = foreach ($name in $names) {
    if (-not $managedMethods.Contains($name)) {
        throw "Missing managed compilation: $name"
    }
    $native = $nativeMethods[$name]
    $managed = $managedMethods[$name]
    $boundary = "*************** Finishing PHASE Importation"
    $nativeImportEnd = $native.IndexOf($boundary, [StringComparison]::Ordinal)
    $managedImportEnd = $managed.IndexOf($boundary, [StringComparison]::Ordinal)
    if ($nativeImportEnd -lt 0 -or $managedImportEnd -lt 0) {
        throw "Missing importation completion for $name"
    }
    $nativePrefix = $native.Substring(0, $nativeImportEnd)
    $managedPrefix = $managed.Substring(0, $managedImportEnd)
    $nativeLines = $nativePrefix.Split("`n")
    $managedLines = $managedPrefix.Split("`n")
    $limit = [Math]::Min($nativeLines.Length, $managedLines.Length)
    $firstDifference = -1
    for ($line = 0; $line -lt $limit; $line++) {
        if (-not [string]::Equals($nativeLines[$line], $managedLines[$line], [StringComparison]::Ordinal)) {
            $firstDifference = $line
            break
        }
    }
    if ($firstDifference -eq -1 -and $nativeLines.Length -ne $managedLines.Length) {
        $firstDifference = $limit
    }
    [ordered]@{
        method = $name
        prefixBeforeImportationCompletionEqual = [string]::Equals($nativePrefix, $managedPrefix, [StringComparison]::Ordinal)
        nativePrefixLines = $nativeLines.Length
        managedPrefixLines = $managedLines.Length
        firstDifferentLine = if ($firstDifference -ge 0) { $firstDifference + 1 } else { $null }
        nativeLine = if ($firstDifference -ge 0 -and $firstDifference -lt $nativeLines.Length) { $nativeLines[$firstDifference] } else { $null }
        managedLine = if ($firstDifference -ge 0 -and $firstDifference -lt $managedLines.Length) { $managedLines[$firstDifference] } else { $null }
    }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath
$results | ForEach-Object { "{0}: equal={1}; first difference={2}" -f $_.method, $_.prefixBeforeImportationCompletionEqual, $_.firstDifferentLine }
