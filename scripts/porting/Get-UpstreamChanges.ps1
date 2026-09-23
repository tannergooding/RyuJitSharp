[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $RuntimeRepository,
    [Parameter(Mandatory)][string] $OutputPath,
    [string] $StatePath = (Join-Path $PSScriptRoot "..\..\docs\porting\state.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git([string] $Repository, [string[]] $Arguments) {
    $output = @(& git -C $Repository @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Git failed in '$Repository': $($Arguments -join ' ')"
    }
    return $output
}

$state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
$csharpRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$RuntimeRepository = (Resolve-Path -LiteralPath $RuntimeRepository).Path
$source = $state.upstream.sourceCommit
$target = $state.upstream.targetCommit
$residual = $state.localRecovery.nativeResidual.commit
$csharpBaseline = $state.checkpoint.csharpBaselineCommit

foreach ($commit in @($source, $target, $residual)) {
    [void](Invoke-Git $RuntimeRepository @("cat-file", "-e", "$commit^{commit}"))
}
[void](Invoke-Git $csharpRepository @("cat-file", "-e", "$csharpBaseline^{commit}"))

$residualChanges = @{}
foreach ($line in (Invoke-Git $RuntimeRepository @("diff", "--name-status", "--no-renames", $source, $residual))) {
    $parts = $line.Split("`t")
    $residualChanges[$parts[1].Replace("/", "\")] = $parts[0]
}

$csharpPaths = @(Invoke-Git $csharpRepository @("ls-tree", "-r", "--name-only", $csharpBaseline, "--", "sources/Core") |
    ForEach-Object { $_.Replace("/", "\") })
$generatorInputs = @(Get-ChildItem -LiteralPath (Join-Path $csharpRepository $state.generatorInputMapping.directory) -File)
$generatorSources = @{}
foreach ($inputFile in $generatorInputs) {
    $name = $inputFile.Name
    $exception = $state.generatorInputMapping.exceptions.PSObject.Properties[$name]
    $nativePath = if ($null -ne $exception) { $exception.Value } else { Join-Path $state.generatorInputMapping.defaultNativeDirectory $name }
    $generatorSources[$nativePath] = Join-Path $state.generatorInputMapping.directory $name
}

$changes = Invoke-Git $RuntimeRepository @(
    "diff", "--name-status", "--no-renames", $source, $target, "--",
    "src/coreclr/jit", "src/coreclr/inc", "src/coreclr/jitshared",
    "src/coreclr/tools/Common/JitInterface"
)
$rows = foreach ($line in $changes) {
    $parts = $line.Split("`t")
    $nativePath = $parts[1].Replace("/", "\")
    $destinations = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $evidence = [Collections.Generic.List[string]]::new()
    foreach ($mapping in $state.sourceMap) {
        if ($mapping.native -contains $nativePath) {
            foreach ($destination in $mapping.csharp) {
                [void]($destinations.Add($destination))
            }
            $evidence.Add("source-map:$($mapping.area)")
        }
    }
    if ($generatorSources.ContainsKey($nativePath)) {
        [void]($destinations.Add($generatorSources[$nativePath]))
        $evidence.Add("generator-input")
    }
    if ($nativePath -match '^src\\coreclr\\(jit|inc|jitshared)\\([^\\]+)\.(cpp|h|hpp)$') {
        $candidate = "sources\Core\$($Matches[1])\$($Matches[2])\"
        if ($csharpPaths.Where({ $_.StartsWith($candidate, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) {
            [void]($destinations.Add($candidate.TrimEnd("\")))
            $evidence.Add("directory-convention")
        }
    }
    [pscustomobject][ordered]@{
        NativePath = $nativePath
        UpstreamStatus = $parts[0]
        ResidualStatus = if ($residualChanges.ContainsKey($nativePath)) { $residualChanges[$nativePath] } else { "no-recorded-edit" }
        CSharpDestinations = ($destinations | Sort-Object) -join "; "
        MappingEvidence = $evidence -join "; "
        Disposition = "pending-review"
    }
}

if (@($rows).Count -eq 0) {
    throw "The pinned upstream comparison selected no files."
}
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation
Write-Output "Wrote $(@($rows).Count) changed-file candidates to $OutputPath. Mapping hints are not completion or exclusion decisions."
