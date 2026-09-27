[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CoreRoot,
    [Parameter(Mandatory)][string] $Corpus,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string] $NativeCommit,
    [string] $ManagedJit = "",
    [string] $ManagedSource = "",
    [switch] $MinOpts,
    [switch] $InstrumentedTier0,
    [switch] $BlockCounters,
    [switch] $DisableObjectStackAllocation,
    [string] $TypeName = "RyuJitSharp.PortingCorpus",
    [string[]] $ExpectedMethods = @("Main", "Add", "Branch", "Locals", "Call", "InlineCaller", "IndirectCall", "FoldConstants", "FoldFloating", "FoldInteger", "FoldHardware",
        "SynchronizedReturn", "GenericCatch", "PInvokeCall", "ReversePInvoke", "ManyReturns", "LocalAddressStore", "LocalAddressDifference", "ImplicitByRefArgument"),
    [ValidateRange(1, 3600)][int] $TimeoutSeconds = 120,
    [switch] $ExecuteManagedCode,
    [ValidateRange(0, 2147483647)][int] $OptimizationRepeatCount = 0,
    [ValidateSet("None", "Dot", "Xml")][string] $FlowGraphFormat = "None",
    [ValidateNotNullOrEmpty()][string] $FlowGraphPhase = "DETERMINE_FIRST_COLD_BLOCK",
    [switch] $FlowGraphEH,
    [switch] $FlowGraphLoops,
    [switch] $FlowGraphMemorySsa,
    [switch] $ReportMetrics,
    [switch] $DumpOrder,
    [ValidateRange(0, 2)][int] $InlineDumpData = 0,
    [ValidateRange(0, 3)][int] $InlineDumpXml = 0,
    [switch] $InlineXmlFile,
    [ValidateSet("Default", "Discretionary", "Model", "Profile", "Random", "Full", "Size", "Replay")]
    [string] $InlinePolicy = "Default",
    [string] $InlineReplayFile = "",
    [switch] $TimingCsv,
    [switch] $TimingSummary,
    [switch] $LoopHoistStats,
    [switch] $EnregistrationStats,
    [switch] $RawHexCode,
    [switch] $GcStress,
    [switch] $FakeProcedureSplitting,
    [switch] $StressProcedureSplitting,
    [switch] $DisableProcedureSplittingEH
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($ExecuteManagedCode -and -not $ManagedJit) {
    throw "-ExecuteManagedCode requires -ManagedJit."
}
if ($InstrumentedTier0 -and $MinOpts) {
    throw "-InstrumentedTier0 selects the runtime's instrumented tier; do not combine it with forced -MinOpts."
}
if ($BlockCounters -and -not $InstrumentedTier0) {
    throw "-BlockCounters requires -InstrumentedTier0."
}
if (($OptimizationRepeatCount -gt 0) -and ($MinOpts -or $InstrumentedTier0)) {
    throw "-OptimizationRepeatCount requires optimized compilation; do not combine it with -MinOpts or -InstrumentedTier0."
}
if (($FlowGraphEH -or $FlowGraphLoops -or $FlowGraphMemorySsa -or $PSBoundParameters.ContainsKey("FlowGraphPhase")) -and ($FlowGraphFormat -eq "None")) {
    throw "Flow graph options require -FlowGraphFormat Dot or Xml."
}
if (($FlowGraphEH -or $FlowGraphLoops -or $FlowGraphMemorySsa) -and ($FlowGraphFormat -ne "Dot")) {
    throw "EH, loop, and memory SSA graph annotations require -FlowGraphFormat Dot."
}
if ($InlineXmlFile -and ($InlineDumpXml -eq 0)) {
    throw "-InlineXmlFile requires -InlineDumpXml."
}
if (($InlinePolicy -eq "Replay") -ne (-not [string]::IsNullOrEmpty($InlineReplayFile))) {
    throw "-InlinePolicy Replay requires -InlineReplayFile, which is only valid for replay."
}

if (($MinOpts -or $InstrumentedTier0) -and -not $PSBoundParameters.ContainsKey("ExpectedMethods")) {
    $ExpectedMethods += "InlineCandidate"
}

$coreRun = Join-Path $CoreRoot "corerun.exe"
$nativeInputs = @($coreRun, (Join-Path $CoreRoot "coreclr.dll"), (Join-Path $CoreRoot "clrjit.dll"),
    (Join-Path $CoreRoot "System.Private.CoreLib.dll"), $Corpus)
