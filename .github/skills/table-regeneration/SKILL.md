---
name: table-regeneration
description: Safely regenerate RyuJitSharp tables and JIT/EE bindings using GenerateTables with artifact-owned staging, verified input provenance, reproducible outputs, and consumer validation. Use when generator inputs, templates, or generated files change.
---

# Table regeneration

Follow `AGENTS.md`, `.github\copilot-instructions.md`, and
`docs\porting\README.md`. Read `generatorInputMapping` and the relevant
checkpoint/pins in `docs\porting\state.json`. Use `oracle-update` for upstream
reconciliation and `parity-validation` for affected behavior evidence. This
skill does not authorize updating upstream pins or repairing unrelated tables.

## Establish the input and output closure

- Preserve existing staged, unstaged, and untracked work. Identify the exact
  generator revision/WIP, inputs/templates, copied managed ABI declarations,
  and generated consumers owned by this batch.
- Record input provenance and source/binary hashes under `artifacts`.
  Use immutable native revisions, the recorded mapping exceptions, and the
  accepted managed input adaptations; do not replace inputs with whichever
  upstream files happen to be newest.
- Map generated outputs to their actual consumers before copying anything.
  Most output paths are relative to `sources\Core`, but the recorded
  `Outputs\jit\emitxarch` tables map to `sources\Core\jit\emit`.
  Do not blindly copy the entire output tree over Core.
- Establish a reproducible clean baseline before changing inputs/templates.
  Reuse a verified baseline manifest when its inputs, generator, toolchain,
  and configuration match. Otherwise use an immutable source snapshot under
  `artifacts`, preserving repository-relative build configuration and ABI
  input paths; never overwrite or reset the active worktree for this purpose.
- Compare the baseline's complete output set with its intended checked-in
  outputs. Record unexplained drift separately instead of folding it into the
  requested input/template change.

## Build through the project and stage the complete executable tree

Build `sources\GenerateTables\GenerateTables.csproj`, not just `Program.cs`.
The project copies `Inputs` and these managed ABI declarations:

- `Core\inc\corjit\ICorJitInfo.cs`
- `Core\inc\corinfo\ICorDynamicInfo.cs`
- `Core\inc\corinfo\ICorStaticInfo.cs`

Keep the generator assembly/apphost, dependency and runtime configuration
files, copied inputs, and other build dependencies together. Do not stage a
lone executable or source-folder copy missing the project's dependencies.
Verify copied input hashes against the intended source snapshot; existence
and `PreserveNewest` timestamps alone do not prove the right inputs were copied.

Run from the repository root. Choose a fresh absolute `$stageDirectory` under
this worktree's `artifacts`, outside the evaluated build output directory:

```powershell
$ErrorActionPreference = "Stop"
$project = "sources\GenerateTables\GenerateTables.csproj"
dotnet build $project -c Debug
if ($LASTEXITCODE -ne 0) {
    throw "GenerateTables build failed."
}

$targetDirectory = dotnet msbuild $project -nologo -p:Configuration=Debug -getProperty:TargetDir
if ($LASTEXITCODE -ne 0) {
    throw "Cannot resolve the generator build output."
}
$buildDirectory = (Resolve-Path -LiteralPath (($targetDirectory | Out-String).Trim())).Path
$stageDirectory = [IO.Path]::GetFullPath($stageDirectory)
$artifactPrefix = (Resolve-Path -LiteralPath "artifacts").Path.TrimEnd('\') + '\'
$buildPrefix = $buildDirectory.TrimEnd('\') + '\'
if (-not $stageDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $stageDirectory.StartsWith($buildPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Stage in a separate directory beneath this worktree's artifacts."
}
if (Test-Path -LiteralPath $stageDirectory) {
    throw "Use a fresh generator staging directory."
}

$null = New-Item -ItemType Directory -Path $stageDirectory
Get-ChildItem -LiteralPath $buildDirectory -Force |
    Copy-Item -Destination $stageDirectory -Recurse
```

Apply the same recipe to the selected baseline snapshot when needed. Preserve
its build properties and `.editorconfig`; do not substitute a partial source
tree that changes build or generator behavior.

## Verify the destructive output boundary before invocation

`GenerateTables.Main` anchors I/O to `AppContext.BaseDirectory`, sets its
working directory there, and deletes that executable tree's `Outputs` subtree
before writing it. Changing the shell's launch directory does not isolate
generation.

Before launching:

- Resolve and inspect the actual staged generator, its `Inputs`, all copied
  ABI declarations, and its `Outputs` destination.
- Confirm that the output destination belongs to this batch under `artifacts`
  and is not a source directory, workspace root, or another worker's tree.
  Check for junctions/symlinks that redirect any relevant path.
- Check the complete copied dependency set and input identities. Review the
  generator's entry point if its path/deletion behavior changed.

Only after those checks, invoke the staged assembly:

```powershell
$generator = Join-Path $stageDirectory "GenerateTables.dll"
dotnet $generator
if ($LASTEXITCODE -ne 0) {
    throw "Staged table generation failed."
}
```

Record the exit status, generator/input identities, and the generated relative
file list/hashes. Do not interpret leftover files from a failed run as a
successful output set.

## Review and integrate the generated delta

- Repeat generation from the same input snapshot in a second fresh staged
  tree when establishing reproducibility. Compare the complete relative file
  sets and bytes, including additions/deletions, not just counts or one table.
- Review the change against the baseline and intended native/managed
  declarations. Explain every changed output; retain unrelated drift outside
  this batch. Keep source changes and their generated outputs together.
- Preserve ordered enums, native numeric identities/aliases, target-width
  arithmetic, instruction ordinals/opcode metadata, and JIT/EE vtable ordering,
  signatures, calling conventions, and field layouts.
- Validate `NamedIntrinsic` against the full ordered native enum as well as
  hardware rows. Preserve instruction/tuple-table correspondence and all
  ordered operation/use pairs in the 256-row ternary decomposition input.
- Keep generated `ReadOnlySpan` table literals compile-time constant. Select
  target-width-specific static spans outside their literals rather than adding
  runtime expressions that lose static-data storage.
- Copy only reviewed outputs to their mapped consumers. Check repository
  encoding/line-ending policy and the final Git diff; never repair only
  `*.generated.cs` or include staging trees in source changes.

## Validate the consumers and close

Use standard .NET build/test commands from the porting contract. Validate the
complete affected consumer family in Debug and Release, including relevant
32-/64-bit or architecture Target fixtures when table identity/layout changes.
Generated bindings require their native contract checks; changed codegen
tables may require phase, disassembly, metadata, or execution evidence through
`parity-validation`. Generation and compilation alone are not parity passes.

Verify nonempty selections and the final source/input/output identities. Run
required full-analysis acceptance; fast iteration evidence does not replace it.
Fix regressions caused by this batch, and record unrelated validation blockers
without disabling checks or silently excluding fixtures.

After acceptance, commit only the coherent generator/input/output/test changes
and related documentation. Keep manifests and comparison evidence under
`artifacts`; remove resolved checkpoint details rather than adding a generator
history to `state.json` or `PLAN.md`. Update provenance pins only when the
authorized reconciliation actually establishes them. Clean up only this
batch's identified temporary trees when their evidence is no longer needed;
preserve active recovery material and other workers' outputs.
