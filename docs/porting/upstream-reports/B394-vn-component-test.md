# VN component test asserts against the current commutative operand order

**Confirmed in native RyuJIT**, Windows x64 Checked, at dotnet/runtime
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Enabling
`DOTNET_JitComponentUnitTests=1` terminates the native process at
`valuenum.cpp:11816`. The same application succeeds with the setting disabled.
This is a stale self-test expectation, not a demonstrated generated-code defect.

## Cause

[`ValueNumStore::VNForFunc`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/valuenum.cpp#L2865-L2881)
canonicalizes commutative operands by VN number, then moves a non-handle constant
to argument one. Consequently, `ADD(1, vnRandom1)` is stored as
`ADD(vnRandom1, 1)`.

[`ValueNumStore::RunTests`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/valuenum.cpp#L11806-L11816)
constructs that expression, retrieves its function application, and asserts the
opposite order:

```cpp
fa2a.GetArg(0) == vnFor1 && fa2a.GetArg(1) == vnRandom1
```

The likely correction is to update the test expectation to the canonical order,
not change production canonicalization. No native patch has been applied or
validated as part of this report.

## Reproduction

Use a Checked Core_Root built from the revision above, in a fresh shell without
other JIT stress settings. `$CoreRoot` denotes that build and `$Application`
denotes a managed application compatible with it:

```powershell
$env:DOTNET_ReadyToRun = '0'
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_JitName = 'clrjit.dll'
$env:DOTNET_JitPath = Join-Path $CoreRoot 'clrjit.dll'
$env:DOTNET_JitComponentUnitTests = '1'
& (Join-Path $CoreRoot 'corerun.exe') $Application
```

The verified application was the retained `ParameterCycles.dll` corpus. With
component tests enabled, stderr contains the assertion above and the process
exits `0xC0000602`, without timing out. With the setting changed to `0`, it exits
zero and reports `ParameterCycles: 2048 floating permutations verified`.
[`Compiler::compDoComponentUnitTestsOnce`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/compiler.cpp#L1731-L1747)
runs this test once during JIT initialization when the option is enabled;
the corpus does not need to construct a particular expression.

Exact local captures and verified input hashes are retained under:

- `artifacts/upstream-reports/component-native-enabled-1/`
- `artifacts/upstream-reports/component-native-control-1/`

To repeat those captures from this repository, select fresh output directories:

```powershell
python artifacts\upstream-reports\capture-native.py `
  artifacts\primary-parameter-cycles\reference-after\manifest.json `
  artifacts\upstream-reports\component-native-enabled-2 --component-tests 1
python artifacts\upstream-reports\capture-native.py `
  artifacts\primary-parameter-cycles\reference-after\manifest.json `
  artifacts\upstream-reports\component-native-control-2 --component-tests 0
```

The runner verifies the reference input hashes and explicitly selects native
`clrjit.dll`; RyuJitSharp is not executing these tests. The C# port retains the
pinned assertion and canonicalization pending an upstream correction.
