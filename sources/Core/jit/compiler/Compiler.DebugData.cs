// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

[System.Flags]
internal enum LocalDebugFlags : ushort
{
    None = 0,
    TrackedWithoutIndex = 1 << 0,
    ClassInfoUpdated = 1 << 1,
    IsHoist = 1 << 2,
    IsMultiDefCse = 1 << 3,
    KeepType = 1 << 4,
    NoLclFldStress = 1 << 5,
    DefinedViaAddress = 1 << 6,
    UnusedStruct = 1 << 7,
    UndoneStructPromotion = 1 << 8,
}

[System.Flags]
internal enum LocalDebugDataCopyFlags : byte
{
    None = 0,
    DoNotEnregisterReason = 1 << 0,
    AddressExposedReason = 1 << 1,
    KeepType = 1 << 2,
    DefinedViaAddress = 1 << 3,
    SingleDefDisqualifyReason = 1 << 4,
}

public partial class Compiler
{
    internal sealed partial class DebugData
    {
        internal static bool s_didComponentUnitTests;

        private static ConditionalWeakTable<Compiler, DebugData>? s_instances;
        private static ConditionalWeakTable<JitTls, JitTlsDebugData>? s_jitTlsInstances;

        private static ConditionalWeakTable<CodeGen, CodeGenDebugData>? s_codeGens;
        private static ConditionalWeakTable<TreeLifeUpdater, TreeLifeUpdaterDebugData>? s_treeLifeUpdaters;
        private static ConditionalWeakTable<
            CodeGen.VariableLiveKeeper.VariableLiveDescriptor,
            VariableLiveDumperDebugData>? s_variableLiveDumpers;
        private static ConditionalWeakTable<IncrementalSsaBuilder, IncrementalSsaBuilderDebugData>? s_incrementalSsaBuilders;
        private static ConditionalWeakTable<Emitter, EmitterDebugData>? s_emitters;
        private static ConditionalWeakTable<FlowEdge, FlowEdgeDebugData>? s_flowEdges;
        private static ConditionalWeakTable<GenTree, GenTreeDebugData>? s_genTrees;
        private static ConditionalWeakTable<insGroup, InstructionGroupDebugData>? s_instructionGroups;
        private static ConditionalWeakTable<AddCodeDsc, AddCodeDscDebugData>? s_addCodeDscs;
        private static ConditionalWeakTable<Statement, StatementDebugData>? s_statements;
        private static ConditionalWeakTable<BasicBlock, BasicBlockDebugData>? s_basicBlocks;
        private static ConditionalWeakTable<NaturalLoopIterInfo, NaturalLoopIterInfoDebugData>? s_naturalLoopIterInfos;
        private static ConditionalWeakTable<InlineCandidateInfo, InlineCandidateInfoDebugData>? s_inlineCandidates;
        private static ConditionalWeakTable<InlineContext, InlineContextDebugData>? s_inlineContexts;
        private static ConditionalWeakTable<AsyncStressPolicy, AsyncStressPolicyDebugData>? s_asyncStressPolicies;
        private static ConditionalWeakTable<ReplayPolicy, ReplayPolicyDebugData>? s_replayPolicies;
        private static ConditionalWeakTable<UnwindInfo, UnwindInfoDebugData>? s_unwindInfos;
        private static ConditionalWeakTable<ClassLayout, ClassLayoutDebugNames>? s_classLayouts;
        private static ConditionalWeakTable<ScevAddRec, ScevAddRecDebugData>? s_scevAddRecs;
        private static ConditionalWeakTable<RefPosition, RefPositionDebugData>? s_refPositions;
        private static ConditionalWeakTable<LinearScan, LinearScanAllocationDebugData>? s_linearScans;
        private static ConditionalWeakTable<
            LinearScan.RegisterSelection,
            LinearScanRegisterSelectionDebugData<LinearScan.RegisterScore>>? s_registerSelections;
        private static ConditionalWeakTable<CSE_HeuristicCommon, CSEHeuristicDebugData>? s_cseHeuristics;

        [ThreadStatic]
        private static JitTls? t_currentJitTls;

        internal static JitTls? CurrentJitTls
        {
            get
            {
                return t_currentJitTls;
            }

            set
            {
                t_currentJitTls = value;
            }
        }

        internal struct JitTlsImporterDebugState
        {
            internal int CurrentOpcodeOffset;
            internal string? CurrentOpcodeName;
            internal bool NestedStackSpill;
            internal Statement? PendingStatement;
        }

        internal sealed class JitTlsDebugData
        {
            internal Compiler? Compiler;
            internal int CompGenTreeID;
            internal LogEnv LogEnv;
            internal JitTlsImporterDebugState ImporterDebugState;
            internal JitTls? Next;
        }

        internal static class InlineXmlDumpData
        {
            private static Lock? s_writerLock;

            internal static bool HasDumpedDataHeader;
            internal static bool HasDumpedXmlHeader;
            internal static Lock WriterLock => LazyStatic.GetOrCreate(ref s_writerLock);
        }

        internal static class InlineReplayData
        {
            private static Lock? s_xmlReaderLock;

            internal static bool WroteReplayBanner;
            internal static FileStream? ReplayFile;
            internal static bool EndOfFile;
            internal static Lock XmlReaderLock => LazyStatic.GetOrCreate(ref s_xmlReaderLock);
        }

        internal static class CSEHeuristicRLHookData
        {
            internal static readonly string[]? FeatureNameAndType = Configuration.DEBUG
                ? [
                    "type", "viable", "live_across_call", "const",
                    "shared_const", "make_cse", "has_call", "containable",
                    "cost_ex", "cost_sz", "use_count", "def_count",
                    "use_wt_cnt", "def_wt_cnt", "distinct_locals", "local_occurrences",
                    "bb_count", "block_spread", "enreg_count",
                ]
                : null;
        }

        internal static class FloatRegisterNames
        {
            internal static readonly string[] FloatNames = regNumberExtensions.CreatePrefixedNames("x");
            internal static readonly string[] YmmNames = regNumberExtensions.CreatePrefixedNames("y");
            internal static readonly string[] ZmmNames = regNumberExtensions.CreatePrefixedNames("z");
        }

        internal static class IndentStackStaticData
        {
            internal static readonly string[] s_asciiIndents = [
                "|",
                "\\",
                "/",
                "+",
                "-",
                "*",
                "?",
            ];

            internal static readonly string[] s_unicodeIndents = [
                "│",
                "└",
                "┌",
                "├",
                "─",
                "▌",
                "?",
            ];
        }

