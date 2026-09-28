# Forced register spilling exposes a retbuf wrapper-order assertion

**Confirmed in native RyuJIT**, Windows x64 Checked, at dotnet/runtime
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. With `JitStressRegs=0xC03`
and `GCStress=4`, compilation of
`Runtime_133521.ScalarFmaNegation(int, int)` asserts during linear scan register
allocation. The same input with GC stress but without register stress succeeds.
No ordinary-configuration miscompilation or memory-safety impact is established.

## Failure and relevant tree shape

The assertion is in
[`Compiler::gtCallGetDefinedRetBufLclAddr`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/gentree.cpp#L22020-L22048):

```cpp
node->OperIs(GT_LCL_ADDR) &&
lvaGetDesc(node->AsLclVarCommon())->IsDefinedViaAddress()
```

The retained post-allocation dump contains this retbuf argument for the call to
`Runtime_133521.NegatedScalarFma(Vector128<float>, ...)`:

```text
t672 RELOAD
  t579 PUTARG_REG rcx
    t671 RELOAD
      t28 LCL_ADDR V05
```

The helper checks for `PUTARG_REG` or `PUTARG_STK` **before** calling
`gtSkipReloadOrCopy`. An outer `RELOAD` therefore bypasses the putarg check;
the subsequent skip exposes `PUTARG_REG`, not the required `LCL_ADDR`.
[`gtSkipReloadOrCopy`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/gentree.h#L10176-L10184)
intentionally removes one reload/copy layer, not a putarg wrapper.

This strongly points to handling an outer reload/copy before inspecting the
putarg, while preserving the existing inner reload/copy handling. No debugger
capture identifies the exact failing call object, and no proposed native fix
has been applied or tested; the dump and helper ordering are the supporting
evidence, not a validated fix.

## Reproduction

The verified input is the retained `UpstreamRegressionProbe.dll`, built from
the pinned upstream regression sources, including
[`src/tests/JIT/Regression_ro_2/Runtime_133521.cs`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/tests/JIT/Regression_ro_2/Runtime_133521.cs).
Its driver invokes `ScalarFmaNegation` with the upstream cases
`(0, -11)`, `(1, -13)`, `(2, 13)` and `(3, 11)`, along with other regression
entry points. It requires AVX and FMA; the verified host also supports AVX512F.
The reproducer has not been minimized.

In a fresh shell, with `$CoreRoot` pointing to the matching Checked build and
`$Corpus` to that DLL:

```powershell
$env:DOTNET_ReadyToRun = '0'
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_JitName = 'clrjit.dll'
$env:DOTNET_JitPath = Join-Path $CoreRoot 'clrjit.dll'
$env:DOTNET_GCStress = '4'
$env:DOTNET_JitStressRegs = 'c03'
$env:DOTNET_JitStressRegsRange = '4f3e2341 8075f3c3 36970323 5eb69743 f18764f8 076e31f8'
$env:DOTNET_JitDump = 'Runtime_133521:*'
$env:DOTNET_JitDumpASCII = '1'
$env:DOTNET_JitDisasmDiffable = '1'
$env:DOTNET_JitStdOutFile = Join-Path $PWD 'spill-jitdump.txt'
& (Join-Path $CoreRoot 'corerun.exe') $Corpus
```

The hashes above belong to the retained binary, not a portable method selector.
If rebuilding or changing the harness, first capture its method hashes without
register stress and update the range. Unrestricted `JitStressRegs=0xC03` also
asserted in `EventSource.Initialize` before the selected regression compiled;
the scoped range avoids that earlier failure.

Observed failure:

```text
Runtime_133521:ScalarFmaNegation(int,int)
phase: Linear scan register alloc
IL size: 341
method hash: 0x8075f3c3
optimization: FullOpts
source: gentree.cpp:22046
process exit: 0xC0000602
```

With `DOTNET_JitStressRegs` and `DOTNET_JitStressRegsRange` removed, the fresh
control exits zero, verifies the regression results, and reports 3,404 GC-stress
collections. That collection count is observational, not a stable expectation.
Both runs use native `clrjit.dll` and neither times out.

## Retained evidence

Fresh captures, input hashes, stdout, stderr and dumps:

- `artifacts/upstream-reports/spill-native-1/`
- `artifacts/upstream-reports/spill-native-control-1/`

The tree above is recorded in `spill-native-1/jitdump.txt` around lines
112186-112202. Original source/dependency hashes and the original scoped failure
remain under `artifacts/upstream-regressions/spill-selected-native-1/`.
The exact driver and project are retained in
`artifacts/upstream-regressions/corpus-built-3/source/`.

Repeat with fresh output directories:

```powershell
python artifacts\upstream-reports\capture-native.py `
  artifacts\upstream-regressions\spill-selected-native-1\Runtime_133521\manifest.json `
  artifacts\upstream-reports\spill-native-2
python artifacts\upstream-reports\capture-native.py `
  artifacts\upstream-regressions\gcstress-native-1\Runtime_133521\manifest.json `
  artifacts\upstream-reports\spill-native-control-2
```

The local runner verifies the recorded inputs, clears inherited runtime settings,
and explicitly selects native `clrjit.dll`. The port retains pinned behavior;
this report does not propose changing only RyuJitSharp to hide the assertion.
