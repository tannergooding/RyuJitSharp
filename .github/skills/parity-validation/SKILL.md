---
name: parity-validation
description: Validate a RyuJitSharp port batch against its pinned native JIT using normal .NET commands and existing corpus/dump comparison tools. Use for phase, diagnostic, codegen, or execution parity acceptance.
---

# Parity validation

Follow `docs\porting\README.md`, `AGENTS.md`, and the active checkpoint in
`docs\porting\state.json`. Read only the relevant milestone/deviation sections.
Use this skill for acceptance of ordinary port batches or oracle updates;
`oracle-update` owns upstream reconciliation and pin changes. Validation alone
does not authorize compiler fixes, new comparison allowances, or publication.

## Establish the evidence boundary

Before running commands, record:

- The native reference revision appropriate to the managed batch, normally
  `upstream.reconciledCommit`, plus the managed revision and immutable snapshot
  of any relevant WIP. Do not substitute a moving upstream branch.
- The native JIT and host provenance, JIT/EE ABI, binary hashes, SDK/toolchain,
  compiler flavor, host RID, target RID, ISA settings, and feature configuration.
  The installed SDK's `clrjit` is not the pinned native reference.
- The concrete corpus, methods/overloads, expected compilation counts,
  phase boundaries, options, and approved exceptions relevant to the change.
- The required acceptance frontier: managed tests, named phase diagnostics,
  machine code/disassembly/metadata, or actual managed execution.

Check available binaries and prior manifests first. Reuse evidence only when
its source, binaries, inputs, and configuration still match. Do not rebuild the
oracle or republish the managed JIT for unchanged inputs. If a matched host or
required backend is unavailable, state the missing prerequisite and continue
independent authorized checks without claiming that frontier passed.

Keep `checkpoint.dynamicTlsInputBoundary` and other unresolved ownership/ABI
boundaries explicit. Do not claim complete parity or change compiler constants
to conceal unidentified EE inputs.

## Build and test through normal workflows

Use the smallest relevant standard commands; combine related fixture filters.
For example:

```powershell
dotnet build sources\Core\RyuJitSharp.csproj -c Debug
dotnet test tests\Core\RyuJitSharp.UnitTests.csproj -c Debug --filter "FullyQualifiedName~RegisterTrackingTests"
dotnet test tests\Targets\RyuJitSharp.Target.UnitTests.csproj -c Debug -r win-x64 -p:TargetRuntimeIdentifier=win-arm64 --filter "FullyQualifiedName~Arm64EmitterInstructionRemovalTests"
```

Substitute affected selections and repeat in Release. `RuntimeIdentifier`
selects the host; `TargetRuntimeIdentifier`/`RyuJitTargetRuntimeIdentifier`
selects compiler semantics. Target fixtures do not execute emitted instructions
on that architecture. Verify all helper dependencies are included in each
project and the intended tests actually execute.

Use the contract's fast workflow for implementation iterations, then run
required full-analysis acceptance without analyzer/documentation overrides.
Record baseline failures and regressions separately; don't disable diagnostics
or omit failing fixtures to report success. Use NativeAOT publication and native
loading as separate gates when required. Initialize Visual C++ tools and run
publication in the same process, following the existing Windows recipe.

## Capture a matched native/managed pair

Use `scripts\porting\Invoke-PortingCorpus.ps1` rather than another corpus runner.
Read its parameters and the relevant corpus recipe in the porting contract.
Build the corpus with standard `dotnet build`. For the default corpus:

```powershell
dotnet build sources\PortingCorpus\PortingCorpus.csproj -c Release
```

Resolve `$coreRoot`, `$managedJit`, `$nativeCommit`, `$managedSource`, and the
actual `$corpus` output from verified inputs. `$managedSource` must identify
relevant WIP, not just HEAD. Choose a fresh `$runRoot` under `artifacts` and
keep it distinct from earlier runs. With these variables established:

```powershell
$ErrorActionPreference = "Stop"
$nativeDirectory = Join-Path $runRoot "native"
$managedDirectory = Join-Path $runRoot "managed"

.\scripts\porting\Invoke-PortingCorpus.ps1 -CoreRoot $coreRoot -Corpus $corpus -OutputDirectory $nativeDirectory -NativeCommit $nativeCommit
.\scripts\porting\Invoke-PortingCorpus.ps1 -CoreRoot $coreRoot -Corpus $corpus -OutputDirectory $managedDirectory -NativeCommit $nativeCommit -ManagedJit $managedJit -ManagedSource $managedSource
```

