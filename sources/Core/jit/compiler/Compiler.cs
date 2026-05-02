// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public bool verbose;

    public bool verboseTrees;

    /// <summary>If true, dump trees using only ASCII characters</summary>
    public bool asciiTrees;

    /// <summary>If true, produce especially verbose dump output in SSA construction.</summary>
    public bool verboseSsa;

    /// <summary>If true, print trees before/after morphing (paired by an intra-compilation id:</summary>
    public bool treesBeforeAfterMorph;

    /// <summary>This counts the trees that have been morphed, allowing us to label each uniquely.</summary>
    public int morphNum;

    public uint expensiveDebugCheckLevel;
#endif

    // This table is useful for memoization of BlockDominancePreds.
    public BlockToFlowEdgeMap? m_blockToEHPreds;

    public ushort asyncContextRestoreEHID = ushort.MaxValue;

    public BasicBlockLocalPairSet? m_insertedSsaLocalsLiveIn;

    // TODO-Review: Prior to reg predict we reserve 24 bytes for Spill temps.
    //              after the reg predict we will use a computed maxTmpSize
    //              which is based upon the number of spill temps predicted by reg predict
    //              All this is necessary because if we under-estimate the size of the spill
    //              temps we could fail when encoding instructions that reference stack offsets for ARM.
    /// <summary>Pre codegen max spill temp size.</summary>
    public const uint MAX_SPILL_TEMP_SIZE = 24;

    public StructPromotionHelper? structPromotionHelper;

    public InlineStrategy? m_inlineStrategy;

    /// <summary>Keeps the mapping from SSA #'s to VN's for the implicit memory variables.</summary>
    protected SsaDefArray<SsaMemDef> lvMemoryPerSsaData;

    protected bool hasUpdatedTypeLocals;

    public const uint CHECK_SPILL_ALL = unchecked((uint)(-1));

    public const uint CHECK_SPILL_NONE = unchecked((uint)(-2));

    /// <summary>The maximum number of bytes of IL processed without clean stack state.</summary>
    /// <remarks>It allows to limit the maximum tree size and depth.</remarks>
    private const uint MAX_TREE_SIZE = 200;

    private bool m_nextAwaitIsTail;

    private static uint jitTotalMethodCompiled;

#if DEBUG
    private static int jitNestingLevel;
#endif

    private HelperToManagedMap? m_helperToManagedMap;

    public FlowGraphDfsTree? m_dfsTree;

    // The next members are annotations on the flow graph used during the optimization phases.
    // They are invalidated once RBO runs and modifies the flow graph.

    public FlowGraphNaturalLoops? m_loops;

    public LoopSideEffects? m_loopSideEffects;

    public BlockToNaturalLoopMap? m_blockToLoop;

    // Dominator tree used by SSA construction and copy propagation (the two are expected to use the same tree
    // in order to avoid the need for SSA reconstruction and an "out of SSA" phase).

    public FlowGraphDominatorTree? m_domTree;

    public FlowGraphDominanceFrontiers? m_domFrontiers;

    public BlockReachabilitySets? m_reachabilitySets;

#if DEBUG
    /// <summary>Are we doing a fallback compile?</summary>
    /// <remarks>That is, have we executed a NO_WAY assert, and we are trying to compile again in a "safer", minopts mode?</remarks>
    public bool jitFallbackCompile;
#endif

    /// <summary>This field keep the R2R helper call that would be inserted to trigger the constructor of the static class.</summary>
    /// <remarks>It is set as nongc or gc static base if they are imported, so CSE can eliminate the repeated call, or the chepeast helper function that triggers it.</remarks>
    public CorInfoHelpFunc m_preferredInitCctor;

    /// <summary>This stack, managed by the SSA numbering infrastructure, keeps "outlined composite SSA numbers".</summary>
    /// <remarks>See "SsaNumInfo::GetNum" for more details on when this is needed.</remarks>
    public Stack<uint>? m_outlinedCompositeSsaNums;

    /// <summary>This map tracks nodes whose value numbers explicitly or implicitly depend on memory states.</summary>
    /// <remarks>
    ///   <para>The map provides the entry block of the most closely enclosing loop that defines the memory region accessed when defining the nodes's VN.</para>
    ///   <para>This information should be consulted when considering hoisting node out of a loop, as the VN for the node will only be valid within the indicated loop.</para>
    ///   <para>It is not fine-grained enough to track memory dependence within loops, so cannot be used for more general code motion.</para>
    ///   <para>If a node does not have an entry in the map we currently assume the VN is not memory dependent and so memory does not constrain hoisting.</para>
    /// </remarks>
    public NodeToLoopMemoryBlockMap? m_nodeToLoopMemoryBlockMap;

    public SignatureToLookupInfoMap? m_signatureToLookupInfoMap;

