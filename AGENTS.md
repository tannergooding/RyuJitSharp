# Porting RyuJIT

Read [the porting contract](docs/porting/README.md) before changing compiler behavior.
Use [state.json](docs/porting/state.json) for pinned revisions and the current
checkpoint, and load only the relevant sections of the
[plan](docs/porting/PLAN.md) and [deviations](docs/porting/DEVIATIONS.md).

- Establish a clean, recognizable port before restructuring. Record discovered
  bugs, rename/refactoring candidates, and architectural ideas in the
  [backlog](docs/porting/BACKLOG.md); do not fold unrelated cleanup into a port.
- Windows x64 is the first execution/parity target. Port whole functions,
  including their relevant conditional paths, rather than selected statements.
  Paths unique to other platforms may explicitly report NYI while remaining
  compilable. Do not introduce silent no-ops or success-shaped fallbacks.
- Preserve upstream algorithms, phase ordering, diagnostics, numeric semantics,
  and JIT/EE contracts. Idiomatic C# does not authorize different generated code
  or dumps. Ask before substantial redesigns or new observable deviations.
- Follow the existing C# layout, not compressed native or generated-looking
  translation. Use multiline control-flow bodies and braced switch sections,
  one executable statement per line, and blank lines between distinct steps,
  before control-flow statements, after completed control-flow blocks, and
  before a final return. Keep related declarations and operations together.
  Respect `.editorconfig`, but also review logical grouping and wrap long
  expressions at meaningful boundaries; formatter compliance is not sufficient.
- Use the intact, pinned `runtime-oracle` for source and behavior comparisons.
  The residual `runtime-port` tree identifies remaining work; deleted native code
  is not evidence of parity. Obtain their local paths from the session setup,
  not from assumptions about another contributor's checkout.
- Keep the mapping sparse. Conventionally, `src/coreclr/jit/<stem>.{h,cpp}`
  maps to `sources/Core/jit/<stem>/`; `Compiler` partials span several native
  files. Verify symbol matches, and record non-obvious mappings for active work.
  An absent mapping or missing native function does not mean a port is complete.
- Preserve saved WIP and its staged/unstaged distinction. Apply snapshots by
  immutable ID, not a moving stash index; do not drop them until verified.
- Change table inputs and their generator together. Review generated output;
  do not make fixes only in `*.generated.cs`. The generator uses its working
  directory and deletes that directory's `Outputs` subtree.
- Prioritize phase dumps, then disassembly and regular runtime tests as those
  become available. Keep tooling checks focused; extensive unit-test infrastructure
  and restructuring are post-port work. Builds, empty runs, native fallback, and
  `CORJIT_SKIPPED` are not parity passes.
- Update the relevant checkpoint, mapping, deviation, and evidence when finishing
  a batch. Keep raw dumps, large inventories, and local recovery paths outside
  maintained documentation; record how to reproduce them.
- Commit each completed, validated dependency-coherent batch with its related
  tests and documentation before starting the next batch. Recovery snapshots
  supplement regular commits; they do not replace them. Leave incomplete work
  uncommitted, and keep unrelated changes out of the batch.
- Continue approved work without asking about routine translation details. Ask
  before expanding scope, accepting output differences, changing ABI/ownership
  contracts, or making substantial design changes. Do not push or open a PR
  without explicit authorization.
