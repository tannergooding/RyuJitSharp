# Cold-section selection corpus

`ColdSectionCases.ColdThrow` has a hot return and two distinct, rarely run
throw blocks at the end of the optimized layout. The two cold blocks let
`fgDetermineFirstColdBlock` select the first even when a single trailing
block would fail its code-estimate threshold. The entry point checks the hot
result and both exception messages.

Build with `dotnet build sources\PortingColdSectionCorpus\PortingColdSectionCorpus.csproj -c Release`.
Copy the resulting DLL to an immutable evidence path before rebuilding it.
Use `scripts\porting\Invoke-PortingCorpus.ps1` with a matching `-CoreRoot`,
`-Corpus <immutable-corpus-DLL>`,
`-NativeCommit 33baf8ee337b20dd0f184b69a6f09be92850bf9e`,
`-TypeName RyuJitSharp.ColdSectionCases`, `-ExpectedMethods ColdThrow`,
`-RawHexCode`, and a fresh `-OutputDirectory`. Repeat with
`-FakeProcedureSplitting`, then with both `-FakeProcedureSplitting` and
`-StressProcedureSplitting`, using separate output directories.

At that pin, the default checked-JIT run disables splitting; fake splitting
selects `BB02` and emits 92 cold bytes; forced stress selects `BB03` and emits
105 cold bytes. Check the phase selections, emitted cold-code boundary and
successful output `Cold section corpus: hot result and cold exception verified`.
For a managed comparison, additionally pass `-ManagedJit`,
`-ManagedSource <immutable-source-commit>`, and `-ExecuteManagedCode`.
The runner records the input hashes; a managed DLL without
`-ExecuteManagedCode` does not prove execution of managed-generated code.

The separate `PortingGCPollCorpus` covers an EH selection veto: run its
`EhCallout` method with `-FakeProcedureSplitting -StressProcedureSplitting`,
then add `-DisableProcedureSplittingEH` and confirm the phase reports the
explicit EH gate rather than selecting a cold block.

`JitFakeProcedureSplitting` exercises cold-section selection but places hot
and cold code in one contiguous allocation in the pinned native oracle; it
does not validate genuine discontiguous code allocation.
