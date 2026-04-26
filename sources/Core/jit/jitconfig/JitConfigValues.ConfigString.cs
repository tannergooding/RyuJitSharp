// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.JitConfigValues.ConfigString;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    public enum ConfigString
    {
#if DEBUG
        /// <summary>LSRA heuristics ordering</summary>
        JitLsraOrdering,

        /// <summary></summary>
        JitInlineMethodsWithEHRange,

        /// <summary>Only apply JitStressRegs to methods in this hash range</summary>
        JitStressRegsRange,

        /// <summary>If set, sends late disassembly output to this file instead of stdout/JitStdOutFile.</summary>
        JitLateDisasmTo,

        /// <summary>Directory for Xml/Dot flowgraph dump(s)</summary>
        JitDumpFgDir,

        /// <summary>Filename for Xml/Dot flowgraph dump(s) (default: "default")</summary>
        JitDumpFgFile,

        /// <summary>
        ///     <para>Phase-based Xml/Dot flowgraph support. Set to the short name of a phase to see the flowgraph after that phase.</para>
        ///     <para>Leave unset to dump after COLD-BLK (determine first cold block) or set to * for all phases</para>
        /// </summary>
        JitDumpFgPhase,

        /// <summary>Same as JitDumpFgPhase, but specifies to dump pre-phase, not post-phase.</summary>
        JitDumpFgPrePhase,

        /// <summary></summary>
        JitRange,

        /// <summary>
        ///     <para>Internal Jit stress mode: stress using the given set of stress mode names, e.g. STRESS_REGS, STRESS_TAILCALL.</para>
        ///     <para>Unless JitStressModeNamesOnly is non-zero, other stress modes from a JitStress setting may also be invoked.</para>
        /// </summary>
        JitStressModeNames,

        /// <summary>
        ///     <para>Internal Jit stress mode: only allow stress using the given set of stress mode names, e.g. STRESS_REGS, STRESS_TAILCALL.</para>
        ///     <para>Note that JitStress must be enabled first, and then only the mentioned stress modes are allowed to be used, at the same percentage weighting as with JitStress -- the stress modes mentioned are NOT unconditionally true for a call to `compStressCompile`.</para>
        ///     <para>This is basically the opposite of JitStressModeNamesNot.</para>
        /// </summary>
        JitStressModeNamesAllow,

        /// <summary>Internal Jit stress mode: do NOT stress using the given set of stress mode names, e.g. STRESS_REGS, STRESS_TAILCALL</summary>
        JitStressModeNamesNot,

        /// <summary>Internal Jit stress mode</summary>
        JitStressRange,

        /// <summary>Generate this set of unit tests</summary>
        JitEmitUnitTestsSections,

        /// <summary>Only show JitDisasm and related info for methods from this semicolon-delimited list of assemblies.</summary>
        JitDisasmAssemblies,
#endif

        /// <summary>If set, sends JIT's stdout output to this file.</summary>
        JitStdOutFile,

#if DEBUG
        /// <summary>Write rich debug info in JSON format to this file</summary>
        WriteRichDebugInfoFile,

        /// <summary>When set, specifies the exact CSEs to perform as a sequence of CSE candidate numbers.</summary>
        JitReplayCSE,

        /// <summary>When set, specify the sequence of rewards from the CSE replay. There should be one reward per step in the sequence.</summary>
        JitReplayCSEReward,

        /// <summary>
        ///     <para>When set, specifies the initial parameter string for the reinforcement-learning based CSE heuristic.</para>
        ///     <para>Note you can also set JitReplayCSE and JitReplayCSEPerfScore along with this, in which case we are asking for a policy evaluation/update based on the provided sequence.</para>
        /// </summary>
        JitRLCSE,

        /// <summary>When set, specify the alpha value (step size) to use in learning.</summary>
        JitRLCSEAlpha,

        /// <summary>A list of CSEs to choose, in the order they should be applied.</summary>
        JitRLHookCSEDecisions,
#endif

#if OPT_CONFIG
        /// <summary></summary>
        JitEnableRboRange,

        /// <summary></summary>
        JitEnableHeadTailMergeRange,

        /// <summary></summary>
        JitEnableVNBasedDeadStoreRemovalRange,

        /// <summary></summary>
        JitEnableEarlyLivenessRange,

        /// <summary>If set, all methods that do _not_ match are forced into MinOpts</summary>
        JitOnlyOptimizeRange,

        /// <summary></summary>
        JitEnablePhysicalPromotionRange,

        /// <summary></summary>
        JitEnableCrossBlockLocalAssertionPropRange,

        /// <summary></summary>
        JitEnableInductionVariableOptsRange,

        /// <summary></summary>
        JitEnableLocalAddrPropagationRange,

        /// <summary>Enable JitOptRepeat based on method hash range</summary>
        JitOptRepeatRange,

        /// <summary>Enable async default value analysis based on method hash range</summary>
        JitAsyncDefaultValueAnalysisRange,

        /// <summary>
        ///     <para>Enable async preserved value analysis based on method hash range.</para>
        ///     <para>This analysis computes state that is guaranteed to not have been changed since the last time suspension happened, and skips storing them in the case where a continuation is being reused.</para>
        /// </summary>
        JitAsyncPreservedValueAnalysisRange,

        /// <summary>Enable continuation reuse based on method hash range</summary>
        JitAsyncReuseContinuationsRange,
#endif

        /// <summary>Do not use AltJit on this semicolon-delimited list of assemblies.</summary>
        AltJitExcludeAssemblies,

        /// <summary>If set, gather JIT function info and write to this file.</summary>
        JitFuncInfoFile,

        /// <summary>If set, gather JIT throughput data and write to a CSV file. This mode must be used in internal retail builds.</summary>
        JitTimeLogCsv,

        /// <summary>If set, gather JIT throughput data and write to this file.</summary>
        JitTimeLogFile,

        /// <summary></summary>
        TailCallOpt,

        /// <summary>Set to file to write noway_assert usage to a file (if not set: stdout). Only valid if MEASURE_NOWAY is defined.</summary>
        JitMeasureNowayAssertFile,

#if DEBUG
        /// <summary></summary>
        JitInlineDumpXmlFile,

        /// <summary></summary>
        JitNoInlineRange,

        /// <summary></summary>
        JitInlineReplayFile,

        /// <summary></summary>
        JitObjectStackAllocationRange,

        /// <summary></summary>
        JitObjectStackAllocationConditionalEscapeRange,

        /// <summary></summary>
        JitObjectStackAllocationTrackFieldsRange,

        /// <summary></summary>
        JitGuardedDevirtualizationRange,

        /// <summary>
        ///     <para>EnableOsrRange allows you to limit the set of methods that will rely on OSR to escape from Tier0 code.</para>
        ///     <para>Methods outside the range that would normally be jitted at Tier0 and have patchpoints will instead be switched to optimized.</para>
        /// </summary>
        JitEnableOsrRange,

        /// <summary>
        ///     <para>EnablePatchpointRange allows you to limit the set of Tier0 methods that will have patchpoints, and hence control which methods will create OSR methods.</para>
        ///     <para>Unlike EnableOsrRange, it will not alter the optimization setting for methods outside the enabled range.</para>
        /// </summary>
        JitEnablePatchpointRange,

        /// <summary></summary>
        JitInstrumentIfOptimizingRange,

        /// <summary>Enable PGO data for only some methods</summary>
        JitEnablePGORange,

        /// <summary>Weight for exception regions for synthesis</summary>
        JitSynthesisExceptionWeight,

        /// <summary>
        ///     <para>Name of a file that contains a list of functions.</para>
        ///     <para>If the currently compiled function is in the file, certain other JIT config variables will be active.</para>
        ///     <para>If the currently compiled function is not in the file, the specific JIT config variables will not be active.</para>
        ///     <para>Functions are approximately in the format output by JitFunctionTrace, e.g.:</para>
        ///     <list type="bullet">
        ///         <item>System.CLRConfig:GetBoolValue(ref,byref):bool (MethodHash=3c54d35e) -- use the MethodHash, not the function name</item>
        ///         <item>System.CLRConfig:GetBoolValue(ref,byref):bool -- use just the name</item>
        ///     </list>
        ///     <para>Lines with leading ";" "#" or "//" are ignored.</para>
        ///     <para>If this is unset, then the JIT config values have their normal behavior.</para>
        /// </summary>
        JitFunctionFile,

        /// <summary></summary>
        JitRawHexCodeFile,
#endif

#if DEBUG && TARGET_WASM
        /// <summary>
        ///     <para>Specify methods that will fail with R2R unsupported after codegen.</para>
        ///     <para>Useful for bypassing methods that compile cleanly but have invalid Wasm codegen.</para>
        /// </summary>
        JitR2RUnsupportedRange,
#endif
    }

    private static readonly unsafe FrozenDictionary<ConfigString, nuint> ConfigStringMetadata = new Dictionary<ConfigString, nuint> {
#if DEBUG
        [JitLsraOrdering] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraOrdering"u8)),
        [JitInlineMethodsWithEHRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEHRange"u8)),
        [JitStressRegsRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegsRange"u8)),
        [JitLateDisasmTo] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasmTo"u8)),
        [JitDumpFgDir] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDir"u8)),
        [JitDumpFgFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgFile"u8)),
        [JitDumpFgPhase] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPhase"u8)),
        [JitDumpFgPrePhase] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPrePhase"u8)),
        [JitRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRange"u8)),
        [JitStressModeNames] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNames"u8)),
        [JitStressModeNamesAllow] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesAllow"u8)),
        [JitStressModeNamesNot] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesNot"u8)),
        [JitStressRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRange"u8)),
        [JitEmitUnitTestsSections] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTestsSections"u8)),
        [JitDisasmAssemblies] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmAssemblies"u8)),
#endif

        [JitStdOutFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStdOutFile"u8)),

#if DEBUG
        [WriteRichDebugInfoFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("WriteRichDebugInfoFile"u8)),
        [JitReplayCSE] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSE"u8)),
        [JitReplayCSEReward] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSEReward"u8)),
        [JitRLCSE] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSE"u8)),
        [JitRLCSEAlpha] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEAlpha"u8)),
        [JitRLHookCSEDecisions] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookCSEDecisions"u8)),
#endif

#if OPT_CONFIG
        [JitEnableRboRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRboRange"u8)),
        [JitEnableHeadTailMergeRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMergeRange"u8)),
        [JitEnableVNBasedDeadStoreRemovalRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableVNBasedDeadStoreRemovalRange"u8)),
        [JitEnableEarlyLivenessRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableEarlyLivenessRange"u8)),
        [JitOnlyOptimizeRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOnlyOptimizeRange"u8)),
        [JitEnablePhysicalPromotionRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotionRange"u8)),
        [JitEnableCrossBlockLocalAssertionPropRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionPropRange"u8)),
        [JitEnableInductionVariableOptsRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOptsRange"u8)),
        [JitEnableLocalAddrPropagationRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLocalAddrPropagationRange"u8)),
        [JitOptRepeatRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeatRange"u8)),
        [JitAsyncDefaultValueAnalysisRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncDefaultValueAnalysisRange"u8)),
        [JitAsyncPreservedValueAnalysisRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncPreservedValueAnalysisRange"u8)),
        [JitAsyncReuseContinuationsRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncReuseContinuationsRange"u8)),
#endif

        [AltJitExcludeAssemblies] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitExcludeAssemblies"u8)),
        [JitFuncInfoFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFuncInfoLogFile"u8)),
        [JitTimeLogCsv] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitTimeLogCsv"u8)),
        [JitTimeLogFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitTimeLogFile"u8)),
        [TailCallOpt] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TailCallOpt"u8)),
        [JitMeasureNowayAssertFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureNowayAssertFile"u8)),

#if DEBUG
        [JitInlineDumpXmlFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXmlFile"u8)),
        [JitNoInlineRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInlineRange"u8)),
        [JitInlineReplayFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineReplayFile"u8)),
        [JitObjectStackAllocationRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRange"u8)),
        [JitObjectStackAllocationConditionalEscapeRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscapeRange"u8)),
        [JitObjectStackAllocationTrackFieldsRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFieldsRange"u8)),
        [JitGuardedDevirtualizationRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRange"u8)),
        [JitEnableOsrRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOsrRange"u8)),
        [JitEnablePatchpointRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePatchpointRange"u8)),
        [JitInstrumentIfOptimizingRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizingRange"u8)),
        [JitEnablePGORange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePGORange"u8)),
        [JitSynthesisExceptionWeight] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisExceptionWeight"u8)),
        [JitFunctionFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionFile"u8)),
        [JitRawHexCodeFile] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCodeFile"u8)),
#endif

#if DEBUG && TARGET_WASM
        [JitR2RUnsupportedRange] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitR2RUnsupportedRange"u8)),
#endif
    }.ToFrozenDictionary();
}
