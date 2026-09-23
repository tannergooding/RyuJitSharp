# Deviations and limitations

This is a seed register, not an exhaustive audit of the existing port.
An existing implementation is evidence of a difference, not automatic approval
of every consequence. Record new differences here as they are encountered.

Each entry needs a stable ID, status, native/C# locations, rationale, observable
effect, and validation or remaining action. Keep unfinished implementation out
of the accepted-output exception list.

## Accepted policy

### D001: Managed allocation instead of the native arena

**Status:** accepted representation change; allocation-statistics differences
are accepted. No other output difference is implied.

Native arena-allocated compiler objects can use managed allocation and lifetimes.
For example, native `src/coreclr/jit/gentree.h` corresponds to the managed class
in `sources/Core/jit/gentree/GenTree.cs`. Native allocation counts/bytes are not
expected to match. Compiler decisions, traversal order, logical node IDs,
generated code, and diagnostics other than those statistics still must match.

**Remaining action:** audit identity/lifetime assumptions as functions are
ported. If a comparator excludes allocation statistics, identify the exact
fields/lines and test that neighboring semantic metrics still compare.

### D002: Idiomatic C# representation

**Status:** accepted when behavior is preserved; not an output exception.

Partial classes, properties, managed references/collections, spans, and existing
C# helpers may replace native structure where appropriate. Examples include
`Compiler` partials and `GenTree` properties. Preserve equality/identity,
collection ordering, integer semantics, ownership, and native interop contracts.
Larger algorithmic or architectural changes require separate approval.

### D003: Deferred non-Windows-x64-only paths

**Status:** accepted scoped deferral; not a successful execution/parity result.

Port whole functions. A path unique to another target may explicitly terminate
with NYI while remaining compilable. Record the affected symbol, target predicate,
and missing behavior when introducing such a deferral. Windows-x64 behavior
within the function must not be replaced by stubs. Verify that the selected
failure path cannot continue as if implemented in Debug or Release.

## Implementation notes and parity findings

### R001: Temporary serialization for debugging

`sources/Core/jit/ee_il_dll/CILJit.cs`, `compileMethod`, uses a process-local
static lock around compilation. The corresponding native entry is in
`src/coreclr/jit/ee_il_dll.cpp`.

**Status:** confirmed temporary debugging aid, not a permanent concurrency design
or evidence that serialization is required for correctness.

The host can invoke the AltJit concurrently from multiple JIT threads. The lock
was added to keep those invocations from running in parallel while debugging.
It may be removed, excluded, or replaced with a more suitable debugging mechanism
when needed. Before claiming concurrent compilation support, review shared/TLS
state and validate parallel/reentrant compilation; the lock currently masks that
dimension of behavior.

### R002: Windows dump line endings

**Status:** output-path mismatch corrected by `JitTextWriter`, not waived as an
output exception. Full compiler dump parity remains outstanding.

Native `src/coreclr/jit/ee_il_dll.cpp`, `jitstdoutInit`, uses CRT text-mode
output on Windows, which translates LF to CRLF. The managed implementation in
`sources/Core/jit/ee_il_dll/Globals.cs` uses `StreamWriter`, and `jitprintf` in
`sources/Core/jit/host/Globals.cs` calls `Write(message)`. Embedded LF characters
are written unchanged. Setting `StreamWriter.NewLine` affects `WriteLine`, not
those embedded characters. A standalone Windows CRT/.NET probe confirmed
`probe\n` produces `70726F62650D0A` natively and `70726F62650A` through this managed
writer setup. This is a byte-level line-ending difference, not different IR or
generated instructions.

The shared writer now applies the native host's text-mode encoding below
`StreamWriter`, including embedded newlines and fragmented writes. Dump output,
function-info logging, and timing CSV writers use it. Sixteen focused cases pass
in Debug and Release on Windows, including explicit CR/LF, write overloads,
append behavior, and stream ownership. The initial six-method native/managed
corpus confirms that this change affects only newline encoding in the captured
managed dump. The Unix branch preserves LF; execution on Unix is not yet verified.

**Remaining action:** compare message layout as more functions become reachable;
the writer cannot restore newlines omitted at individual ported call sites.
Using managed output is not itself a defect. Culture/encoding fidelity still
needs comparison; no defect in either has been established by this investigation.

The earlier object-identity concern was too broad: the two `PendingDsc` hash-code
diagnostics in `Compiler.imp.cs` are both behind `if (false && verbose)`, matching
disabled native diagnostics in `importer.cpp`. They do not currently affect dump
output. `Compiler.dspOffset` also preserves the upstream nonzero
`0xD1FFAB1E` substitution in diffable mode. If those diagnostics are enabled later,
review their identity and pointer formatting then; they are not a demonstrated
active parity failure or justification for stripping hashes from comparisons.

## Incomplete implementation, not intentional deviations

| ID | Evidence at the recorded C# baseline | Required action |
| --- | --- | --- |
| G001 | `Compiler.comp.cs`, `compCompileHelper`/local `GetResult`, deliberately returns `CORJIT_SKIPPED` until codegen exists. | Separate phase validation from codegen success and native fallback. Return success only after real code and required metadata exist. |
| G002 | `Compiler.cs`, `Compiler.fg.cs`, and `Compiler.opt.cs` contain phase methods returning `MODIFIED_NOTHING` with port TODOs. | Treat each as a stub, not a verified no-op. Replace complete functions in dependency order. |
| G003 | `Compiler.imp.cs`, `impHWIntrinsic`, currently returns `null` under a port TODO. | Reconcile the full intrinsic contract, including `mustExpand`; ordinary-call fallback is not proof of parity. |
| G004 | The original baseline had an empty `tests/Core/RyuJitSharp.UnitTests.csproj`. | Focused output regression cases now exist. Do not infer compiler-wide coverage; prioritize dump/disassembly and runtime-test validation. |

These entries describe the unstashed baseline. Reassess the affected gaps when
the saved WIP is integrated; do not overwrite existing work based on this table.