This example uses the runner's default corpus/type/method selection. For another
corpus, supply its exact `-TypeName` and nonempty `-ExpectedMethods`; retain
duplicate names when overloads/instantiations are expected. The selector covers
the specified type, so the expected set must account for every captured method.

- Keep native and managed settings identical: minopts, tier/PGO, stress seeds,
  ISA, optional phases, and corpus bytes. Verify supported parameter combinations
  in the runner instead of guessing flags.
- Preserve `manifest.json`, raw dumps/stdout/stderr, binary hashes, command
  outcomes, and expected/observed method counts. A fresh file timestamp or
  process exit zero is not proof of complete managed compilation.
- The managed capture defaults to nonexecuting mode and can expect native
  fallback. It can provide scoped managed phase evidence, not execution parity.
  Use `-ExecuteManagedCode` only for the implemented backend, then verify every
  selected method completed managed compilation without skip/fallback.
- Repeat the native control when nondeterminism or stress affects interpretation.
  A timeout, missing output, empty selection, or variation in the control is a
  failed or limited comparison, not noise to normalize away.

## Interpret the existing comparators exactly

`scripts\porting\Compare-PortingDumps.ps1` supports `Importation`,
`Redundant branch opts`, and `FullDump`. Its default `Importation` comparison
ends before `Finishing PHASE Importation`; it excludes the final post-import
dump. `FullDump` compares captured per-method text without independently
checking compilation completion. Verify the requested boundaries and complete
capture before interpreting either result.

Ordinary dump differences produce JSON entries with `equal=false`; they do
not cause a failing process exit. Check the report, not just the exit status.
For an import-prefix comparison with the directories above:

```powershell
$reportPath = Join-Path $runRoot "importation-comparison.json"
if (Test-Path -LiteralPath $reportPath) {
    throw "Use a fresh comparison report path."
}

.\scripts\porting\Compare-PortingDumps.ps1 -NativeDump (Join-Path $nativeDirectory "jitdump.txt") -ManagedDump (Join-Path $managedDirectory "jitdump.txt") -OutputPath $reportPath -Phase Importation

$nativeManifest = Get-Content -LiteralPath (Join-Path $nativeDirectory "manifest.json") -Raw | ConvertFrom-Json
$results = @(Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json)
if (($results.Count -eq 0) -or
    ($results.Count -ne @($nativeManifest.methodHeaders).Count) -or
    (@($results | Where-Object { $_.equal -ne $true }).Count -ne 0)) {
    throw "Importation-prefix comparison is incomplete or differs; inspect the report."
}
```

The comparer rejects method identity/order differences and duplicate headers;
it cannot compare a capture with duplicate per-method headers. Select a
compatible corpus or use the existing appropriate cohort tooling.

Do not enable `-CanonicalizeProcessSpecificHandles` without an applicable
approved comparison allowance. Retain raw differences even when an approved
allowance is used. Do not strip IDs, ordering, whitespace, costs, or metadata
to manufacture equality.

For supported helper cohorts, reuse `scripts\porting\compare_helper_captures.py`
with its existing configuration and native repeat-control inputs. Its normalized
phase/stdout equality does not establish raw newline fidelity; its byte checks
also do not cover referenced data or relocation-target contents. Read the
contract's documented coverage before reporting results. Use matched upstream
SuperPMI/disassembly tooling for the required codegen/metadata frontier rather
than treating dump equality as machine-code or GC/EH/unwind parity.

## Diagnose and report

Investigate the first meaningful mismatch within the active batch. Check input
identity, JIT selection, and phase completion before blaming translated code.
Preserve mismatches and minimize the reproducer without removing the affected
behavior. New observable differences require approval; confirmed native defects
belong in `UPSTREAM-REPORTS.md`, and accepted differences in `DEVIATIONS.md`.

Report the exact evidence frontier, expected/actual coverage, mismatches,
skips/fallback, approved exception IDs, and remaining limits. Link logs and
manifests rather than copying transcripts. Keep completed evidence under
`artifacts` and retain only active limits/relevant references in the checkpoint;
do not add a validation history to `PLAN.md`.

Accept only the final relevant source/binary/configuration snapshot. Reuse
valid results and rerun invalidated checks after grouped fixes. Required
checks that cannot run remain explicit gaps; do not label the batch verified
or publish without authorization for the exception.