        internal static class InsCflagsNamesData
        {
            private sealed class Data
            {
                internal readonly string[] Values =
                [
                    "0", "v", "c", "cv", "z", "zv", "zc", "zcv",
                    "n", "nv", "nc", "ncv", "nz", "nzv", "nzc", "nzcv",
                ];
            }

            private static Data? s_data;

            internal static string[] Values => LazyStatic.GetOrCreate(ref s_data).Values;
        }

        internal static class MethodRangeData
        {
            internal static ConfigMethodRange DisabledRange;
            internal static ConfigMethodRange fJitStressRange;
            internal static ConfigMethodRange fJitRange;
            internal static ConfigMethodRange s_jitInstrumentIfOptimizingRange;
            internal static ConfigMethodRange s_jitEnableOsrRange;
            internal static ConfigMethodRange s_jitEnablePgoRange;
            internal static ConfigMethodRange s_jitInlineMethodsWithEHRange;
            internal static ConfigMethodRange s_jitOptRepeatRange;
            internal static ConfigMethodRange s_localAddrPropagationRange;
            internal static ConfigMethodRange s_jitAsyncDefaultValueAnalysisRange;
            internal static ConfigMethodRange s_jitAsyncPreservedValueAnalysisRange;
            internal static ConfigMethodRange s_jitEnableEarlyLivenessRange;
            internal static ConfigMethodRange s_jitEnableHeadTailMergeRange;
            internal static ConfigMethodRange s_jitEnableInductionVariableOptsRange;
            internal static ConfigMethodRange s_physicalPromotionRange;
            internal static ConfigMethodRange s_jitEnableRboRange;
            internal static ConfigMethodRange s_jitEnableVNBasedDeadStoreRemovalRange;
            internal static ConfigMethodRange s_jitEnableCrossBlockLocalAssertionPropRange;
            internal static ConfigMethodRange s_jitGuardedDevirtualizationRange;
            internal static ConfigMethodRange s_jitEnablePatchpointRange;
            internal static ConfigMethodRange s_jitOptimizeAwaitRange;
            internal static ConfigMethodRange s_jitObjectStackAllocationConditionalEscapeRange;
            internal static ConfigMethodRange s_jitObjectStackAllocationTrackFieldsRange;
            internal static ConfigMethodRange s_jitObjectStackAllocationRange;
        }

        internal static class FunctionTraceData
        {
            internal static int jitNestingLevel;
        }

        internal static class CseHeuristicData
        {
            internal static uint s_totalCseCount;
        }

        internal static class DebugJumpSelector
        {
#pragma warning disable CA1802 // Keep the native debug selector non-constant so disabled tracing has no unreachable branches.
            internal static readonly int INTERESTING_JUMP_NUM = -1;
#pragma warning restore CA1802
        }

        internal static class CompStressData
        {
            internal static readonly string[] ModeNames = Enum.GetNames<compStressArea>();
            internal static readonly int[] AreaHashes = new int[(int)STRESS_COUNT];
            internal static readonly byte[][] ModeNamesUtf8 = Array.ConvertAll(ModeNames, static name => Encoding.ASCII.GetBytes(name));
        }

        internal static class RichDebugInfoFileData
        {
            private static Lock? s_fileLock;
            internal static Lock FileLock => LazyStatic.GetOrCreate(ref s_fileLock);
        }

        internal static class CodeGenStaticData
        {
            internal static ConfigMethodRange JitR2RUnsupportedRange;
        }

        internal static class DebuggerDisplaySequences
        {
            internal static int s_compMethodsCount;
            internal static uint s_cBlockSequence;
            internal static uint s_cBlocksSequence;
            internal static uint s_cBlocksVSequence;
            internal static uint s_cStmtSequence;
            internal static uint s_cTreeSequence;
            internal static uint s_cTreeLIRSequence;
            internal static uint s_cTreeRangeSequence;
            internal static uint s_cTreesSequence;
            internal static uint s_cEHSequence;
            internal static uint s_cVarSequence;
            internal static uint s_cVarDscSequence;
            internal static uint s_cVarsSequence;
            internal static uint s_cVarsFinalSequence;
            internal static uint s_cBlockPredsSequence;
            internal static uint s_cBlockSuccsSequence;
            internal static uint s_cReachSequence;
            internal static uint s_cDomsSequence;
            internal static uint s_cLivenessSequence;
            internal static uint s_cCVarSetSequence;
            internal static uint s_cLoopsSequence;
            internal static uint s_cLoopsASequence;
            internal static uint s_cLoopSequence;
            internal static uint s_cScevSequence;
            internal static uint s_cVNSequence;
            internal static uint s_dRegMaskSequence;
            internal static uint s_dBlockListSequence;
            internal static uint s_dIsaSequence;
            internal static uint s_dIsaFlagsSequence;
            internal static uint s_cTreeFlagsSequence;
        }

        private static class ArrayStackSidecar<T>
        {
            private static ConditionalWeakTable<ArrayStack<T>, ArrayStackDebugData>? s_instances;

            internal static ArrayStackDebugData Get(ArrayStack<T> arrayStack)
            {
                var instances = LazyStatic.GetOrCreate(ref s_instances);
                return instances.GetValue(arrayStack, static _ => new ArrayStackDebugData());
            }
        }

        internal sealed class ArrayStackDebugData
        {
            internal uint Version;
        }

        internal sealed class EmitterDebugData
        {
            internal VARSET_TP debugPrevGCrefVars = [];
            internal VARSET_TP debugThisGCrefVars = [];
            internal GCInfo.regPtrDsc? debugPrevRegPtrDsc;
            internal regMaskTP debugPrevGCrefRegs;
            internal regMaskTP debugPrevByrefRegs;
            internal int VarRefOffs;
            internal bool EmitIssuing;
            internal bool CheckAlignment;
        }

        internal sealed class CodeGenDebugData
        {
            internal CodeGen.TrnslLocalVarInfo[] TranslatedLocalVarInfo = [];
        }

        internal sealed class GenTreeDebugData
        {
            internal genTreeOps OperSave;
            internal GenTreeDebugFlags Flags;
            internal int TreeId;
            internal int SequenceNumber;
            internal int UseNumber;
            internal bool CostsInitialized;
            internal GenTree.genRegTag RegTag;
            internal GenTreeCallDebugFlags CallDebugFlags;
            internal InlineObservation InlineObservation;
            internal IL_OFFSET RawILOffset;
            internal nint TargetHandle;
            internal IL_OFFSET LocalILOffset = BAD_IL_OFFSET;
            internal IL_OFFSET StmtLastILOffset = BAD_IL_OFFSET;
        }

        internal sealed class InstructionGroupDebugData
        {
            internal insGroup? Self;
            internal BasicBlock? LastGeneratedBlock;
            internal List<BasicBlock>? Blocks;
            internal nuint DataSize;
        }

