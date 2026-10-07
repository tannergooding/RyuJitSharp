# Porting RyuJIT

Read [the porting contract](docs/porting/README.md) before changing compiler behavior.
Use [state.json](docs/porting/state.json) for pinned revisions and the current
checkpoint, and load only the relevant sections of the
[plan](docs/porting/PLAN.md) and [deviations](docs/porting/DEVIATIONS.md).

- Establish a clean, recognizable port before restructuring. Record discovered
  bugs, rename/refactoring candidates, and architectural ideas in the
  [backlog](docs/porting/BACKLOG.md); do not fold unrelated cleanup into a port.
- Validate each target at its implemented frontier. Translate each whole function,
  preserving target conditionals and dependency calls. Add compilable declarations
  and tracked, terminating NYI helper stubs for unported other-target dependencies;
  do not omit their callers' branches. Remove the translated definition from
  `runtime-port` in the same batch, retaining the unported helper definitions there.
  A caller need not wait for those helpers to execute. Do not introduce silent
  no-ops or success-shaped fallbacks.
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
- Use the intact, pinned `runtime-oracle` for source and behavior comparisons.
  The residual `runtime-port` tree identifies remaining work; deleted native code
  is not evidence of parity. Obtain their local paths from the session setup,
  not from assumptions about another contributor's checkout.
- Treat the residual as the untranslated-definition work list, not a second
  recurring audit project. Track stubs and exceptions sparsely. On an oracle
  update, use the immutable old/new upstream diff: apply changes to already
  translated definitions in C# and keep them absent from the residual; update
  still-unported definitions and retain new definitions for porting. Do not
  restore completed bodies merely to resolve modify/delete conflicts.
- Keep the residual native branch at the pinned oracle plus one consolidated
  `Remove ported code` commit. Amend that native commit for accepted retirements
  and restorations; do not append new cleanup commits. Preserve recovery refs
  when consolidating history. This authorization does not cover C# amendments.
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