#if SWIFT_SUPPORT
    public SwiftLoweringMap? m_swiftLoweringCache;
#endif

#if TARGET_X86 && FEATURE_IJW
    public bool[]? m_specialCopyArgs;
#endif

    /// <summary>The value numbers for this compilation.</summary>
    public ValueNumStore? vnStore;

    public ValueNumberState? vnState;

    /// <summary>True iff GcHeap and ByrefExposed memory have all the same def points.</summary>
    public bool byrefStatesMatchGcHeapStates;

    public uint acdCount;

    /// <summary>The following is the upper limit on how many expressions we'll keep track of for the CSE analysis.</summary>
    protected const uint MAX_CSE_CNT = EXPSET_SZ;

    protected const int MIN_CSE_COST = 2;

    protected unsafe ASSERT_TP* bbJtrueAssertionOut;

    protected FrameType rpFrameType;

    /// <summary>Set to true after we have called rpMustCreateEBPFrame once</summary>
    protected bool rpMustCreateEBPCalled;

    /// <summary>Lowering; needed to Lower IR that's added or modified after Lowering.</summary>
    private Lowering? m_pLowering;

    /// <summary>Register allocator</summary>
    private IRegAlloc? m_regAlloc;

    public Stack<ParameterRegisterLocalMapping>? m_paramRegLocalMappings;

    public CORINFO_ASYNC_INFO asyncInfo;

    public bool asyncInfoInitialized;

    public VirtualStubParamInfo? virtualStubParamInfo;

    public CodeGenInterface? codeGen;

#if FEATURE_SIMD
    /// <summary>Have we identified any SIMD types?</summary>
    /// <remarks>This is currently used by struct promotion to avoid getting type information for a struct field to see if it is a SIMD type, if we haven't seen any SIMD types or operations in the method.</remarks>
    public bool _usesSIMDTypes;

    public SimdHandlesCache? m_simdHandleCache;
#endif

    /// <summary>The Compiler instance for the inlinee</summary>
    public Compiler? InlineeCompiler;

    public Options opts;

    public static bool s_pAltJitExcludeAssembliesListInitialized;

    public static AssemblyNamesList2? s_pAltJitExcludeAssembliesList;

#if DEBUG
    public static bool s_pJitDisasmIncludeAssembliesListInitialized;

    public static AssemblyNamesList2? s_pJitDisasmIncludeAssembliesList;

    public static bool s_pJitFunctionFileInitialized;

    public static MethodSet? s_pJitMethodSet;
#endif

    public Info info;

    private ClassLayoutTable? m_classLayoutTable;

    /// <summary>the most recently active phase</summary>
    public Phases mostRecentlyActivePhase;

    /// <summary>the currently active phase checks</summary>
    public PhaseChecks activePhaseChecks;

    /// <summary>the currently active phase dumps</summary>
    public PhaseDumps activePhaseDumps = PhaseDumps.DUMP_ALL;

#if MEASURE_MEM_ALLOC
    /// <summary>Display per-phase memory statistics for every function</summary>
    public static bool s_dspMemStats;
#endif

#if LOOP_HOIST_STATS
    public uint m_loopsConsidered;

    public bool m_curLoopHasHoistedExpression;

    public uint m_loopsWithHoistedExpressions;

    public uint m_totalHoistedExpressions;

    /// <summary>This lock protects the data structures below.</summary>
    public static Lock? s_loopHoistStatsLock;

    public static uint s_loopsConsidered;

    public static uint s_loopsWithHoistedExpressions;

    public static uint s_totalHoistedExpressions;
