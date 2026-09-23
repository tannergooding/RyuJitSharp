# Port findings and post-port work

Preserve actionable findings without turning the clean port into a rewrite.
Record a stable ID, category, location/evidence, impact, proposed action, and
disposition. Distinguish confirmed defects from hypotheses.

Fix translation defects that block correctness or parity in the relevant batch.
Record suspected upstream bugs and ask before introducing an intentional C#-only
behavior change. Defer unrelated renames, refactoring, and restructuring. Link
existing deviation entries rather than maintaining competing descriptions.

| ID | Category | Evidence and impact | Proposed action | Disposition |
| --- | --- | --- | --- | --- |
| B001 | Port correctness | [R002](DEVIATIONS.md#r002-windows-dump-line-endings): Windows output paths produced different raw line endings. | Centralize host newline encoding; preserve native message layout and check embedded/fragmented writes. | Corrected; 16 focused cases in Debug/Release and raw before/after dump comparison. Unix execution remains unverified. |
| B002 | Debugging design | [R001](DEVIATIONS.md#r001-temporary-serialization-for-debugging): `CILJit.compileMethod` serializes all compilations for debugger usability. | Remove, exclude, or make debugging serialization opt-in when useful; verify concurrency before claiming support. | Deferred until needed; not a permanent design requirement. |
| B003 | Tooling maintenance | `GenerateTables.Program.Main` uses working-directory-relative inputs and deletes its relative `Outputs` directory. | Consider explicit input/output paths and a reproducibility check when generator maintenance requires it; retain native input provenance. | Candidate, not a prerequisite for the clean port. |
| B004 | Port correctness | `Compiler.compInitOptions` initialized `verboseDump` to `true`, unlike native `compiler.cpp`. A six-method corpus produced 719 compilation headers, including 713 unselected methods. | Restore the native `false` default and reject unselected headers in the comparison runner. | Corrected; the runner rejects the old binary's 713 extra headers and accepts exactly six with the fix. |
| B005 | Port correctness / upstream synchronization | `Globals.dumpSingleInstr` formatted `ShortInlineI` as `sbyte`, so old native `ldc.i4.s -2` rendered `0xFFFFFFFE` but C# rendered `0xFE`. Target native commit `6276bd4a23e` fixes the native format from `%X` to `%llX`, making the target output `0xFFFFFFFFFFFFFFFE`. | Sign-extend both short and 32-bit signed integer operands to `long` before formatting; retain full-width `InlineI8`. | Target behavior confirmed by compiling the pinned native dumper in an isolated probe. Managed fix and boundary cases await the target-sync build. |
| B006 | Dump fidelity | Native floating operands use `%f`, whereas the port used default floating formatting. The pinned native dumper probe confirms six fractional digits, widening of single operands, signed zero, and Windows CRT spellings including `-nan(ind)` and `nan(snan)`. The suspected 64-bit integer truncation was an upstream bug already fixed by the target revision; see B005. | Match finite formatting with invariant culture and preserve host-specific non-finite spellings. | Managed fix and focused cases await the target-sync build; native evidence is an isolated function probe, not full pipeline parity. |
| B007 | Port correctness | `Globals.dumpSingleInstr` computed the byte-display length before advancing over variable-length switch/phi operands. A two-target switch displayed only its opcode instead of all 13 bytes. | Compute display length after consuming the operand table; retain the native returned instruction length. | Native switch output confirmed in the pinned dumper probe. Managed fix and empty/nonempty switch cases await the target-sync build. |

Larger module decomposition and broad unit-test infrastructure belong to the
post-port phase. Add concrete candidates here as evidence appears, rather than
preemptively planning a redesign of every compiler subsystem.
