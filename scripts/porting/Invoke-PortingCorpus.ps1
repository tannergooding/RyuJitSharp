[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CoreRoot,
    [Parameter(Mandatory)][string] $Corpus,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string] $NativeCommit,
    [string] $ManagedJit = "",
    [string] $ManagedSource = "",
    [switch] $MinOpts,
    [switch] $DisableObjectStackAllocation,
    [string] $TypeName = "RyuJitSharp.PortingCorpus",
    [string[]] $ExpectedMethods = @("Main", "Add", "Branch", "Locals", "Call", "InlineCaller", "IndirectCall", "FoldConstants", "FoldFloating", "FoldInteger", "FoldHardware",
        "SynchronizedReturn", "GenericCatch", "PInvokeCall", "ReversePInvoke", "ManyReturns", "LocalAddressStore", "LocalAddressDifference", "ImplicitByRefArgument"),
    [ValidateRange(1, 3600)][int] $TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($MinOpts -and -not $PSBoundParameters.ContainsKey("ExpectedMethods")) {
    $ExpectedMethods += "InlineCandidate"
}

$coreRun = Join-Path $CoreRoot "corerun.exe"
$nativeInputs = @($coreRun, (Join-Path $CoreRoot "coreclr.dll"), (Join-Path $CoreRoot "clrjit.dll"),
    (Join-Path $CoreRoot "System.Private.CoreLib.dll"), $Corpus)
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
    DOTNET_TieredCompilation = "0"
    DOTNET_JitDump = $selector
    DOTNET_JitDisasmDiffable = "1"
    DOTNET_JitDumpASCII = "1"
    DOTNET_JitStdOutFile = $dumpPath
}
if ($MinOpts) {
    $settings.DOTNET_JitMinOpts = "1"
}
if ($DisableObjectStackAllocation) {
    $settings.DOTNET_JitObjectStackAllocation = "0"
}
if ($ManagedJit) {
    $settings.DOTNET_AltJit = $selector
    $settings.DOTNET_AltJitName = [IO.Path]::GetFileName($ManagedJit)
    $settings.DOTNET_AltJitPath = (Resolve-Path -LiteralPath $ManagedJit).Path
    $settings.DOTNET_RunAltJitCode = "0"
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
    nativeFallbackExpected = [bool]$ManagedJit
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
foreach ($method in $ExpectedMethods) {
    $pattern = $typePattern + [regex]::Escape($method) + '(?:\[[^\r\n]*\])?\('
    if (@($methodHeaders | Where-Object { $_ -cmatch $pattern }).Count -ne 1) {
        throw "Expected exactly one compilation dump for $method; see $OutputDirectory"
    }
}
Write-Output "Captured $($methodHeaders.Count) selected compilation headers in $dumpPath"