#endif

#if TRACK_ENREG_STATS
    public static EnregisterStats s_enregisterStats;
#endif

    public JitMetrics Metrics;

    // Max value of scope count for which we would use linear search; for larger values we would use hashtable lookup.
    public const uint MAX_LINEAR_FIND_LCL_SCOPELIST = 32;

    public EntryState? stackState;

    /// <summary>Address of global cookie for unsafe buffer checks</summary>
    public unsafe GSCookie* gsGlobalSecurityCookieAddr;

    /// <summary>Value of global cookie if addr is NULL</summary>
    public GSCookie gsGlobalSecurityCookieVal;

    /// <summary>Table used by shadow param analysis code</summary>
    public ShadowParamVarInfo? gsShadowVarInfo;

    public uint gsShadowVarInfoCount;

#if DEBUG
    private NodeToTestDataMap? m_nodeTestData;

    private const uint FIRST_LOOP_HOIST_CSE_CLASS = 1000;

    /// <summary>LoopHoist test annotations turn into CSE requirements</summary>
    /// <remarks>we label them with CSE Class #'s starting at FIRST_LOOP_HOIST_CSE_CLASS. Current kept in this.</remarks>
    private uint m_loopHoistCSEClass = FIRST_LOOP_HOIST_CSE_CLASS;
#endif

    public FieldSeqStore? m_fieldSeqStore;

    public m_memorySsaMapInlineArray m_memorySsaMap;

    // The Refany type is the only struct type whose structure is implicitly assumed by IL.  We need its fields.
    public unsafe CORINFO_CLASS_HANDLE m_refAnyClass;

#if VARSET_COUNTOPS
    public static BitSetSupport.BitSetOpCounter m_varsetOpCounter;
#endif

#if ALLVARSET_COUNTOPS
    public static BitSetSupport.BitSetOpCounter m_allvarsetOpCounter;
#endif

    public static HelperCallProperties s_helperCallProperties;

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    public FpStructLoweringMap? m_fpStructLoweringCache;
#endif

#if TARGET_AMD64
    // The following are for initializing register allocator "constants" defined in targetamd64.h
    // that now depend upon runtime ISA information, e.g., the presence of AVX512, which increases
    // the number of SIMD (xmm, ymm, and zmm) registers from 16 to 32.
    // As only 64-bit xarch has the capability to have the additional registers, we limit the changes
    // to TARGET_AMD64 only.
    //
    // Users of these values need to define four accessor functions:
    //
    //    regMaskTP get_RBM_ALLFLOAT();
    //    regMaskTP get_RBM_FLT_CALLEE_TRASH();
    //    uint get_CNT_CALLEE_TRASH_FLOAT();
    //    uint get_AVAILABLE_REG_COUNT();
    //
    // which return the values of these variables.
    //
    // This was done to avoid polluting all `targetXXX.h` macro definitions with a compiler parameter, where only
    // TARGET_AMD64 requires one.

    private regMaskFlt rbmAllFloat;

    private regMaskFlt rbmFltCalleeTrash;

    private uint cntCalleeTrashFloat;

    private regMaskInt rbmAllInt;

    private regMaskInt rbmIntCalleeTrash;

    private uint cntCalleeTrashInt;

    private regNumber regIntLast;
#endif

#if TARGET_XARCH
    // The following are for initializing register allocator "constants" defined in targetamd64.h
    // that now depend upon runtime ISA information, e.g., the presence of AVX512, which adds
    // 8 mask registers for use.
    //
    // Users of these values need to define four accessor functions:
    //
    //    regMaskTP get_RBM_ALLMASK();
    //    regMaskTP get_RBM_MSK_CALLEE_TRASH();
    //    uint get_CNT_CALLEE_TRASH_MASK();
    //    uint get_AVAILABLE_REG_COUNT();
    //
    // which return the values of these variables.
    //
    // This was done to avoid polluting all `targetXXX.h` macro definitions with a compiler parameter, where only
    // TARGET_XARCH requires one.

    // TODO: Port once regMaskTP exists
    // private regMaskTP rbmAllMask;
    //
    // private regMaskTP rbmMskCalleeTrash;

    private uint cntCalleeTrashMask;

    // TODO: Port once regMaskTP exists
    // private varTypeCalleeTrashRegsInlineArray varTypeCalleeTrashRegs;
