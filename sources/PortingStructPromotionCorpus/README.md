# Original struct-local promotion corpus

`TwoFields` reads a two-int struct returned by a non-inlined factory.
`MakeTriple` and `MakeDoublePair` construct three-int and two-double
structs, respectively. Their original locals are candidates for
`fgPromoteStructs`, not promotion temps added by a later phase.
`FourFieldCopy` passes a four-int value between non-inlined calls without
reading its fields; its spilled call-argument local is marked
do-not-enregister and is a no-promotion control. `ThreeFields` and
`FloatingFields` also have no promotion in the caller, although their
factories promote original locals. `Main` checks positive and negative
integer inputs, exact binary floating-point results, and both copy
outcomes before printing
`Struct promotion corpus: integer, floating, and copy results verified`.

Build and capture the native optimized bodies with the maintained runner
from the repository root. Use the pinned matching Windows x64 Checked
CoreRoot and a fresh output directory for each invocation:

```powershell
dotnet build sources\PortingStructPromotionCorpus\PortingStructPromotionCorpus.csproj -c Release -o artifacts\struct-promotion\corpus
$coreRoot = '<Core_Root built from the pinned native commit>'
$corpus = 'artifacts\struct-promotion\corpus\PortingStructPromotionCorpus.dll'
$methods = @('TwoFields', 'ThreeFields', 'FloatingFields', 'FourFieldCopy',
    'MakePair', 'MakeTriple', 'MakeDoublePair', 'MakeQuad', 'SumQuad')
& scripts\porting\Invoke-PortingCorpus.ps1 `
    -CoreRoot $coreRoot -Corpus $corpus `
    -OutputDirectory 'artifacts\struct-promotion\native-next' `
    -NativeCommit '33baf8ee337b20dd0f184b69a6f09be92850bf9e' `
    -TypeName 'RyuJitSharp.StructPromotionCases' -ExpectedMethods $methods
```

The pinned native run promotes exactly one original local in each of
`TwoFields`, `MakePair`, `MakeTriple`, `MakeDoublePair`, `MakeQuad`, and
`SumQuad`. In `TwoFields`, the `lvaTable` grows from struct V01 to int
field locals V04 and V05. `MakeTriple` adds three int field locals;
`MakeDoublePair` adds two double field locals. `ThreeFields`,
`FloatingFields`, and `FourFieldCopy` report `[no changes]` for this
phase. The no-change `FourFieldCopy` local is a spilled argument marked
do-not-enregister; **four fields alone are not a general suppression
rule** -- `MakeQuad` and `SumQuad` promote their four-field locals.
Compare the `lvaTable before/after fgPromoteStructs`, `Trees after Morph -
Promote Structs`, and emitted instructions in the raw `jitdump.txt`, not
just compilation headers or a successful build.

For a later managed comparison, invoke the same maintained runner with
a separate fresh `-OutputDirectory`, plus `-ManagedJit <published DLL>`,
`-ManagedSource <immutable source commit>`, and `-ExecuteManagedCode`.
Default optimized compilation is the evidence target; tiered execution
is unnecessary for this phase.
