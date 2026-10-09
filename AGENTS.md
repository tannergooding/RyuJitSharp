# Porting RyuJIT

Read [the porting contract](docs/porting/README.md) before changing compiler behavior.
Use [state.json](docs/porting/state.json) for pinned revisions and the current
checkpoint, and load only the relevant sections of the
[plan](docs/porting/PLAN.md) and [deviations](docs/porting/DEVIATIONS.md).

- Preserve the recognizable C# port while resolving its tracked correctness,
  parity, and coverage work. Record confirmed native defects in
  [UPSTREAM-REPORTS.md](docs/porting/UPSTREAM-REPORTS.md) and accepted observable
  differences in [DEVIATIONS.md](docs/porting/DEVIATIONS.md); do not bundle
  unrelated cleanup.
- Validate changes at the relevant target frontier. Preserve target conditionals
  and dependency behavior; unsupported target paths must remain explicit and
  terminating rather than becoming silent no-ops or success-shaped fallbacks.
- Preserve upstream algorithms, phase ordering, diagnostics, numeric semantics,
  and JIT/EE contracts. Idiomatic C# does not authorize different generated code
  or dumps. Ask before substantial redesigns or new observable deviations.
- Follow the existing C# layout, not compressed native or generated-looking
  translation. Use multiline control-flow bodies and braced switch sections,
  with one executable statement per line. Group code for visual balance and
  readable flow, much like paragraphs: keep related assertions, values,
  operations, and returns together; separate distinct steps or setup/cleanup
  when they form their own logical components. Choose blank lines from the
  surrounding context, not a hard rule tied to statement kinds.
  Respect `.editorconfig`, but also review logical grouping and wrap long
  expressions at meaningful boundaries; formatter compliance is not sufficient.
- Use the pinned upstream revision recorded in [state.json](docs/porting/state.json)
  as the source and behavior reference when relevant. Native source absence or
  successful compilation is not evidence of parity.
- When synchronizing upstream changes, compare immutable old and new revisions
  and apply relevant deltas to the managed implementation. Keep the source map
  sparse and record only non-obvious mappings, active exceptions, and unresolved
  decisions; do not recreate a native-source inventory.
- Keep the mapping sparse. Conventionally, `src/coreclr/jit/<stem>.{h,cpp}`
  maps to `sources/Core/jit/<stem>/`; `Compiler` partials span several native
  files. Verify symbol matches, and record non-obvious mappings for active work.
  Conventional mappings need no per-function entries. Treat unexpected native
  absence or a known incomplete translation as an exception, not a reason to
  re-audit every retired definition.
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
- After context recovery, resume the concrete operation in `checkpoint.nextAction`;
  do not restart planning or reopen settled scope and validation decisions.
  Keep that cursor specific: the failing command or symbol, the unanswered
  question, and the next completion boundary.
- Bound investigations to blockers of the active batch. Stop when the question
  is answered; record nonblocking discrepancies briefly and continue. Repeated
  reads or checks without an implementation decision require narrowing the
  investigation, not expanding the process. Reuse established validation and
  consolidate ledger/evidence maintenance across coherent portions rather than
  restarting a test-and-documentation cycle for each helper.
