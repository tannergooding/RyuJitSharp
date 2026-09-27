# Mask-local conversion corpus

On an AVX-512F host, `MaskRoundTrip` stores the result of
`Avx512F.CompareEqual` in a local and uses it as a mask in both `Compress`
and `Expand`. The pinned native `fgOptimizeMaskConversions` selects its
V01 local for `TYP_MASK` storage, removing one mask-to-vector store and two
vector-to-mask uses. `MaskWithVectorUse` also reads the local as a vector,
invalidating the conversion; `VectorMaskInput` starts with a vector rather
than a comparison mask, tying the conversion costs. `Main` checks both
matching and nonmatching inputs for all three methods and prints a success
line. It checks `Avx512F.IsSupported` before invoking any intrinsics, and
reports AVX-512F and AVX10v1 support.
An unsupported host executes no intrinsic body, so it is not positive evidence.

Build and capture on the pinned Windows x64 Checked CoreRoot. The native
reference is commit `33baf8ee337b20dd0f184b69a6f09be92850bf9e`, with
`clrjit.dll` SHA-256
`0623912071874F1A12CC35008D60BE4D9F8AF663ED386DC1D005E988994EC392`.
Use a fresh output directory for each run. Set `$managedJit` and
`$managedSource` to a published DLL and its immutable 40-hex source commit
to run managed instead; leave both empty for native. The example published
managed DLL from snapshot `2bca28d58aa3b71d7e857b43195f0e849d683599`
has SHA-256
`B7CF7385C570DE2FF2B94C5B2AE978364C51739A40F401CFF9EAD179F1E65B8F`.

```powershell
dotnet build sources\PortingMaskConversionCorpus\PortingMaskConversionCorpus.csproj `
    -c Release -o artifacts\mask-conversion\corpus
if ($LASTEXITCODE -ne 0) { throw 'Corpus build failed' }

$coreRoot = '<Checked Windows x64 Core_Root for the pinned native commit>'
$managedJit = ''
$managedSource = ''
$output = 'artifacts\mask-conversion\native-next'
$corpus = (Resolve-Path 'artifacts\mask-conversion\corpus\PortingMaskConversionCorpus.dll').Path
$coreRun = (Resolve-Path (Join-Path $coreRoot 'corerun.exe')).Path
if ((Get-FileHash (Join-Path $coreRoot 'clrjit.dll') -Algorithm SHA256).Hash -ne
    '0623912071874F1A12CC35008D60BE4D9F8AF663ED386DC1D005E988994EC392') {
    throw 'CoreRoot is not the recorded pinned native build'
}
if ([bool]$managedJit -ne [bool]$managedSource -or
    ($managedSource -and $managedSource -notmatch '^[0-9a-fA-F]{40}$')) {
    throw 'A managed JIT requires an immutable 40-hex source commit'
}
if ($managedSource -eq '2bca28d58aa3b71d7e857b43195f0e849d683599' -and
    (Get-FileHash $managedJit -Algorithm SHA256).Hash -ne
    'B7CF7385C570DE2FF2B94C5B2AE978364C51739A40F401CFF9EAD179F1E65B8F') {
    throw 'Managed JIT does not match the recorded source snapshot'
}
if (Test-Path $output) { throw 'Use a fresh output directory' }
$output = (New-Item -ItemType Directory -Path $output).FullName

