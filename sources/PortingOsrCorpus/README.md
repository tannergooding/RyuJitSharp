# OSR patchpoint corpus

`HotLoop` exposes a live local's address before a million-iteration loop.
`ContextLoop<string>` runs a generic method that uses its type context. Both
return fixed results and independently count their one-time initialization:
`Touch` must execute once before `HotLoop`, and `InitializeContext` must execute
once before `ContextLoop`. A Tier1-OSR body that restarts the original entry
increments either counter twice, even if recomputing the loop happens to yield
the expected final sums. On a mismatch, stderr reports both results and counts.

Build with `dotnet build sources\PortingOsrCorpus\PortingOsrCorpus.csproj -c Release`.
Use the pinned Windows x64 Checked CoreRoot and native JIT at
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Enable tiering,
`TC_QuickJitForLoops`, on-stack replacement, and a low initial OSR counter.
Run the built
`artifacts\bin\sources\PortingOsrCorpus\Release\net11.0\PortingOsrCorpus.dll`
with that CoreRoot's `corerun.exe` in a child process after removing inherited
`DOTNET_` and `COMPlus_` settings. Set:

| Variable | Value |
| --- | --- |
| `DOTNET_ReadyToRun` | `0` |
| `DOTNET_TieredCompilation` | `1` |
| `DOTNET_TieredPGO` | `0` |
| `DOTNET_TC_QuickJitForLoops` | `1` |
| `DOTNET_TC_OnStackReplacement` | `1` |
| `DOTNET_TC_OnStackReplacement_InitialCounter` | `10` |
| `DOTNET_JitDump`, `DOTNET_JitDisasm` | `RyuJitSharp.OsrCases:*` |
| `DOTNET_JitDumpASCII`, `DOTNET_JitDisasmDiffable` | `1` |
| `DOTNET_JitStdOutFile` | A new dump path |

The initial-counter setting `10` yielded a counter value of **16** in the
pinned native Tier0 disassembly; do not interpret the setting as decimal ten.
The exact capture, hashes, raw dump, stdout, and stderr are recorded by the
artifact-only `artifacts\osr-patchpoint\capture.ps1`. Always use a fresh
output directory; do not substitute an ordinary optimized Tier1 body.

Successful output is exactly `OSR corpus: hot loop and generic context verified`.
For positive evidence, require a Tier0 patchpoint's `--OSR--- Total Frame Size`,
virtual local offsets and Tier0 callee-save register mask, **and** a separately
compiled OSR body for the same method. An ordinary optimized Tier1 compilation
or successful output alone is not OSR evidence. Special offsets are recorded
when the native JIT reports them; no offset is inferred from an absent marker.

The initial, unstrengthened corpus and its captures remain under
`artifacts\osr-patchpoint\native-2` and `managed-01b67-1` (DLL SHA-256
`A67AE2A1E074B460AAFB23604A891E24DF906FCC407DA54BBEAEB32D599A3462`).
The metadata publisher matched native Tier0 frames and slots, but its OSR
variants restarted initialization. Use a fresh capture for this strengthened
corpus; do not interpret the earlier successful execution as resumption parity.
The strengthened native reference is
`artifacts\osr-patchpoint\native-strengthened-1`; the single old-managed
negative control is `artifacts\osr-patchpoint\managed-old-01b67-strengthened-1`.
Both used DLL SHA-256 `D2CDBEAEB79DBEF1BEBE95E934672945B88F74F7CBD819AC72954130D0573159`;
`artifacts\osr-patchpoint\comparison-strengthened-1.json` records the exact
observed result and OSR selections.

Snapshot `3cbe4fa23f4128f3640b923623a3b055f5752c8d` passes both
once-only checks and emits native-matching Tier1-OSR instructions for this
corpus. Its capture is `artifacts\osr-patchpoint\managed-entry-3cbe-1`;
`comparison-entry-3cbe-1.json` records the remaining Tier0 operand-printing
and OSR phase-status differences. These differences are not normalized away.

Snapshot `c1e8cbf8bc2795baa1669a5cdc0ed4b68af1c2d1` corrects the counter
address type and native functor phase-status contract. Its fresh capture is
`artifacts\osr-patchpoint\managed-contracts-c1e8-1`;
`comparison-contracts-c1e8-1.json` records matching phase boundaries, statuses
and emitted instruction lines for all six bodies, with both initialization
counts remaining one. Raw whole dumps still differ at the deferred
profile-check diagnostics; this is not whole-dump or raw-code-byte parity.