        internal sealed class StatementDebugData
        {
            internal int Id;
            internal IL_OFFSET LastILOffset;
        }

        internal sealed class BasicBlockDebugData
        {
            internal int TargetStackDepth;
            internal int TraversalStamp;
        }

        internal sealed class NaturalLoopIterInfoDebugData
        {
            internal GenTree? InitTree;
        }

        internal sealed class FlowEdgeDebugData
        {
            internal bool LikelihoodSet;
        }

        internal sealed class AddCodeDscDebugData
        {
            internal uint Number;
        }

        internal sealed class InlineCandidateInfoDebugData
        {
            internal int AsyncStressIndex = -1;
        }

        internal sealed class InlineContextDebugData
        {
            internal InlinePolicy? Policy;
            internal int TreeId;
            internal BitArray? ILInstsSet;
        }

        internal sealed class AsyncStressPolicyDebugData
        {
            internal bool IsAsyncCall;
            internal int AsyncStressIndex = -1;
            internal uint BasicBlockCount;
        }

        internal sealed class ReplayPolicyDebugData
        {
            internal InlineContext? InlineContext;
            internal IL_OFFSET Offset = BAD_IL_OFFSET;
            internal bool WasForceInline;
            internal bool IsDataCollectionTarget;
        }

        internal sealed class UnwindInfoDebugData
        {
            internal bool AddingNOP;
            internal bool Initialized;
        }

        internal sealed class ClassLayoutDebugNames
        {
            internal string ClassName = string.Empty;
            internal string ShortClassName = string.Empty;
        }

        internal sealed class ScevAddRecDebugData
        {
            internal FlowGraphNaturalLoop? Loop;
        }

        internal sealed class RefPositionDebugData
        {
            internal uint MinRegCandidateCount;
            internal uint ReferencePositionNumber;
            internal GenTree? BuildNode;
        }

        internal sealed class LinearScanAllocationDebugData
        {
            internal static int EmptyColumnWidth;
            internal static ConfigMethodRange StressRegsRange;

            internal GenTree? CurrentBuildNode;
            internal int StressMask;
            internal uint MaxNodeLocation;
            internal uint ConsecutiveRegistersLocation;
            internal RefPosition? ActiveRefPosition;
            internal regMaskTP Registers;
            internal bool DumpFormatInitialized;
            internal regMaskTP LastDumpRegisters;
            internal RefPosition? LastDumpRefPosition;
            internal int DumpLastUsedRegNumIndex;
            internal int DumpRegColumnWidth;
            internal int DumpNodeLocationWidth;
            internal int DumpRefPositionWidth;
            internal int DumpShortRefPositionWidth;
            internal int DumpTableIndent;
            internal int DumpBbNumWidth;
            internal int DumpPredBbNumWidth;
            internal int DumpRowsSinceTitle;
            internal string DumpColumnSeparator = "";
            internal string DumpLine = "";
            internal string DumpMiddleBox = "";
            internal string DumpRightBox = "";
        }

        internal sealed class LinearScanRegisterSelectionDebugData<TRegisterScore>
            where TRegisterScore : notnull
        {
            internal readonly TRegisterScore[] RegSelectionOrder;
            internal readonly Dictionary<TRegisterScore, System.Action> MappingTable;

            internal LinearScanRegisterSelectionDebugData(
                int heuristicCount,
                Dictionary<TRegisterScore, System.Action> mappingTable)
            {
                RegSelectionOrder = new TRegisterScore[heuristicCount];
                MappingTable = mappingTable;
            }
        }

        internal sealed class CSEHeuristicDebugData
        {
            internal List<uint>? Sequence;
            internal List<double>? Likelihoods;
            internal List<double>? BaseLikelihoods;
            internal List<string>? Features;
            internal double[]? Rewards;
            internal CLRRandom? Random;
        }

        internal sealed class VariableLiveDumperDebugData
        {
            private readonly Compiler m_compiler;
            private readonly int m_varNum;

            // Append-only range storage makes this index equivalent to the native stable list iterator.
            private int m_startingLiveRange;
            private bool m_hasLiveRangesToDump;

            internal VariableLiveDumperDebugData(Compiler compiler, int varNum)
            {
                m_compiler = compiler;
                m_varNum = varNum;
            }

            internal Compiler Compiler => m_compiler;

            internal int VarNum => m_varNum;

            internal void resetDumper(List<CodeGen.VariableLiveKeeper.VariableLiveRange> liveRanges)
            {
                assert(m_hasLiveRangesToDump);
                if (liveRanges[^1].m_EndEmitLocation.Valid())
                {
                    m_hasLiveRangesToDump = false;
                }
                else
                {
                    m_startingLiveRange = liveRanges.Count - 1;
                }
            }

            internal void setDumperStartAt(int liveRangeIndex)
            {
                m_hasLiveRangesToDump = true;
                m_startingLiveRange = liveRangeIndex;
            }

            internal int getStartForDump() => m_startingLiveRange;

            internal bool hasLiveRangesToDump() => m_hasLiveRangesToDump;
        }

        internal sealed class IncrementalSsaBuilderDebugData
        {
            internal bool FinalizedDefs;
        }

        internal sealed class TreeLifeUpdaterDebugData
        {
            internal readonly int Epoch;
            internal nint[] OldLife;
            internal nint[] OldStackPtrsLife;

            internal TreeLifeUpdaterDebugData(Compiler compiler)
            {
                Epoch = compiler.CurLVEpoch;
                OldLife = VarSetOps.MakeEmpty(compiler);
                OldStackPtrsLife = VarSetOps.MakeEmpty(compiler);
            }
        }

        internal static ArrayStackDebugData Get<T>(ArrayStack<T> arrayStack)
        {
            return ArrayStackSidecar<T>.Get(arrayStack);
        }

        internal static EmitterDebugData Get(Emitter emitter)
        {
            var instances = LazyStatic.GetOrCreate(ref s_emitters);
            return instances.GetValue(emitter, static _ => new EmitterDebugData());
        }

        internal static VariableLiveDumperDebugData Get(
            CodeGen.VariableLiveKeeper.VariableLiveDescriptor descriptor,
            Compiler compiler,
            int varNum)
        {
            var instances = LazyStatic.GetOrCreate(ref s_variableLiveDumpers);
            return instances.GetValue(descriptor, _ => new VariableLiveDumperDebugData(compiler, varNum));
        }