$controls = @{
    CoreRoot = $coreRoot
    Corpus = $corpus
    OutputDirectory = (Join-Path $output 'controls')
    NativeCommit = '33baf8ee337b20dd0f184b69a6f09be92850bf9e'
    TypeName = 'RyuJitSharp.MaskConversionControls'
    ExpectedMethods = @('MaskWithVectorUse', 'VectorMaskInput')
}
if ($managedJit) {
    $controls.ManagedJit = (Resolve-Path $managedJit).Path
    $controls.ManagedSource = $managedSource
    $controls.ExecuteManagedCode = $true
}
& scripts\porting\Invoke-PortingCorpus.ps1 @controls
$controlOutput = Get-Content (Join-Path $output 'controls\stdout.txt') -Raw
$controlDump = Get-Content (Join-Path $output 'controls\jitdump.txt') -Raw
if ($controlOutput -notmatch
        'Mask conversion corpus: round-trip, vector-use, and vector-input results verified' -or
    $controlDump -notmatch 'Weighting: Invalid\{200\.00c 0\.00s\}' -or
    $controlDump -notmatch 'Weighting: \{100\.00c 100\.00s\}' -or
    $controlDump -match 'Updated (?:store|use) V01 at .*to mask' -or
    ($managedJit -and ($controlDump -match 'OPTIONS: opts\.MinOpts\(\) == true' -or
        [regex]::Matches($controlDump, '(?m)^Method code size: \d+').Count -ne 2))) {
    throw 'Control execution or no-change decisions did not match'
}
```

The controls must have two selected compilation headers, exit zero, and print
`Mask conversion corpus: round-trip, vector-use, and vector-input results
verified`. `MaskWithVectorUse` invalidates the local's weight with a vector
read; `VectorMaskInput` ties current and switched costs at 100.00. Neither
converts V01. The maintained runner records the DLL hashes and actual
`DOTNET_RunAltJitCode=1` selection when a managed JIT is supplied.

Capture the positive method directly because the pinned native checked JIT
asserts in `scopeinfo.cpp:722` when a **full dump** tries to print the `k1`
local's debug live range after the phase and codegen IR. The managed full-dump
process instead retries at MinOpts and exits zero, which is **not** proof it
executed the optimized body. The separate disassembly-only run below is the
execution proof. It checks the full-options body, the corpus result, and the
AltJIT marker, without treating native fallback as success. All `DOTNET_`
and `COMPlus_` variables are isolated for these invocations and restored
even on failure:

```powershell
$selector = 'RyuJitSharp.MaskConversionCases:MaskRoundTrip'
$savedJitEnvironment = @(Get-ChildItem Env: | Where-Object {
    $_.Name -match '^(DOTNET_|COMPlus_)'
})
try {
    foreach ($item in $savedJitEnvironment) {
        Remove-Item -LiteralPath ("Env:" + $item.Name)
    }
    $env:DOTNET_ReadyToRun = '0'
    $env:DOTNET_TieredCompilation = '0'
    $env:DOTNET_JitDisasmDiffable = '1'
    $env:DOTNET_JitDumpASCII = '1'
    if ($managedJit) {
        $env:DOTNET_AltJit = $selector
        $env:DOTNET_AltJitPath = (Resolve-Path $managedJit).Path
        $env:DOTNET_AltJitName = [IO.Path]::GetFileName($env:DOTNET_AltJitPath)
        $env:DOTNET_RunAltJitCode = '1'
        $env:DOTNET_AltJitAssertOnNYI = '0'
    }

    $env:DOTNET_JitDump = $selector
    $env:DOTNET_JitStdOutFile = Join-Path $output 'positive-phase.txt'
    & $coreRun $corpus 2>&1 | Set-Content (Join-Path $output 'phase-process.txt')
    $phaseExit = $LASTEXITCODE
    $phaseProcess = Get-Content (Join-Path $output 'phase-process.txt') -Raw
    $phase = Get-Content (Join-Path $output 'positive-phase.txt') -Raw
    if ([regex]::Matches($phase, 'Updated (?:store|use) V01 at \[\d+\] to mask \(removed conversion\)').Count -ne 3 -or
        $phase -notmatch 'Trees after Optimize mask conversions' -or
        $phase -notmatch 'STORE_LCL_VAR mask.*V01') {
        throw 'Positive phase failed to remove three conversions and retain mask V01'
    }
    if ($managedJit) {
        if ($phaseExit -ne 0 -or
            $phase -notmatch '(?m)^V01 loc0: \*{6} START compiling RyuJitSharp\.MaskConversionCases:MaskRoundTrip' -or
            $phase -notmatch 'OPTIONS: opts\.MinOpts\(\) == true') {
            throw 'Unexpected managed full-dump outcome; inspect for a MinOpts retry'
        }
    }
    elseif ($phaseExit -ne -1073740286 -or $phaseProcess -notmatch 'scopeinfo\.cpp:722') {
        throw 'Unexpected native checked-JIT diagnostic exit'
    }

    Remove-Item Env:DOTNET_JitDump, Env:DOTNET_JitStdOutFile
    $env:DOTNET_JitDisasm = $selector
    & $coreRun $corpus 2>&1 | Set-Content (Join-Path $output 'positive-execution.txt')
    $executionExit = $LASTEXITCODE
    $execution = Get-Content (Join-Path $output 'positive-execution.txt') -Raw
    if ($executionExit -ne 0 -or
        $execution -notmatch 'AVX512F=True' -or
        $execution -notmatch 'Mask conversion corpus: round-trip, vector-use, and vector-input results verified' -or
        $execution -notmatch 'MaskRoundTrip\(int\):int \(FullOpts\)' -or
        $execution -notmatch 'V01 loc0.*mask\s+->\s+k1' -or
        $execution -notmatch 'Total bytes of code 52' -or
        ($managedJit -and $execution -notmatch '; invoked as altjit')) {
        throw 'Positive FullOpts body did not execute with the expected mask and output'
    }
}
finally {
    foreach ($item in @(Get-ChildItem Env: | Where-Object {
        $_.Name -match '^(DOTNET_|COMPlus_)'
    })) {
        Remove-Item -LiteralPath ("Env:" + $item.Name)
    }
    foreach ($item in $savedJitEnvironment) {
        Set-Item -LiteralPath ("Env:" + $item.Name) -Value $item.Value
    }
}
```

Inspect `positive-phase.txt` for the weighted costs (V01: 300.00 versus
0.00), the three rewrites, and the post-phase tree without conversions.
The pinned native positive body is 52 bytes; the vector-use and vector-input
controls are 67 and 49 bytes. Retain the raw native and managed captures,
including their line endings. The observed phase slices differ in the profile-check diagnostic;
the positive disassembly differs only in tab spacing before the `RWD04`
comment. All 10 positive, 14 vector-use, and 11 vector-input emitted
instruction lines match. The artifact-only
`artifacts\mask-conversion\capture.ps1` and its raw captures preserve the
historical runs, but are not needed to reproduce these checks.