if ($InlineReplayFile) {
    $InlineReplayFile = (Resolve-Path -LiteralPath $InlineReplayFile).Path
    $nativeInputs += $InlineReplayFile
}
foreach ($path in $nativeInputs) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required input does not exist: $path"
    }
}
if ($ManagedJit) {
    if (-not (Test-Path -LiteralPath $ManagedJit -PathType Leaf)) {
        throw "Managed JIT does not exist: $ManagedJit"
    }
    if ([string]::IsNullOrWhiteSpace($ManagedSource)) {
        throw "Describe the managed source revision and any uncommitted source snapshot with -ManagedSource."
    }
}
if ([string]::IsNullOrWhiteSpace($TypeName) -or $ExpectedMethods.Count -eq 0) {
    throw "A concrete type and nonempty expected method set are required."
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Use a new output directory to prevent mixing runs: $OutputDirectory"
}
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dumpPath = Join-Path $OutputDirectory "jitdump.txt"
$selector = "${TypeName}:*"

$start = [Diagnostics.ProcessStartInfo]::new((Resolve-Path -LiteralPath $coreRun).Path)
$start.UseShellExecute = $false
$start.WorkingDirectory = (Resolve-Path -LiteralPath $CoreRoot).Path
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.ArgumentList.Add((Resolve-Path -LiteralPath $Corpus).Path)
foreach ($name in @($start.Environment.Keys)) {
    if ($name.StartsWith("DOTNET_", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("COMPlus_", [StringComparison]::OrdinalIgnoreCase)) {
        [void]($start.Environment.Remove($name))
    }
}
$settings = [ordered]@{
    DOTNET_ReadyToRun = "0"
    DOTNET_TieredCompilation = if ($InstrumentedTier0) { "1" } else { "0" }
    DOTNET_JitDump = $selector
    DOTNET_JitDisasmDiffable = "1"
    DOTNET_JitDumpASCII = "1"
    DOTNET_JitStdOutFile = $dumpPath
}
if ($InlinePolicy -ne "Default") {
    $settings["DOTNET_JitInlinePolicy$InlinePolicy"] = "1"
}
if ($InlineReplayFile) {
    $settings.DOTNET_JitInlineReplayFile = $InlineReplayFile
}
if ($InstrumentedTier0) {
    $settings.DOTNET_TieredPGO = "1"
    $settings.DOTNET_TieredPGO_InstrumentOnlyHotCode = "0"
    $settings.DOTNET_TC_CallCounting = "0"
    $settings.DOTNET_TC_QuickJitForLoops = "1"
    if ($BlockCounters) {
        $settings.DOTNET_JitEdgeProfiling = "0"
    }
}
if ($MinOpts) {
    $settings.DOTNET_JitMinOpts = "1"
}
if ($OptimizationRepeatCount -gt 0) {
    $settings.DOTNET_JitOptRepeat = $selector
    $settings.DOTNET_JitOptRepeatCount = $OptimizationRepeatCount.ToString("X", [Globalization.CultureInfo]::InvariantCulture)
}
if ($FlowGraphFormat -ne "None") {
    $settings.DOTNET_JitDumpFg = $selector
    $settings.DOTNET_JitDumpFgTier0 = if ($InstrumentedTier0) { "1" } else { "0" }
    $settings.DOTNET_JitDumpFgFile = "graphs"
    $settings.DOTNET_JitDumpFgDir = $OutputDirectory
    $settings.DOTNET_JitDumpFgPhase = $FlowGraphPhase
    $settings.DOTNET_JitDumpFgDot = if ($FlowGraphFormat -eq "Dot") { "1" } else { "0" }
    $settings.DOTNET_JitDumpFgEH = if ($FlowGraphEH) { "1" } else { "0" }
    $settings.DOTNET_JitDumpFgLoops = if ($FlowGraphLoops) { "1" } else { "0" }
    $settings.DOTNET_JitDumpFgMemorySsa = if ($FlowGraphMemorySsa) { "1" } else { "0" }
}
if ($DisableObjectStackAllocation) {
    $settings.DOTNET_JitObjectStackAllocation = "0"
}
if ($ReportMetrics) {
    $settings.DOTNET_JitReportMetrics = "1"
}
if ($DumpOrder) {
    $settings.DOTNET_JitOrder = "1"
    $settings.DOTNET_JitDisasmAssemblies = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $Corpus).Path).Name
}
if ($InlineDumpData -ne 0) {
    $settings.DOTNET_JitInlineDumpData = $InlineDumpData.ToString()
}
if ($InlineDumpXml -ne 0) {
    $settings.DOTNET_JitInlineDumpXml = $InlineDumpXml.ToString()
}
if ($InlineXmlFile) {
    $settings.DOTNET_JitInlineDumpXmlFile = Join-Path $OutputDirectory "inlines.xml"
}
if ($TimingCsv) {
    $settings.DOTNET_JitTimeLogCsv = Join-Path $OutputDirectory "timing.csv"
}
if ($TimingSummary) {
    $settings.DOTNET_JitTimeLogFile = Join-Path $OutputDirectory "timing.txt"
}
if ($LoopHoistStats) {
    $settings.DOTNET_JitLoopHoistStats = "1"
}
if ($EnregistrationStats) {
    $settings.DOTNET_JitEnregStats = "1"
}
if ($RawHexCode) {
    $settings.DOTNET_JitRawHexCode = $selector
}
if ($GcStress) {
    $settings.DOTNET_GCStress = "4"
}
if ($FakeProcedureSplitting) {
    $settings.DOTNET_JitFakeProcedureSplitting = "1"
}
if ($StressProcedureSplitting) {
    $settings.DOTNET_JitStressProcedureSplitting = "1"
}
if ($DisableProcedureSplittingEH) {
    $settings.DOTNET_JitNoProcedureSplittingEH = $selector
}
if ($ManagedJit) {
    $settings.DOTNET_AltJit = $selector
    $settings.DOTNET_AltJitName = [IO.Path]::GetFileName($ManagedJit)
    $settings.DOTNET_AltJitPath = (Resolve-Path -LiteralPath $ManagedJit).Path
    $settings.DOTNET_RunAltJitCode = if ($ExecuteManagedCode) { "1" } else { "0" }
    $settings.DOTNET_AltJitAssertOnNYI = "0"
}
foreach ($setting in $settings.GetEnumerator()) {
    $start.Environment[$setting.Key] = $setting.Value
}

