[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $NativeDump,
    [Parameter(Mandatory)][string] $ManagedDump,
    [Parameter(Mandatory)][string] $OutputPath,
    [ValidateSet("Importation", "Redundant branch opts", "FullDump")][string] $Phase = "Importation",
    [switch] $CanonicalizeProcessSpecificHandles
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

function Get-ComparisonRegions([string] $Text, [string] $Method, [string] $Phase) {
    $regions = [Collections.Generic.List[string]]::new()
    if ($Phase -eq "FullDump") {
        $regions.Add($Text)
        return $regions.ToArray()
    }

    if ($Phase -eq "Importation") {
        $boundary = "*************** Finishing PHASE Importation"
        $end = $Text.IndexOf($boundary, [StringComparison]::Ordinal)
        if ($end -lt 0) {
            throw "Missing importation completion for $Method"
        }
        $regions.Add($Text.Substring(0, $end))
        return $regions.ToArray()
    }

    $startMarker = "*************** Starting PHASE $Phase"
    $endMarker = "*************** Finishing PHASE $Phase"
    $offset = 0
    while ($offset -lt $Text.Length) {
        $start = $Text.IndexOf($startMarker, $offset, [StringComparison]::Ordinal)
        if ($start -lt 0) {
            break
        }

        $end = $Text.IndexOf($endMarker, $start, [StringComparison]::Ordinal)
        if ($end -lt 0) {
            throw "Missing completion for phase '$Phase' in $Method"
        }

        $end += $endMarker.Length
        $regions.Add($Text.Substring($start, $end - $start))
        $offset = $end
    }

    if ($regions.Count -eq 0) {
        throw "No phase '$Phase' in $Method"
    }

    return $regions.ToArray()
}

function Normalize-ProcessSpecificHandles([string] $Text) {
    $mapping = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    $handlePattern = [regex]::new(
        '(?<prefix>(?:method|class|context) )(?<address>[0-9A-Fa-f]{8}(?:[0-9A-Fa-f]{8})?)(?![0-9A-Fa-f])|' +
        '(?<prefix>exactContextHandle=0x)(?<address>[0-9A-Fa-f]{8}(?:[0-9A-Fa-f]{8})?)(?![0-9A-Fa-f])')
    $lines = $Text.Split("`n")
    for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
        $line = $lines[$lineIndex]
        if ($line.Contains("ResolveVirtualMethod (") -or $line.Contains("CheckCanInline:") -or
            $line.Contains("exactContextHandle=0x")) {
            $matches = $handlePattern.Matches($line)
            if ($matches.Count -gt 0) {
                $builder = [Text.StringBuilder]::new($line.Length)
                $offset = 0
                foreach ($match in $matches) {
                    [void]$builder.Append($line, $offset, $match.Index - $offset)
                    $address = $match.Groups["address"].Value
                    if (-not $mapping.ContainsKey($address)) {
                        $mapping.Add($address, "HANDLE{0:D4}" -f ($mapping.Count + 1))
                    }
                    [void]$builder.Append($match.Groups["prefix"].Value)
                    [void]$builder.Append($mapping[$address])
                    $offset = $match.Index + $match.Length
                }
                [void]$builder.Append($line, $offset, $line.Length - $offset)
                $lines[$lineIndex] = $builder.ToString()
            }
        }
    }

    # Keep handle-identity relationships while removing only process-local addresses in these diagnostics.
    return [string]::Join("`n", $lines)
}

function Get-FirstDifference([string] $Native, [string] $Managed) {
    $nativeLines = $Native.Split("`n")
    $managedLines = $Managed.Split("`n")
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

    return [ordered]@{
        line = if ($firstDifference -ge 0) { $firstDifference + 1 } else { $null }
        nativeLine = if ($firstDifference -ge 0 -and $firstDifference -lt $nativeLines.Length) { $nativeLines[$firstDifference] } else { $null }
        managedLine = if ($firstDifference -ge 0 -and $firstDifference -lt $managedLines.Length) { $managedLines[$firstDifference] } else { $null }
    }
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
    $nativeRegions = @(Get-ComparisonRegions $nativeMethods[$name] $name $Phase)
    $managedRegions = @(Get-ComparisonRegions $managedMethods[$name] $name $Phase)
    $nativeText = [string]::Join("`n", $nativeRegions)
    $managedText = [string]::Join("`n", $managedRegions)
    $rawEqual = [string]::Equals($nativeText, $managedText, [StringComparison]::Ordinal)
    $rawDifference = Get-FirstDifference $nativeText $managedText
    if ($CanonicalizeProcessSpecificHandles) {
        $nativeText = Normalize-ProcessSpecificHandles $nativeText
        $managedText = Normalize-ProcessSpecificHandles $managedText
    }
    $difference = Get-FirstDifference $nativeText $managedText
    $equal = ($nativeRegions.Count -eq $managedRegions.Count) -and
        [string]::Equals($nativeText, $managedText, [StringComparison]::Ordinal)
    $result = [ordered]@{
        method = $name
        phase = $Phase
        phaseOccurrenceCountNative = $nativeRegions.Count
        phaseOccurrenceCountManaged = $managedRegions.Count
        equal = $equal
        rawEqual = $rawEqual
        processSpecificHandlesCanonicalized = [bool]$CanonicalizeProcessSpecificHandles
        nativeLines = $nativeText.Split("`n").Length
        managedLines = $managedText.Split("`n").Length
        firstDifferentLine = $difference.line
        nativeLine = $difference.nativeLine
        managedLine = $difference.managedLine
        firstRawDifferentLine = $rawDifference.line
        rawNativeLine = $rawDifference.nativeLine
        rawManagedLine = $rawDifference.managedLine
    }
    if ($Phase -eq "Importation") {
        $result["prefixBeforeImportationCompletionEqual"] = $equal
        $result["nativePrefixLines"] = $nativeText.Split("`n").Length
        $result["managedPrefixLines"] = $managedText.Split("`n").Length
    }
    $result
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath
$results | ForEach-Object { "{0}: equal={1}; first difference={2}" -f $_.method, $_.equal, $_.firstDifferentLine }