        internal static VariableLiveDumperDebugData Get(
            CodeGen.VariableLiveKeeper.VariableLiveDescriptor descriptor)
        {
            var instances = LazyStatic.GetOrCreate(ref s_variableLiveDumpers);
            return instances.GetValue(descriptor, static _ =>
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LiveRangeDiagnosticsAreOnlyAvailableInDebugBuilds);
                return default!;
            });
        }

        internal static IncrementalSsaBuilderDebugData Get(IncrementalSsaBuilder builder)
        {
            var instances = LazyStatic.GetOrCreate(ref s_incrementalSsaBuilders);
            return instances.GetValue(builder, static _ => new IncrementalSsaBuilderDebugData());
        }

        internal static TreeLifeUpdaterDebugData Get(TreeLifeUpdater updater, Compiler compiler)
        {
            var instances = LazyStatic.GetOrCreate(ref s_treeLifeUpdaters);
            return instances.GetValue(updater, _ => new TreeLifeUpdaterDebugData(compiler));
        }

        internal static GenTreeDebugData Get(GenTree genTree)
        {
            var instances = LazyStatic.GetOrCreate(ref s_genTrees);
            return instances.GetValue(genTree, static _ => new GenTreeDebugData());
        }

        internal static InstructionGroupDebugData Get(insGroup instructionGroup)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instructionGroups);
            return instances.GetValue(instructionGroup, static _ => new InstructionGroupDebugData());
        }

        internal static StatementDebugData Get(Statement statement)
        {
            var instances = LazyStatic.GetOrCreate(ref s_statements);
            return instances.GetValue(statement, static _ => new StatementDebugData());
        }

        internal static BasicBlockDebugData Get(BasicBlock basicBlock)
        {
            var instances = LazyStatic.GetOrCreate(ref s_basicBlocks);
            return instances.GetValue(basicBlock, static _ => new BasicBlockDebugData());
        }

        internal static NaturalLoopIterInfoDebugData Get(NaturalLoopIterInfo iterInfo)
        {
            var instances = LazyStatic.GetOrCreate(ref s_naturalLoopIterInfos);
            return instances.GetValue(iterInfo, static _ => new NaturalLoopIterInfoDebugData());
        }

        internal static FlowEdgeDebugData Get(FlowEdge flowEdge)
        {
            var instances = LazyStatic.GetOrCreate(ref s_flowEdges);
            return instances.GetValue(flowEdge, static _ => new FlowEdgeDebugData());
        }

        internal static AddCodeDscDebugData Get(AddCodeDsc addCodeDsc)
        {
            var instances = LazyStatic.GetOrCreate(ref s_addCodeDscs);
            return instances.GetValue(addCodeDsc, static _ => new AddCodeDscDebugData());
        }

        internal static InlineCandidateInfoDebugData Get(InlineCandidateInfo candidateInfo)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.AsyncInliningStressIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_inlineCandidates);
            return instances.GetValue(candidateInfo, static _ => new InlineCandidateInfoDebugData());
        }

        internal static InlineContextDebugData Get(InlineContext inlineContext)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.InlineContextDiagnosticStateIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_inlineContexts);
            return instances.GetValue(inlineContext, static _ => new InlineContextDebugData());
        }

        internal static AsyncStressPolicyDebugData Get(AsyncStressPolicy policy)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.AsyncInliningStressIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_asyncStressPolicies);
            return instances.GetValue(policy, static _ => new AsyncStressPolicyDebugData());
        }

        internal static ReplayPolicyDebugData Get(ReplayPolicy policy)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.ReplayInlinePolicyIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_replayPolicies);
            return instances.GetValue(policy, static _ => new ReplayPolicyDebugData());
        }

        internal static UnwindInfoDebugData Get(UnwindInfo unwindInfo)
        {
            var instances = LazyStatic.GetOrCreate(ref s_unwindInfos);
            return instances.GetValue(unwindInfo, static _ => new UnwindInfoDebugData());
        }

        internal static ClassLayoutDebugNames Get(ClassLayout classLayout)
        {
            var instances = LazyStatic.GetOrCreate(ref s_classLayouts);
            return instances.GetValue(classLayout, static _ => new ClassLayoutDebugNames());
        }

        internal static ScevAddRecDebugData Get(ScevAddRec addRec)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.ScalarEvolutionDebugDataIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_scevAddRecs);
            return instances.GetValue(addRec, static _ => new ScevAddRecDebugData());
        }

        internal static RefPositionDebugData Get(RefPosition refPosition)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    FatalJitErrorCode.RefPositionDebugDataIsOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_refPositions);
            return instances.GetValue(refPosition, static _ => new RefPositionDebugData());
        }

        internal static LinearScanAllocationDebugData Get(LinearScan linearScan)
        {
            var instances = LazyStatic.GetOrCreate(ref s_linearScans);
            return instances.GetValue(linearScan, static _ => new LinearScanAllocationDebugData());
        }

        internal static LinearScanRegisterSelectionDebugData<LinearScan.RegisterScore> Get(
            LinearScan.RegisterSelection registerSelection,
            ConditionalWeakTable<
                LinearScan.RegisterSelection,
                LinearScanRegisterSelectionDebugData<LinearScan.RegisterScore>>.CreateValueCallback createValueCallback)
        {
            var instances = LazyStatic.GetOrCreate(ref s_registerSelections);
            return instances.GetValue(registerSelection, createValueCallback);
        }

        internal static CSEHeuristicDebugData Get(CSE_HeuristicCommon heuristic)
        {
            var instances = LazyStatic.GetOrCreate(ref s_cseHeuristics);
            return instances.GetValue(heuristic, static _ => new CSEHeuristicDebugData());
        }

        internal static bool s_pJitDisasmIncludeAssembliesListInitialized;
        internal static AssemblyNamesList2? s_pJitDisasmIncludeAssembliesList;
        internal static bool s_pJitFunctionFileInitialized;
        internal static MethodSet2? s_pJitMethodSet;
        internal static int s_nMaxTrees;
        internal static bool s_compOrderHeaderPrinted;
        internal static bool s_logToEEfailed;
        internal static int s_lvaDoubleAlignedProcsCount;

        internal static class LoopHoistStatsData
        {
            internal static readonly Lock? s_loopHoistStatsLock = Configuration.DEBUG ? new() : null;
            internal static int s_loopsConsidered;
            internal static int s_loopsWithHoistedExpressions;
            internal static int s_totalHoistedExpressions;
        }

        internal static class EnregisterStatsData
        {
            internal static EnregisterStats s_enregisterStats;
        }

        internal static class FatalStatisticsData
        {
            internal static uint s_fatalNoWayAssertBodyArgsCount;
        }

        internal bool AsciiTrees;
        internal bool CodeGenInterruptibleUsed;
        internal bool CodeGenVerbose;
        internal bool CodeGenWriteBarrierUsed;
        internal int CodeGenTranslatedLocalVariableCount;
        internal uint CodeGenCurrentDisplayOffset;
        internal GenTree? AssertionPropCurrentTree;
        internal long CompCyclesAtEndOfInlining;
        internal long CompCycles;
        internal int InlineMethodXmlFilePosition;
        internal CLRRandom? InlineStressRandom;
        internal uint AddCodeDscCount;
        internal bool UnknownSizeFrameFinalized;
        internal int ExpensiveDebugCheckLevel;
        internal LocalDebugData[]? LocalVariables;
        internal string?[]? VarScopeNames;
        internal bool Verbose;
        internal bool VerboseTrees;
        internal bool VerboseSsa;
        internal bool CompAllowStress = true;
        internal string? GSSecurityCheckBlocker;
        internal int CompGenTreeID;
        internal List<BasicBlock>? FlowGraphBlockOrder;
        internal bool PrintInlinedMethods;
        internal bool EHNormalizationDone;
        internal int InlinedCodeSize;
        internal VARSET_TP TrackedVars = [];
        internal int LoopsConsidered;
        internal bool CurrentLoopHasHoistedExpression;
        internal int LoopsWithHoistedExpressions;
        internal int TotalHoistedExpressions;
        internal InlineArrayCompilerStressCount<byte> CompActiveStressModes;
        internal GenTree? LastConsumedNode;
        internal bool TreesBeforeAfterMorph;
        internal int BlockTraversalLabel = 1;
        internal bool ImplicitByRefLclFldsStale;
        internal bool CompDebugBreak;
        internal bool JitFallbackCompile;
        internal bool PgoDeferredInconsistency;
        internal int MorphNum;
        internal bool SafeBasicBlockCreation = true;
        internal bool SafeFlowEdgeCreation = true;
        internal bool StackCheckOnRet;
        internal bool StackCheckOnCall;
        internal bool CodeGenDone;
        internal NodeToTestDataMap? NodeTestData;
        internal int LoopHoistCSEClass = FIRST_LOOP_HOIST_CSE_CLASS;
        internal long NumStatementLinksTraversed;
        internal nint SizeEstimate;
        internal nint CycleEstimate;
        internal bool PoisoningAnyImplicitByrefs;
        internal int ReturnSpCheck = BAD_VAR_NUM;
        internal int CallSpCheck = BAD_VAR_NUM;
        internal bool CompMinOptsIsUsed;
        internal bool CompGcChecks;
        internal bool CompEnablePCRelAddr;
        internal bool CompProcedureSplittingEH;
        internal bool DspCode;
        internal bool DspEHTable;
        internal bool DspDebugInfo;
        internal bool DspInstrs;
        internal bool DspLines;
        internal bool VarNames;
        internal bool DisAsmSpilled;
        internal bool DisasmWithGC;
        internal bool DisAddr;
        internal bool DisAsm2;
        internal bool DspOrder;
        internal bool DspUnwind;
        internal bool CompLongAddress;
        internal bool DspGCtbls;
        internal bool DspMetrics;
        internal bool CompJitAlignLoopForJcc;

        internal struct LocalDebugData
        {
            internal LocalDebugFlags Flags;
            internal DoNotEnregisterReason DoNotEnregisterReason;
            internal AddressExposedReason AddressExposedReason;
            internal byte EncodedSingleDefDisqualifyReason;
            internal string? Reason;
            internal LocalSsaDebugData[]? SsaDefinitions;

            internal readonly bool IsEmpty =>
                (Flags is LocalDebugFlags.None) &&
                (DoNotEnregisterReason is DoNotEnregisterReason.None) &&
                (AddressExposedReason is AddressExposedReason.NONE) &&
                (EncodedSingleDefDisqualifyReason == 0) &&
                (Reason is null) &&
                (SsaDefinitions is null);
        }

        internal struct LocalSsaDebugData
        {
            internal ValueNumPair OriginalVNPair;
            internal bool Updated;
        }

        internal static JitTlsDebugData Get(JitTls jitTls)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitTlsScopesAreOnlyAvailableInDebugBuilds);
                return default!;
            }

            var instances = LazyStatic.GetOrCreate(ref s_jitTlsInstances);
            return instances.GetValue(jitTls, static _ => new JitTlsDebugData());
        }

        internal static CodeGenDebugData Get(CodeGen codeGen)
        {
            var instances = LazyStatic.GetOrCreate(ref s_codeGens);
            return instances.GetValue(codeGen, static _ => new CodeGenDebugData());
        }

        internal static DebugData Get(Compiler compiler)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(compiler, static _ => new DebugData());
        }
    }

    /// <summary>If true, dump trees using only ASCII characters.</summary>
    public bool asciiTrees
    {
        get
        {
            return GetDebugData().AsciiTrees;
        }

        set
        {
            GetDebugData().AsciiTrees = value;
        }
    }

    /// <summary>If true, print trees before and after morphing.</summary>
    public bool treesBeforeAfterMorph
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().TreesBeforeAfterMorph;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().TreesBeforeAfterMorph = value;
            }
        }
    }

    /// <summary>This counts morphed trees to label their diagnostic output.</summary>
    public int morphNum
    {
        get
        {
            return Configuration.DEBUG ? GetDebugData().MorphNum : 0;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().MorphNum = value;
            }
        }
    }

    public int expensiveDebugCheckLevel
    {
        get
        {
            return GetDebugData().ExpensiveDebugCheckLevel;
        }

        set
        {
            GetDebugData().ExpensiveDebugCheckLevel = value;
        }
    }

    /// <summary>Indicates that code generation has completed for the method.</summary>
    public bool compCodeGenDone
    {
        get
        {
            return GetDebugData().CodeGenDone;
        }

        set
        {
            GetDebugData().CodeGenDone = value;
        }
    }

    public bool compDebugBreak
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompDebugBreak;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugBreakStateIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompDebugBreak = value;
        }
    }

    public bool fgPgoDeferredInconsistency
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.DeferredProfileInconsistencyStateIsOnlyAvailableInDebugBuilds);
            }

            return GetDebugData().PgoDeferredInconsistency;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.DeferredProfileInconsistencyStateIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().PgoDeferredInconsistency = value;
        }
    }

    /// <summary>Are we doing a fallback compile?</summary>
    public bool jitFallbackCompile
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().JitFallbackCompile;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().JitFallbackCompile = value;
            }
        }
    }

    private NodeToTestDataMap? _nodeTestData
    {
        get
        {
            return GetDebugData().NodeTestData;
        }

        set
        {
            GetDebugData().NodeTestData = value;
        }
    }

    private int _loopHoistCSEClass
    {
        get
        {
            return GetDebugData().LoopHoistCSEClass;
        }

        set
        {
            GetDebugData().LoopHoistCSEClass = value;
        }
    }

    /// <summary>Number of statement links traversed while performing debug checks.</summary>
    public ref long compNumStatementLinksTraversed => ref GetDebugData().NumStatementLinksTraversed;

    /// <summary>The estimated size of the method as per `gtSetEvalOrder`.</summary>
    public nint compSizeEstimate
    {
        get
        {
            return GetDebugData().SizeEstimate;
        }

        set
        {
            GetDebugData().SizeEstimate = value;
        }
    }

    /// <summary>The estimated cycle count of the method as per `gtSetEvalOrder`.</summary>
    public nint compCycleEstimate
    {
        get
        {
            return GetDebugData().CycleEstimate;
        }

        set
        {
            GetDebugData().CycleEstimate = value;
        }
    }

    /// <summary>Importer inserted IR before returns to poison implicit byrefs.</summary>
    public bool compPoisoningAnyImplicitByrefs
    {
        get
        {
            return GetDebugData().PoisoningAnyImplicitByrefs;
        }

        set
        {
            GetDebugData().PoisoningAnyImplicitByrefs = value;
        }
    }

    private int NextBlockTraversalLabel => Interlocked.Increment(ref GetDebugData().BlockTraversalLabel);

    public bool fgImplicitByRefLclFldsStale
    {
        get
        {
            return GetDebugData().ImplicitByRefLclFldsStale;
        }

        set
        {
            GetDebugData().ImplicitByRefLclFldsStale = value;
        }
    }

    internal bool GetLocalVarTrackedWithoutIndex(int localNumber)
    {
        return Configuration.DEBUG && GetLocalDebugFlag(localNumber, LocalDebugFlags.TrackedWithoutIndex);
    }

    [Conditional("DEBUG")]
    internal void SetLocalVarTrackedWithoutIndex(int localNumber, bool value)
    {
        SetLocalDebugFlag(localNumber, LocalDebugFlags.TrackedWithoutIndex, value);
    }

    internal bool GetLocalDebugFlag(int localNumber, LocalDebugFlags flag)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugFlagsAreOnlyAvailableInDebugBuilds);
        }

        return (GetLocalDebugData(localNumber).Flags & flag) != 0;
    }

    internal bool GetLocalDebugFlag(in LclVarDsc local, LocalDebugFlags flag)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugFlagsAreOnlyAvailableInDebugBuilds);
        }

        return GetLocalDebugFlag(lvaGetLclNum(in local), flag);
    }

    internal bool GetLocalClassInfoUpdated(int localNumber)
        => Configuration.DEBUG && GetLocalDebugFlag(localNumber, LocalDebugFlags.ClassInfoUpdated);

    internal bool GetLocalClassInfoUpdated(in LclVarDsc local)
        => Configuration.DEBUG && GetLocalDebugFlag(in local, LocalDebugFlags.ClassInfoUpdated);

    internal void SetLocalDebugFlag(int localNumber, LocalDebugFlags flag, bool value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugFlagsAreOnlyAvailableInDebugBuilds);
        }

        var localData = GetLocalDebugData(localNumber);
        localData.Flags = (localData.Flags & ~flag) | (value ? flag : LocalDebugFlags.None);
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalDebugFlag(in LclVarDsc local, LocalDebugFlags flag, bool value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugFlagsAreOnlyAvailableInDebugBuilds);
        }

        SetLocalDebugFlag(lvaGetLclNum(in local), flag, value);
    }

    internal string? GetLocalDebugReason(int localNumber)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugReasonIsOnlyAvailableInDebugBuilds);
            return null;
        }

        return GetLocalDebugData(localNumber).Reason;
    }

    internal void SetLocalDebugReason(int localNumber, string? reason)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LocalDebugReasonIsOnlyAvailableInDebugBuilds);
            return;
        }

        var localData = GetLocalDebugData(localNumber);
        localData.Reason = reason;
        SetLocalDebugData(localNumber, localData);
    }

    internal string? GetVarScopeName(int scopeNumber)
    {
        if (!Configuration.DEBUG)
        {
            return null;
        }

        var names = GetDebugData().VarScopeNames;
        return names is not null && (uint)scopeNumber < (uint)names.Length
            ? names[scopeNumber]
            : null;
    }

    internal string? GetVarScopeName(in VarScopeDsc scope)
    {
        if (!Configuration.DEBUG || (info.compVarScopes.Length is 0))
        {
            return null;
        }

        var scopeIndex = (int)(
            System.Runtime.CompilerServices.Unsafe.ByteOffset(in info.compVarScopes[0], in scope) /
            System.Runtime.CompilerServices.Unsafe.SizeOf<VarScopeDsc>());
        return GetVarScopeName(scopeIndex);
    }

    internal void SetVarScopeName(int scopeNumber, string? name)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            return;
        }

        System.ArgumentOutOfRangeException.ThrowIfNegative(scopeNumber);

        var debugData = GetDebugData();
        var names = debugData.VarScopeNames;

        if (names is null)
        {
            if (name is null)
            {
                return;
            }

            names = new string?[System.Math.Max(4, scopeNumber + 1)];
        }
        else if (scopeNumber >= names.Length)
        {
            if (name is null)
            {
                return;
            }

            System.Array.Resize(ref names, System.Math.Max(names.Length * 2, scopeNumber + 1));
        }

        names[scopeNumber] = name;
        debugData.VarScopeNames = names;
    }

    [Conditional("DEBUG")]
    internal void ResetVarScopeNames()
    {
        GetDebugData().VarScopeNames = null;
    }

    internal DoNotEnregisterReason GetLocalDoNotEnregisterReason(int localNumber)
        => Configuration.DEBUG ? GetLocalDebugData(localNumber).DoNotEnregisterReason : DoNotEnregisterReason.None;

    internal DoNotEnregisterReason GetLocalDoNotEnregisterReason(in LclVarDsc local)
        => Configuration.DEBUG ? GetLocalDoNotEnregisterReason(lvaGetLclNum(in local)) : DoNotEnregisterReason.None;

    internal void SetLocalDoNotEnregisterReason(int localNumber, DoNotEnregisterReason reason)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.DoNotEnregisterReasonIsOnlyAvailableInDebugBuilds);
        }

        var localData = GetLocalDebugData(localNumber);
        localData.DoNotEnregisterReason = reason;
        SetLocalDebugData(localNumber, localData);
    }

    internal AddressExposedReason GetLocalAddressExposedReason(int localNumber)
        => Configuration.DEBUG ? GetLocalDebugData(localNumber).AddressExposedReason : AddressExposedReason.NONE;

    internal AddressExposedReason GetLocalAddressExposedReason(in LclVarDsc local)
        => Configuration.DEBUG ? GetLocalAddressExposedReason(lvaGetLclNum(in local)) : AddressExposedReason.NONE;

    internal void SetLocalAddressExposedReason(int localNumber, AddressExposedReason reason)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.AddressExposedReasonIsOnlyAvailableInDebugBuilds);
        }

        var localData = GetLocalDebugData(localNumber);
        localData.AddressExposedReason = reason;
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalAddressExposed(int localNumber, bool value, AddressExposedReason reason)
    {
        lvaGetDesc(localNumber).SetAddressExposed(value);
        if (Configuration.DEBUG)
        {
            SetLocalAddressExposedReason(localNumber, reason);
        }
    }

    internal void SetLocalAddressExposed(in LclVarDsc local, bool value, AddressExposedReason reason)
        => SetLocalAddressExposed(lvaGetLclNum(in local), value, reason);

    internal byte GetLocalSingleDefDisqualifyReason(int localNumber)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds);
        }

        return (byte)(GetLocalDebugData(localNumber).EncodedSingleDefDisqualifyReason ^ 'H');
    }

    internal byte GetLocalSingleDefDisqualifyReason(in LclVarDsc local)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds);
        }

        return GetLocalSingleDefDisqualifyReason(lvaGetLclNum(in local));
    }

    internal void SetLocalSingleDefDisqualifyReason(int localNumber, byte reason)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds);
        }

        var localData = GetLocalDebugData(localNumber);
        localData.EncodedSingleDefDisqualifyReason = (byte)(reason ^ 'H');
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalSingleDefDisqualifyReason(in LclVarDsc local, byte reason)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds);
        }

        SetLocalSingleDefDisqualifyReason(lvaGetLclNum(in local), reason);
    }

    internal void CopyLocalDebugData(int sourceLocalNumber, int destinationLocalNumber, LocalDebugDataCopyFlags fields)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
        }

        var source = GetLocalDebugData(sourceLocalNumber);
        var destination = GetLocalDebugData(destinationLocalNumber);

        if ((fields & LocalDebugDataCopyFlags.DoNotEnregisterReason) != 0)
        {
            destination.DoNotEnregisterReason = source.DoNotEnregisterReason;
        }

        if ((fields & LocalDebugDataCopyFlags.AddressExposedReason) != 0)
        {
            destination.AddressExposedReason = source.AddressExposedReason;
        }

        if ((fields & LocalDebugDataCopyFlags.KeepType) != 0)
        {
            destination.Flags = (destination.Flags & ~LocalDebugFlags.KeepType) |
                (source.Flags & LocalDebugFlags.KeepType);
        }

        if ((fields & LocalDebugDataCopyFlags.DefinedViaAddress) != 0)
        {
            destination.Flags = (destination.Flags & ~LocalDebugFlags.DefinedViaAddress) |
                (source.Flags & LocalDebugFlags.DefinedViaAddress);
        }

        if ((fields & LocalDebugDataCopyFlags.SingleDefDisqualifyReason) != 0)
        {
            destination.EncodedSingleDefDisqualifyReason = source.EncodedSingleDefDisqualifyReason;
        }

        SetLocalDebugData(destinationLocalNumber, destination);
    }

    internal void CopyLocalDebugData(in LclVarDsc source, in LclVarDsc destination, LocalDebugDataCopyFlags fields)
        => CopyLocalDebugData(lvaGetLclNum(in source), lvaGetLclNum(in destination), fields);

    [Conditional("DEBUG")]
    internal void SetLocalTrackedVars(VARSET_TP trackedVars)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TrackedVariableSetStateIsOnlyAvailableInDebugBuilds);
            return;
        }

        var data = GetDebugData();
        VarSetOps.AssignNoCopy(this, ref data.TrackedVars, trackedVars);
    }

    internal VARSET_TP GetLocalTrackedVars()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TrackedVariableSetStateIsOnlyAvailableInDebugBuilds);
            return default!;
        }

        return GetDebugData().TrackedVars;
    }

    [Conditional("DEBUG")]
    internal void BeginLoopHoistStats()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds);
        }

        var data = GetDebugData();
        data.CurrentLoopHasHoistedExpression = false;
        data.LoopsConsidered++;
    }

    [Conditional("DEBUG")]
    internal void RecordLoopHoistedExpression()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds);
        }

        var data = GetDebugData();
        if (!data.CurrentLoopHasHoistedExpression)
        {
            data.LoopsWithHoistedExpressions++;
            data.CurrentLoopHasHoistedExpression = true;
        }

        data.TotalHoistedExpressions++;
    }

    internal int GetTotalLoopHoistedExpressions()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds);
        }

        return GetDebugData().TotalHoistedExpressions;
    }

    [Conditional("DEBUG")]
    internal void ResetLocalDebugData(int localNumber)
        => SetLocalDebugData(localNumber, default);

    [Conditional("DEBUG")]
    internal void RecordLocalSsaValueNumberUpdate(int localNumber, int ssaNum, ValueNumPair originalVNPair)
    {
        var ssaIndex = ssaNum - SsaConfig.FIRST_SSA_NUM;
        System.ArgumentOutOfRangeException.ThrowIfNegative(ssaIndex);

        var localData = GetLocalDebugData(localNumber);
        var ssaDefinitions = localData.SsaDefinitions;

        if (ssaDefinitions is null)
        {
            ssaDefinitions = new DebugData.LocalSsaDebugData[System.Math.Max(4, ssaIndex + 1)];
            for (var index = 0; index < ssaDefinitions.Length; index++)
            {
                ssaDefinitions[index].OriginalVNPair = new ValueNumPair();
            }
        }
        else if (ssaIndex >= ssaDefinitions.Length)
        {
            var oldLength = ssaDefinitions.Length;
            var newLength = System.Math.Max(oldLength * 2, ssaIndex + 1);
            System.Array.Resize(ref ssaDefinitions, newLength);

            for (var index = oldLength; index < ssaDefinitions.Length; index++)
            {
                ssaDefinitions[index].OriginalVNPair = new ValueNumPair();
            }
        }

        ref var ssaDebugData = ref ssaDefinitions[ssaIndex];
        assert(!ssaDebugData.Updated);
        ssaDebugData.OriginalVNPair = originalVNPair;
        ssaDebugData.Updated = true;

        localData.SsaDefinitions = ssaDefinitions;
        SetLocalDebugData(localNumber, localData);
    }

    [Conditional("DEBUG")]
    internal void ResetLocalSsaDebugData(int localNumber)
    {
        var localData = GetLocalDebugData(localNumber);
        if (localData.SsaDefinitions is null)
        {
            return;
        }

        localData.SsaDefinitions = null;
        SetLocalDebugData(localNumber, localData);
    }

    internal int GetCompGenTreeIDFromDebugSidecar()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
        }

        return GetDebugData().CompGenTreeID;
    }

    internal void SetCompGenTreeIDInDebugSidecar(int value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
        }

        GetDebugData().CompGenTreeID = value;
    }

    internal void IncrementCompGenTreeIDInDebugSidecar()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
        }

        GetDebugData().CompGenTreeID++;
    }

    internal void AddInlinedCodeSize(int value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineSizeDiagnosticsAreOnlyAvailableInDebugBuilds);
            return;
        }

        GetDebugData().InlinedCodeSize += value;
    }

    internal int GetInlinedCodeSize()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineSizeDiagnosticsAreOnlyAvailableInDebugBuilds);
            return default;
        }

        return GetDebugData().InlinedCodeSize;
    }

    internal void SetInlinedCodeSize(int value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineSizeDiagnosticsAreOnlyAvailableInDebugBuilds);
            return;
        }

        GetDebugData().InlinedCodeSize = value;
    }

    internal List<BasicBlock>? GetFlowGraphBlockOrder()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BasicBlockDumpOrderIsOnlyAvailableInDebugBuilds);
        }

        return GetDebugData().FlowGraphBlockOrder;
    }

    internal void SetFlowGraphBlockOrder(List<BasicBlock>? value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BasicBlockDumpOrderIsOnlyAvailableInDebugBuilds);
        }

        GetDebugData().FlowGraphBlockOrder = value;
    }

    internal bool GetPrintInlinedMethods()
        => Configuration.DEBUG && GetDebugData().PrintInlinedMethods;

    internal void SetPrintInlinedMethods(bool value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineMethodDiagnosticsAreOnlyAvailableInDebugBuilds);
        }

        GetDebugData().PrintInlinedMethods = value;
    }

    internal bool GetEHNormalizationDone()
        => Configuration.DEBUG && GetDebugData().EHNormalizationDone;

    internal void SetEHNormalizationDone(bool value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EHNormalizationDiagnosticsAreOnlyAvailableInDebugBuilds);
        }

        GetDebugData().EHNormalizationDone = value;
    }

    private DebugData.LocalDebugData GetLocalDebugData(int localNumber)
    {
        var owner = GetLocalDebugDataOwner();
        var localVariables = owner.GetDebugData().LocalVariables;
        return localVariables is not null && (uint)localNumber < (uint)localVariables.Length
            ? localVariables[localNumber]
            : default;
    }

    private void SetLocalDebugData(int localNumber, DebugData.LocalDebugData localData)
    {
        var owner = GetLocalDebugDataOwner();
        var data = owner.GetDebugData();
        var localVariables = data.LocalVariables;

        if (localVariables is not null && (uint)localNumber < (uint)localVariables.Length)
        {
            localVariables[localNumber] = localData;
            return;
        }

        if (localData.IsEmpty)
        {
            return;
        }

        System.ArgumentOutOfRangeException.ThrowIfNegative(localNumber);

        if (localVariables is null)
        {
            localVariables = new DebugData.LocalDebugData[System.Math.Max(4, localNumber + 1)];
        }
        else
        {
            var newLength = System.Math.Max(localVariables.Length * 2, localNumber + 1);
            System.Array.Resize(ref localVariables, newLength);
        }

        data.LocalVariables = localVariables;
        localVariables[localNumber] = localData;
    }

    private Compiler GetLocalDebugDataOwner()
    {
        var owner = this;
        while (owner.compIsForInlining)
        {
            owner = owner.impInlineInfo.InlinerCompiler;
        }

        return owner;
    }

    internal GenTree? LastConsumedNode => GetDebugData().LastConsumedNode;

    [Conditional("DEBUG")]
    internal void SetOptAssertionPropCurrentTree(GenTree tree)
    {
        GetDebugData().AssertionPropCurrentTree = tree;
    }

    [Conditional("DEBUG")]
    internal void SetLastConsumedNode(GenTree? node)
    {
        GetDebugData().LastConsumedNode = node;
    }

    /// <summary>When false, assert when creating a new basic block.</summary>
    public bool fgSafeBasicBlockCreation
    {
        get
        {
            return !Configuration.DEBUG || GetDebugData().SafeBasicBlockCreation;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BasicBlockCreationStateIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().SafeBasicBlockCreation = value;
        }
    }

    /// <summary>When false, assert when creating a new flow edge.</summary>
    public bool fgSafeFlowEdgeCreation
    {
        get
        {
            return !Configuration.DEBUG || GetDebugData().SafeFlowEdgeCreation;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.FlowEdgeCreationStateIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().SafeFlowEdgeCreation = value;
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            return default!;
        }

        return DebugData.Get(this);
    }

    internal bool compStackCheckOnRet
    {
        get
        {
            return GetDebugData().StackCheckOnRet;
        }

        set
        {
            GetDebugData().StackCheckOnRet = value;
        }
    }

    internal bool compStackCheckOnCall
    {
        get
        {
            return GetDebugData().StackCheckOnCall;
        }

        set
        {
            GetDebugData().StackCheckOnCall = value;
        }
    }

    internal int lvaReturnSpCheck
    {
        get
        {
            return GetDebugData().ReturnSpCheck;
        }

        set
        {
            GetDebugData().ReturnSpCheck = value;
        }
    }

    internal int lvaCallSpCheck
    {
        get
        {
            return GetDebugData().CallSpCheck;
        }

        set
        {
            GetDebugData().CallSpCheck = value;
        }
    }

    internal long InlineDiagnosticCompCycles
    {
        get
        {
            return GetDebugData().CompCycles;
        }

        set
        {
            GetDebugData().CompCycles = value;
        }
    }

    internal long InlineDiagnosticCompCyclesAtEndOfInlining
    {
        get
        {
            return GetDebugData().CompCyclesAtEndOfInlining;
        }

        set
        {
            GetDebugData().CompCyclesAtEndOfInlining = value;
        }
    }

    internal int InlineMethodXmlFilePosition
    {
        get
        {
            return GetDebugData().InlineMethodXmlFilePosition;
        }

        set
        {
            GetDebugData().InlineMethodXmlFilePosition = value;
        }
    }

    // The upstream counter is already in microseconds, but inline diagnostics scale it again.
    internal uint InlineDiagnosticJitTimeMicroseconds
    {
        get
        {
            var compCycles = InlineDiagnosticCompCycles;
            if (compCycles <= 0)
            {
                return 0;
            }

            var microseconds = ((double)compCycles / Stopwatch.Frequency) * 1_000_000;
            return unchecked((uint)microseconds);
        }
    }
}