$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    if (-not $process.Start()) {
        throw "Could not start corerun."
    }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $OutputDirectory "stdout.txt")
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $OutputDirectory "stderr.txt")
    $exitCode = $process.ExitCode
}
finally {
    $process.Dispose()
}

$hashes = [ordered]@{}
foreach ($path in $nativeInputs) {
    $hashes[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
if ($ManagedJit) {
    $hashes[$ManagedJit] = (Get-FileHash -LiteralPath $ManagedJit -Algorithm SHA256).Hash
}
$allMethodHeaders = @()
if (Test-Path -LiteralPath $dumpPath -PathType Leaf) {
    $allMethodHeaders = @(Select-String -LiteralPath $dumpPath -Pattern '^\*{6} START compiling ' -CaseSensitive |
        ForEach-Object { $_.Line })
}
$typePattern = '^\*{6} START compiling ' + [regex]::Escape($TypeName) + ':'
$methodHeaders = @($allMethodHeaders | Where-Object { $_ -cmatch $typePattern })
$unexpectedMethodHeaders = @($allMethodHeaders | Where-Object { $_ -cnotmatch $typePattern })
[ordered]@{
    timestamp = [DateTimeOffset]::Now.ToString("o")
    nativeCommit = $NativeCommit
    managedSource = $ManagedSource
    coreRoot = $start.WorkingDirectory
    corpus = $start.ArgumentList[0]
    managedJit = $ManagedJit
    expectedMethods = $ExpectedMethods
    environment = $settings
    binaryHashes = $hashes
    exitCode = $exitCode
    timedOut = $timedOut
    methodHeaders = $methodHeaders
    unexpectedMethodHeaders = $unexpectedMethodHeaders
    runnerHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    nativeFallbackExpected = [bool]$ManagedJit -and -not $ExecuteManagedCode
    managedExecutionRequested = [bool]$ExecuteManagedCode
    codegenParityEstablished = $false
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory "manifest.json")

if ($timedOut) {
    throw "Corpus timed out; see $OutputDirectory"
}
if ($exitCode -ne 0) {
    throw "Corpus exited with $exitCode; see $OutputDirectory"
}
if ($unexpectedMethodHeaders.Count -ne 0) {
    throw "Captured $($unexpectedMethodHeaders.Count) unselected compilation headers; see $OutputDirectory"
}
if ($methodHeaders.Count -ne $ExpectedMethods.Count) {
    throw "Expected $($ExpectedMethods.Count) selected compilation headers, got $($methodHeaders.Count); see $OutputDirectory"
}
$expectedCounts = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
foreach ($method in $ExpectedMethods) {
    if (-not $expectedCounts.TryAdd($method, 1)) {
        $expectedCounts[$method]++
    }
}
foreach ($entry in $expectedCounts.GetEnumerator()) {
    $method = $entry.Key
    $expectedCount = $entry.Value
    $pattern = $typePattern + [regex]::Escape($method) + '(?:\[[^\r\n]*\])?\('
    if (@($methodHeaders | Where-Object { $_ -cmatch $pattern }).Count -ne $expectedCount) {
        throw "Expected exactly $expectedCount compilation dump(s) for $method; see $OutputDirectory"
    }
}
Write-Output "Captured $($methodHeaders.Count) selected compilation headers in $dumpPath"
