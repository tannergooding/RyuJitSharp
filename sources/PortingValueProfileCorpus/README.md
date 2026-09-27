# Value-profile instrumentation corpus

`Copy` and `Equal` call the span copy and sequence-equality APIs with variable
lengths. `Main` checks empty, single-byte, short and full-buffer copies, detects
mismatches, then warms both methods over lengths 1 through 64. The expected output
is `Value profile corpus: copy lengths and equality results verified`.

The value probes require optimized instrumentation: ordinary Tier0 leaves calls
to the span wrappers, while optimized instrumentation inlines them and exposes
the `SpanHelpers` calls that carry value-profile metadata. On native
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`, `Copy` produces two count and two
value schema entries; `Equal` produces four count and two value entries.
Both contain `CORINFO_HELP_VALUEPROFILE32` trees. Their instrumented Tier1 bodies
are 106 and 101 bytes, respectively. The integrated managed phase executes
these tiers with matching probe schemas and emitted instruction sequences.

Build from the repository root:

```powershell
dotnet build sources\PortingValueProfileCorpus\PortingValueProfileCorpus.csproj `
    -c Release -o artifacts\value-profile-corpus
```

For the eager Tier0 no-change control, use the maintained runner with the
matching Checked Windows x64 CoreRoot:

```powershell
$coreRoot = '<Core_Root built from the pinned native commit>'
$corpus = (Resolve-Path 'artifacts\value-profile-corpus\PortingValueProfileCorpus.dll').Path
& scripts\porting\Invoke-PortingCorpus.ps1 `
    -CoreRoot $coreRoot -Corpus $corpus `
    -OutputDirectory 'artifacts\value-profile-tier0-next' `
    -NativeCommit '33baf8ee337b20dd0f184b69a6f09be92850bf9e' `
    -TypeName 'RyuJitSharp.ValueProfileCases' -ExpectedMethods Copy,Equal `
    -InstrumentedTier0
```

For positive native evidence, enable optimized instrumentation of hot methods.
This is a multi-tier run, so the single-compilation-per-method runner is not
appropriate. The following invocation isolates JIT settings and retains the
complete dump rather than selecting only the final version of each method:

```powershell
$output = (New-Item -ItemType Directory 'artifacts\value-profile-optimized-next').FullName
$settings = @{
    DOTNET_ReadyToRun = '0'
    DOTNET_TieredCompilation = '1'
    DOTNET_TieredPGO = '1'
    DOTNET_TieredPGO_InstrumentOnlyHotCode = '1'
    DOTNET_TieredPGO_InstrumentedTierAlwaysOptimized = '1'
    DOTNET_TC_CallCountingDelayMs = '0'
    DOTNET_TC_QuickJitForLoops = '1'
    DOTNET_JitDump = 'RyuJitSharp.ValueProfileCases:*'
    DOTNET_JitDumpASCII = '1'
    DOTNET_JitDisasmDiffable = '1'
    DOTNET_JitStdOutFile = (Join-Path $output 'jitdump.txt')
}
$saved = @(Get-ChildItem Env: | Where-Object { $_.Name -match '^(DOTNET_|COMPlus_)' })
try {
    foreach ($item in $saved) {
        Remove-Item -LiteralPath ("Env:" + $item.Name)
    }
    foreach ($setting in $settings.GetEnumerator()) {
        Set-Item -LiteralPath ("Env:" + $setting.Key) -Value $setting.Value
    }
    & (Join-Path $coreRoot 'corerun.exe') $corpus
    if ($LASTEXITCODE -ne 0) {
        throw 'Value-profile corpus failed'
    }
}
finally {
    foreach ($item in @(Get-ChildItem Env: | Where-Object { $_.Name -match '^(DOTNET_|COMPlus_)' })) {
        Remove-Item -LiteralPath ("Env:" + $item.Name)
    }
    foreach ($item in $saved) {
        Set-Item -LiteralPath ("Env:" + $item.Name) -Value $item.Value
    }
}
```

Require the expected output, both positive schema counts, the helper calls and
successful instrumented Tier1 compilation. Warmup timing can change how many
later versions are compiled; a successful initial Tier0 body alone is not
positive value-probe evidence. Keep the selected JIT, corpus and runtime hashes
with the dump. Managed comparisons additionally require the exact AltJIT
selection, `DOTNET_RunAltJitCode=1`, and evidence excluding compilation retries
or native fallback.
