# OSR patchpoint corpus

`HotLoop` exposes a live local's address before a million-iteration loop.
`ContextLoop<string>` runs a generic method that uses its type context. Both
return fixed results. These checks do not detect restarting initialization and
repeating the loop, so successful output alone does not establish correct OSR
resumption.

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

The metadata publisher matches the native Tier0 frames and slots. The initial
managed comparison compiles both Tier1-OSR variants but lacks native importer
entry redirection and repeats initialization. Correct carried-state resumption
remains unverified; the repeated work happens to produce the expected results.