#endif

    public unsafe Compiler(CORINFO_METHOD_HANDLE methodHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, InlineInfo? inlineInfo)
    {
        // TODO: Port constructor
    }

    private ClassLayoutTable typClassLayoutTable
    {
        get
        {
            var result = m_classLayoutTable;

            if (result is null)
            {
                result = CreateClassLayoutTable(this);
                m_classLayoutTable = result;
            }
            return result;

            static ClassLayoutTable CreateClassLayoutTable(Compiler compiler)
            {
                assert(compiler.m_classLayoutTable is null);
                ClassLayoutTable? result;

                if (compiler.compIsForInlining)
                {
                    var inlinerCompiler = compiler.impInlineInfo.InlinerCompiler;
                    assert(inlinerCompiler is not null);
                    result = inlinerCompiler.typClassLayoutTable;
                }
                else
                {
                    result = new ClassLayoutTable();
                }
                return result;
            }
        }
    }

    public unsafe var_types GetHfaType(CORINFO_CLASS_HANDLE hClass)
    {
        if (GlobalJitOptions.compFeatureHfa)
        {
            if (hClass != NO_CLASS_HANDLE)
            {
                var elemKind = info.compCompHnd->getHFAType(hClass);

                if (elemKind != CORINFO_HFA_ELEM_NONE)
                {
                    // This type may not appear elsewhere, but it will occupy a floating point register.
                    compFloatingPointUsed = true;
                }
                return HfaTypeFromElemKind(elemKind);
            }
        }
        return TYP_UNDEF;
    }

    /// <inheritdoc cref="GetReturnTypeForStruct(CORINFO_CLASS_HANDLE, CorInfoCallConvExtension, out structPassingKind, uint)" />
    public unsafe var_types GetReturnTypeForStruct(CORINFO_CLASS_HANDLE clsHnd, CorInfoCallConvExtension callConv, uint structSize = 0)
        => GetReturnTypeForStruct(clsHnd, callConv, out Unsafe.NullRef<structPassingKind>(), structSize);

    /// <summary>Get the type that is used to return values of the given struct type.</summary>
    /// <param name="clsHnd"></param>
    /// <param name="callConv"></param>
    /// <param name="wbPassStruct"></param>
    /// <param name="structSize"></param>
    /// <returns></returns>
    /// <remarks>If the size is unknown, pass 0 and it will be determined from 'clsHnd'.</remarks>
    public unsafe var_types GetReturnTypeForStruct(CORINFO_CLASS_HANDLE clsHnd, CorInfoCallConvExtension callConv, out structPassingKind wbPassStruct, uint structSize = 0)
    {
        // TODO: Port getReturnTypeForStruct
        wbPassStruct = default;
        return TYP_UNKNOWN;
    }

    /// <summary>Assumes called as part of process shutdown; does any compiler-specific work associated with that.</summary>
    public static unsafe void ProcessShutdownWork(ICorStaticInfo* staticInfo)
    {
    }

#if MEASURE_NOWAY
    public void RecordNowayAssert(ReadOnlySpan<char> filePath, uint line, ReadOnlySpan<char> message)
    {
        // TODO: Port RecordNowayAssert
    }
#endif

    /// <summary>Get the layout for the specified class handle.</summary>
    /// <param name="classHandle"></param>
    /// <returns></returns>
    public unsafe ClassLayout typGetObjLayout(CORINFO_CLASS_HANDLE classHandle) => typClassLayoutTable.GetObjLayout(this, classHandle);

    [InlineArray((int)(MemoryKindCount))]
    public struct m_memorySsaMapInlineArray
    {
        public NodeToUnsignedMap e0;
    }

    // TODO: Port once regMaskTP exists
    // [InlineArray((int)(TYP_COUNT))]
    // private struct varTypeCalleeTrashRegsInlineArray
    // {
    //     public regMaskTP e0;
    // }
}
