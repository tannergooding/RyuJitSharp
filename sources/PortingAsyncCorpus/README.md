# Runtime-async transformation corpus

This corpus uses the SDK 11 compiler feature `<Features>$(Features);runtime-async=on</Features>`, as in the pinned runtime's `src/tests/async/Directory.Build.props`. The method-level `RuntimeAsyncMethodGenerationAttribute` has the same namespace, constructor, and property as the pinned test attribute. A source-level `async` method alone does not establish JIT-native async transformation: check the native `RyuJitSharp.AsyncCases:<method>(...):int` body for a modified `Transform async` phase, an assigned state, and a suspension block. The corresponding `Task<int>`-returning entry has no such phase.

From the repository root, build with `dotnet build sources\PortingAsyncCorpus\PortingAsyncCorpus.csproj -c Release`. Run `artifacts\bin\sources\PortingAsyncCorpus\Release\net11.0\PortingAsyncCorpus.dll` with the pinned Windows x64 checked CoreRoot's `corerun.exe`, setting `DOTNET_ReadyToRun=0`, `DOTNET_TieredCompilation=0`, and `DOTNET_JitDump=RyuJitSharp.AsyncCases:*` in an isolated process. The output must be exactly `Native async corpus: suspension, tail await, exception, and context verified` with exit code 0; the program rejects prematurely completed awaits and incorrect results.

`Suspend` checks suspension/resumption; `Catch` checks an exception across an await; `Context` checks restoration of the caller's `AsyncLocal<int>` after the continuation changes it. `TailAwait` awaits the non-async `TailVersion` wrapper, whose async version also transforms. **The original pinned full-optimization dump classifies all five awaits as normal, not tail awaits.** A source-level `return await` is not evidence that `TransformTailAwaits` ran.

`InlinedTailAwait` separately awaits the aggressively inlined, non-async `InlineTailVersion` wrapper around `Suspend`. Its Task-returning call suspends until the independently gated input completes, then returns 40. With `DOTNET_TieredCompilation=1`, the pinned native JIT compiles `InlineTailVersion(Task<int>):int` as a **Tier0** async version with `Found 1 tail awaits in 1 blocks`, `Transforming tail await`, and `Creating tail suspension`. `InlinedTailAwait(Task<int>):int` remains a normal await; the positive tail transformation is in the wrapper's async version, not inferred from method naming. For the same DLL with `DOTNET_TieredCompilation=0`, all six async-version awaits remain normal.

For deterministic comparisons of tiered helper-call forms, set
`DOTNET_JitEnableOptionalRelocs=0` in both processes. Dynamic-PGO inputs can
still change call order; disable `DOTNET_TieredPGO` as well when comparing
those paths. The corpus verifies runtime results and context restoration, but
does not establish whole-dump, arbitrary-PGO, or raw-code-byte parity. To
exercise GC stress, add `DOTNET_GCStress=4` to the FullOpts configuration.
