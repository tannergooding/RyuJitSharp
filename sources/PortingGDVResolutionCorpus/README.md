# GDV resolution corpus

`InterfaceCaller` and `VirtualCaller` receive an interface/base-typed value
from aggressively inlineable factories and call `DerivedWorker.Compute`.
Under native Tier1 dynamic PGO, each caller imports a guarded-devirtualization
candidate, inlines the factory, and resolves a method-table `NE` guard using
the updated exact local type. `UnknownReceiver` can have a candidate without
an updated exact local and is the no-resolution control. `Main` checks each
result over three rounds of 30,000 calls, pauses between rounds to allow
Tier1 promotion, then checks the results again after the last pause. It prints
`GDV corpus: interface and virtual results verified` on success.

Build from the repository root and capture the default optimized native
control with the maintained runner. Always use a fresh output directory:

```powershell
dotnet build sources\PortingGDVResolutionCorpus\PortingGDVResolutionCorpus.csproj -c Release -o artifacts\gdv-resolution\corpus
$coreRoot = '<Core_Root built from the pinned native commit>'
$corpus = 'artifacts\gdv-resolution\corpus\PortingGDVResolutionCorpus.dll'
$methods = @('InterfaceCaller', 'VirtualCaller', 'UnknownReceiver')
& scripts\porting\Invoke-PortingCorpus.ps1 `
    -CoreRoot $coreRoot -Corpus $corpus `
    -OutputDirectory 'artifacts\gdv-resolution\default-native-next' `
    -NativeCommit '33baf8ee337b20dd0f184b69a6f09be92850bf9e' `
    -TypeName 'RyuJitSharp.GDVCases' -ExpectedMethods $methods
```

For a published managed JIT, use another fresh output directory and add
`-ManagedJit <published DLL> -ManagedSource <immutable 40-hex commit>
-ExecuteManagedCode` to the same maintained-runner command. The default
control selects exactly three methods; it is *not* positive resolver
coverage.

The maintained runner deliberately disables tiering. To capture the positive
native Tier1 case without the artifact-only helper, run this self-contained
PowerShell block from the repository root after setting `$coreRoot` and
`$corpus` above. Set `$managedJit` to a published DLL and choose a different
fresh `$output` to attempt managed execution:

```powershell
$managedJit = $null # Or the full path to a published RyuJitSharp.dll.
$output = 'artifacts\gdv-resolution\tiered-native-next'
if (Test-Path -LiteralPath $output) { throw "Use a fresh output: $output" }
New-Item -ItemType Directory -Path $output -ErrorAction Stop | Out-Null
$output = (Resolve-Path $output).Path
$start = [Diagnostics.ProcessStartInfo]::new((Join-Path $coreRoot 'corerun.exe'))
$start.UseShellExecute = $false
$start.WorkingDirectory = (Resolve-Path $coreRoot).Path
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.ArgumentList.Add((Resolve-Path $corpus).Path)
foreach ($key in @($start.Environment.Keys)) {
    if ($key.StartsWith('DOTNET_', [StringComparison]::OrdinalIgnoreCase) -or
        $key.StartsWith('COMPlus_', [StringComparison]::OrdinalIgnoreCase)) {
        [void]$start.Environment.Remove($key)
    }
}
$start.Environment['DOTNET_ReadyToRun'] = '0'
$start.Environment['DOTNET_TieredCompilation'] = '1'
$start.Environment['DOTNET_TieredPGO'] = '1'
$start.Environment['DOTNET_TC_QuickJitForLoops'] = '1'
$start.Environment['DOTNET_TC_CallCountingDelayMs'] = '0'
$start.Environment['DOTNET_JitDump'] = 'RyuJitSharp.GDVCases:*'
$start.Environment['DOTNET_JitDisasmDiffable'] = '1'
$start.Environment['DOTNET_JitDumpASCII'] = '1'
$start.Environment['DOTNET_JitStdOutFile'] = Join-Path $output 'jitdump.txt'
if ($managedJit) {
    $start.Environment['DOTNET_AltJit'] = 'RyuJitSharp.GDVCases:*'
    $start.Environment['DOTNET_AltJitName'] = [IO.Path]::GetFileName($managedJit)
    $start.Environment['DOTNET_AltJitPath'] = (Resolve-Path $managedJit).Path
    $start.Environment['DOTNET_RunAltJitCode'] = '1'
    $start.Environment['DOTNET_AltJitAssertOnNYI'] = '0'
}
$process = [Diagnostics.Process]::new() # Dispose after reading the exit code.
$process.StartInfo = $start
try {
    if (-not $process.Start()) { throw 'corerun did not start' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(300000)) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
        throw 'corerun timed out'
    }
    $out = $stdout.GetAwaiter().GetResult()
    $err = $stderr.GetAwaiter().GetResult()
    $out | Set-Content (Join-Path $output 'stdout.txt')
    $err | Set-Content (Join-Path $output 'stderr.txt')
    if ($process.ExitCode -ne 0 -or
        $out.Trim() -cne 'GDV corpus: interface and virtual results verified' -or
        $err.Trim()) {
        throw "Corpus failed: exit code $($process.ExitCode); see $output"
    }
}
finally {
    $process.Dispose()
}
```

Inspect the optimized Tier1 compilations of `InterfaceCaller`, `VirtualCaller`,
and `UnknownReceiver`, not their Tier0 or instrumented Tier0 versions.
The native positive phase reports `GDV in BB01 can be resolved` in the first
two Tier1 callers, changes BB01 from conditional BB04/BB03 to unconditional
BB03, and retains the side-effecting `NE` tree. `UnknownReceiver` marks a
candidate but does not resolve it. Matching code size alone is insufficient:
the default optimized callers also emit four bytes without this phase change.

Managed tiered execution currently fails with `0xC0000374` after initial
Tier0 compilations, before instrumented Tier0 or Tier1. Matching Tier0 code,
passing default execution and focused source tests do not establish positive
managed resolver parity. Exact captures and coverage are recorded in
[the porting checkpoint](..\..\docs\porting\state.json).
