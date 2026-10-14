# RyuJitSharp

Follow `AGENTS.md` and `docs\porting\README.md` for the porting and parity
contracts. Use `docs\porting\state.json` for pinned upstream revisions and the
concrete resume cursor; load only relevant plan and deviation sections.

## Architecture

- `sources\Core` is the managed RyuJIT port. Compiler partials implement the
  phase pipeline; CodeGen and Emitter implement target instruction selection,
  emission, and metadata publication. Native JIT/EE contracts and their managed
  declarations must remain compatible.
- `Configuration` selects target behavior in the universal managed compilation.
  NativeAOT uses RID-specific files under `sources\Core\ILLink.Substitutions`
  to specialize that behavior. `RuntimeIdentifier` selects the host;
  `TargetRuntimeIdentifier` or `RyuJitTargetRuntimeIdentifier` selects compiler
  target semantics.
- GenerateTables combines native-derived inputs and managed ABI declarations
  to produce tables and bindings compiled into Core. Change inputs/templates
  with their generated output, not generated files alone.
- Core tests are NUnit fixtures. The Target test project links selected Core
  fixtures to exercise other target semantics on the host; these managed tests
  are not execution of generated code on the target architecture.

## Build and test commands

Use the normal developer build for fast implementation iterations:

```powershell
.\build.cmd -fast -test -solution tests\Core\RyuJitSharp.UnitTests.csproj
```

Normal builds run the repository's analyzer and `.editorconfig` checks.
Full-analysis build and focused test examples:

```powershell
dotnet build sources\Core\RyuJitSharp.csproj -c Debug
dotnet test tests\Core\RyuJitSharp.UnitTests.csproj -c Debug --filter "FullyQualifiedName~RegisterTrackingTests"
```

Repeat affected selections in Release. For a direct fast `dotnet` iteration,
add `-p:RunAnalyzers=false -p:GenerateDocumentationFile=false`; fast results
do not replace the full-analysis acceptance required by the porting contract.

Keep the test host and compiler target separate:

```powershell
dotnet test tests\Targets\RyuJitSharp.Target.UnitTests.csproj -c Debug -r win-x64 -p:TargetRuntimeIdentifier=win-arm64 --filter "FullyQualifiedName~Arm64EmitterInstructionRemovalTests"
```

Use the porting contract for NativeAOT publication, phase-dump, disassembly,
and execution checks at the relevant target frontier.

## Batch acceptance

- For mechanical porting or conditional cleanup, select a complete file group
  or proven pattern family. Record its completion condition, validation, and
  deferrals in `checkpoint.activeBatch`; inventory that family once under
  `artifacts`, not through repeated whole-repository scans.
- Give every candidate a converted, retained-with-concrete-dependency, or
  explicitly deferred disposition. Touched files or partially processed
  families do not satisfy the completion condition.
- Implement the coherent batch before combined Debug/Release acceptance.
  An intermediate scoped implementation commit is not a new validation
  boundary. Run earlier checks only for a concrete failure or material risk;
  close and commit the accepted batch before starting another pattern.
- After acceptance, remove resolved checkpoint details. Keep the concrete next
  action, unresolved boundaries, and relevant evidence links; do not append
  completed validation narratives to `state.json` or the plan.

## Generator isolation

Build GenerateTables through its project so its inputs and managed ABI
dependencies are copied. Invoke an artifact-owned executable tree, including
the complete copied dependencies when staging an isolated run.

Verify the executable's resolved `Inputs` and `Outputs` paths before generation.
The generator anchors them to `AppContext.BaseDirectory`, sets its working
directory there, and deletes that `Outputs` subtree. Changing the launch
directory does not isolate output. Keep temporary generator trees outside
`sources`; review and copy only intended generated files back.

## Universal native-facing enums

Preserve native numeric values and per-target aliases when making declarations
universal. Resolve name collisions with target-qualified members, not globally
unique replacement IDs or a parallel managed contract solely for disambiguation.

For managed target-disambiguated names, use `PREFIX_CONFIG_NAME`, with `X64`
and `ARM32` target labels. Share a symbol only when its native identity, value,
and semantics match. Preserve approved semantic distinctions and do not perform
unrelated renames while applying these conventions.

## Debug guard cleanup

Conditional diagnostic calls omit Release argument evaluation, but referenced
types and members must still bind in Release. Distinguish removable body guards
from missing declaration/API/layout closures. Record each concrete dependency
once and continue the rest of the batch rather than retrying the same rejected
candidate on the next scan.

Reuse `TestConfiguration` helpers for Debug-only test setup. Preserve Debug TLS
nesting and Release thread-static compiler identity and restoration. Keep
diagnostic text, output gating, and Release side effects unchanged; do not add
allocations, unnecessary state, or success-shaped shims merely to lower
directive counts.
