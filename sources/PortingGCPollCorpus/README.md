# GC-poll corpus

`GCPollCases` exercises Windows x64 direct P/Invoke to `kernel32!GetCurrentProcessId`. The entry point is called both with and without `SuppressGCTransition`; `Main` verifies its result against the current process ID. Only `GCPollCases` is selected for JIT dumps, so the runtime assertions in `Main` do not add selected methods.

| Method | Native optimized / GC stress | Native minopts |
| --- | --- | --- |
| `InlinePoll` | Inline poll on the suppressed-call path | Callout poll |
| `CalloutPoll` | Inline poll | Callout poll |
| `MixedCalls` | Regular unmanaged call eliminates the explicit poll | Callout poll (minopts uses block flags) |
| `EhCallout` | Inline poll in a try/finally method | Callout poll |

Build with `dotnet build sources\PortingGCPollCorpus\PortingGCPollCorpus.csproj -c Release -o artifacts\gc-poll-insertion\corpus`.

Use `scripts\porting\Invoke-PortingCorpus.ps1` with a matching `-CoreRoot`,
`-Corpus artifacts\gc-poll-insertion\corpus\PortingGCPollCorpus.dll`,
`-NativeCommit 33baf8ee337b20dd0f184b69a6f09be92850bf9e`,
`-TypeName RyuJitSharp.GCPollCases`,
`-ExpectedMethods InlinePoll,CalloutPoll,MixedCalls,EhCallout`, `-RawHexCode`,
and a fresh `-OutputDirectory`. Repeat with `-MinOpts` and `-GcStress`,
using separate output directories. Check the successful process-ID output,
generated bodies and poll selections listed above.

For a new managed JIT DLL, also provide `-ManagedJit <path>`,
`-ManagedSource <immutable-source-commit>` and `-ExecuteManagedCode`.
The runner records DLL and corpus hashes in each manifest. Without
`-ExecuteManagedCode`, supplying a managed JIT does not prove execution of
managed-generated code. Matching poll selections do not establish full native
dump or machine-code parity.
