# Post-inline no-return corpus

`NoReturnCases.DirectNested` calls `Record` before `ThrowComplex` inside a
return expression. On the pinned native JIT, examining the inline candidate
identifies `ThrowComplex` as no-return, but the inline is unprofitable.
Post-inline cleanup then preserves the preceding `Record` call, removes the
expression after the no-return call, and converts the block to `BBJ_THROW`.
`Main` executes the method and checks that the expected exception and side
effect occur. `Root`, `Nested`, and `Conditional` exercise an aggressively
inlined guard without that phase transformation; `Record` and `ThrowComplex`
are additional selected bodies. Minopts compiles all eight methods, including
`InlineGuard` and `Throw`, but skips the phase.

Build with
`dotnet build sources\PortingNoReturnCorpus\PortingNoReturnCorpus.csproj -c Release -o artifacts\post-inline-no-return\corpus`.
Use `scripts\porting\Invoke-PortingCorpus.ps1` with a matching `-CoreRoot`,
`-Corpus artifacts\post-inline-no-return\corpus\PortingNoReturnCorpus.dll`,
`-NativeCommit 33baf8ee337b20dd0f184b69a6f09be92850bf9e`,
`-TypeName RyuJitSharp.NoReturnCases`,
`-ExpectedMethods Root,Nested,Conditional,DirectNested,Record,ThrowComplex`,
`-RawHexCode`, and a fresh `-OutputDirectory`.
For the minopts control, add `-MinOpts` and include `InlineGuard,Throw` in
`-ExpectedMethods`, using a separate output directory.

Check `DirectNested` for the positive cleanup message, the return-to-throw CFG
change, and emitted calls to `Record` before `ThrowComplex` without subsequent
arithmetic. The corpus must print
`Post-inline no-return corpus: inline throws and preceding effects verified`.
Minopts skips this phase; its successful execution is a control, not positive
cleanup evidence.

For a managed comparison, also pass `-ManagedJit <published DLL>`,
`-ManagedSource <immutable-source-commit>`, and `-ExecuteManagedCode`.
The runner records input hashes. A managed DLL without `-ExecuteManagedCode`
does not establish execution of managed-generated code.
