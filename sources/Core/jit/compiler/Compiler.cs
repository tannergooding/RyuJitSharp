// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

public partial class Compiler
{
    // <summary>call has "tail" IL prefix</summary>
    private const int PREFIX_TAILCALL_EXPLICIT = 0x00000001;

    // <summary>call is treated as having "tail" prefix even though there is no "tail" IL prefix</summary>
    private const int PREFIX_TAILCALL_IMPLICIT = 0x00000002;

    private const int PREFIX_TAILCALL = PREFIX_TAILCALL_EXPLICIT | PREFIX_TAILCALL_IMPLICIT;

    private const int PREFIX_VOLATILE = 0x00000004;

    private const int PREFIX_UNALIGNED = 0x00000008;

    private const int PREFIX_CONSTRAINED = 0x00000010;

    private const int PREFIX_READONLY = 0x00000020;

#if DEBUG
    /// <summary>call doesn't "tail" IL prefix but is treated as explicit because of tail call stress</summary>
    private const int PREFIX_TAILCALL_STRESS = 0x00000040;
#endif

    private const int PREFIX_IS_TASK_AWAIT = 0x00000080;

    private const int PREFIX_TASK_AWAIT_CONTINUE_ON_CAPTURED_CONTEXT = 0x00000100;

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

    public int expensiveDebugCheckLevel;
#endif

    // This table is useful for memoization of BlockDominancePreds.
    public BlockToFlowEdgeMap? _blockToEHPreds;

    public ushort asyncContextRestoreEHID = ushort.MaxValue;

    public BasicBlockLocalPairSet? _insertedSsaLocalsLiveIn;

    // TODO-Review: Prior to reg predict we reserve 24 bytes for Spill temps.
    //              after the reg predict we will use a computed maxTmpSize
    //              which is based upon the number of spill temps predicted by reg predict
    //              All this is necessary because if we under-estimate the size of the spill
    //              temps we could fail when encoding instructions that reference stack offsets for ARM.
    /// <summary>Pre codegen max spill temp size.</summary>
    public const int MAX_SPILL_TEMP_SIZE = 24;

    public StructPromotionHelper? structPromotionHelper;

    public InlineStrategy? _inlineStrategy;

    /// <summary>Keeps the mapping from SSA #'s to VN's for the implicit memory variables.</summary>
    protected SsaDefArray<SsaMemDef> lvMemoryPerSsaData;

    protected bool hasUpdatedTypeLocals;

    public const int CHECK_SPILL_ALL = -1;

    public const int CHECK_SPILL_NONE = -2;

    /// <summary>The maximum number of bytes of IL processed without clean stack state.</summary>
    /// <remarks>It allows to limit the maximum tree size and depth.</remarks>
    private const int MAX_TREE_SIZE = 200;

    private bool _nextAwaitIsTail;

    private static int jitTotalMethodCompiled;

#if DEBUG
    private static int jitNestingLevel;
#endif

    private HelperToManagedMap? _helperToManagedMap;

    public FlowGraphDfsTree? _dfsTree;

    // The next members are annotations on the flow graph used during the optimization phases.
    // They are invalidated once RBO runs and modifies the flow graph.

    public FlowGraphNaturalLoops? _loops;

    public LoopSideEffects? _loopSideEffects;

    public BlockToNaturalLoopMap? _blockToLoop;

    // Dominator tree used by SSA construction and copy propagation (the two are expected to use the same tree
    // in order to avoid the need for SSA reconstruction and an "out of SSA" phase).

    public FlowGraphDominatorTree? _domTree;

    public FlowGraphDominanceFrontiers? _domFrontiers;

    public BlockReachabilitySets? _reachabilitySets;

#if DEBUG
    /// <summary>Are we doing a fallback compile?</summary>
    /// <remarks>That is, have we executed a NO_WAY assert, and we are trying to compile again in a "safer", minopts mode?</remarks>
    public bool jitFallbackCompile;
#endif

    /// <summary>This field keep the R2R helper call that would be inserted to trigger the constructor of the static class.</summary>
    /// <remarks>It is set as nongc or gc static base if they are imported, so CSE can eliminate the repeated call, or the chepeast helper function that triggers it.</remarks>
    public CorInfoHelpFunc _preferredInitCctor;

    /// <summary>This stack, managed by the SSA numbering infrastructure, keeps "outlined composite SSA numbers".</summary>
    /// <remarks>See "SsaNumInfo.GetNum" for more details on when this is needed.</remarks>
    public Stack<int>? _outlinedCompositeSsaNums;

    /// <summary>This map tracks nodes whose value numbers explicitly or implicitly depend on memory states.</summary>
    /// <remarks>
    ///   <para>The map provides the entry block of the most closely enclosing loop that defines the memory region accessed when defining the nodes's VN.</para>
    ///   <para>This information should be consulted when considering hoisting node out of a loop, as the VN for the node will only be valid within the indicated loop.</para>
    ///   <para>It is not fine-grained enough to track memory dependence within loops, so cannot be used for more general code motion.</para>
    ///   <para>If a node does not have an entry in the map we currently assume the VN is not memory dependent and so memory does not constrain hoisting.</para>
    /// </remarks>
    public NodeToLoopMemoryBlockMap? _nodeToLoopMemoryBlockMap;

    public SignatureToLookupInfoMap? _signatureToLookupInfoMap;

#if SWIFT_SUPPORT
    public SwiftLoweringMap? _swiftLoweringCache;
#endif

#if TARGET_X86 && FEATURE_IJW
    public bool[]? _specialCopyArgs;
#endif

    /// <summary>The value numbers for this compilation.</summary>
    public ValueNumStore? vnStore;

    public ValueNumberState? vnState;

    /// <summary>True iff GcHeap and ByrefExposed memory have all the same def points.</summary>
    public bool byrefStatesMatchGcHeapStates;

    public int acdCount;

    /// <summary>The following is the upper limit on how many expressions we'll keep track of for the CSE analysis.</summary>
    protected const int MAX_CSE_CNT = EXPSET_SZ;

    protected const int MIN_CSE_COST = 2;

    protected unsafe ASSERT_TP* bbJtrueAssertionOut;

    protected FrameType rpFrameType;

    /// <summary>Set to true after we have called rpMustCreateEBPFrame once</summary>
    protected bool rpMustCreateEBPCalled;

    /// <summary>Lowering; needed to Lower IR that's added or modified after Lowering.</summary>
    private Lowering? _pLowering;

    /// <summary>Register allocator</summary>
    private IRegAlloc? _regAlloc;

    public Stack<ParameterRegisterLocalMapping>? _paramRegLocalMappings;

    public CORINFO_ASYNC_INFO asyncInfo;

    public bool asyncInfoInitialized;

    public VirtualStubParamInfo? virtualStubParamInfo;

    public ICodeGen? codeGen;

#if FEATURE_SIMD
    /// <summary>Have we identified any simd types?</summary>
    /// <remarks>This is currently used by struct promotion to avoid getting type information for a struct field to see if it is a simd type, if we haven't seen any simd types or operations in the method.</remarks>
    public bool _usesSimdTypes;

    public simdHandlesCache? _simdHandleCache;
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

    public static MethodSet2? s_pJitMethodSet;
#endif

    public Info info;

    private ClassLayoutTable? _classLayoutTable;

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
    public int _loopsConsidered;

    public bool _curLoopHasHoistedExpression;

    public int _loopsWithHoistedExpressions;

    public int _totalHoistedExpressions;

    /// <summary>This lock protects the data structures below.</summary>
    public static Lock? s_loopHoistStatsLock;

    public static int s_loopsConsidered;

    public static int s_loopsWithHoistedExpressions;

    public static int s_totalHoistedExpressions;
#endif

#if TRACK_ENREG_STATS
    public static EnregisterStats s_enregisterStats;
#endif

    public JitMetrics Metrics;

    // Max value of scope count for which we would use linear search; for larger values we would use hashtable lookup.
    public const int MAX_LINEAR_FIND_LCL_SCOPELIST = 32;

    public EntryState stackState;

    /// <summary>Address of global cookie for unsafe buffer checks</summary>
    public unsafe GSCookie* gsGlobalSecurityCookieAddr;

    /// <summary>Value of global cookie if addr is null</summary>
    public GSCookie gsGlobalSecurityCookieVal;

    /// <summary>Table used by shadow param analysis code</summary>
    public ShadowParamVarInfo? gsShadowVarInfo;

    public int gsShadowVarInfoCount;

#if DEBUG
    private NodeToTestDataMap? _nodeTestData;

    private const int FIRST_LOOP_HOIST_CSE_CLASS = 1000;

    /// <summary>LoopHoist test annotations turn into CSE requirements</summary>
    /// <remarks>we label them with CSE Class #'s starting at FIRST_LOOP_HOIST_CSE_CLASS. Current kept in this.</remarks>
    private int _loopHoistCSEClass = FIRST_LOOP_HOIST_CSE_CLASS;
#endif

    public FieldSeqStore? _fieldSeqStore;

    public InlineArrayMemoryKindCount<NodeToUnsignedMap> _memorySsaMap;

    // The Refany type is the only struct type whose structure is implicitly assumed by IL.  We need its fields.
    public unsafe CORINFO_CLASS_HANDLE _refAnyClass;

#if VARSET_COUNTOPS
    public static BitSetSupport.BitSetOpCounter _varsetOpCounter;
#endif

#if ALLVARSET_COUNTOPS
    public static BitSetSupport.BitSetOpCounter _allvarsetOpCounter;
#endif

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    public FpStructLoweringMap? _fpStructLoweringCache;
#endif

#if TARGET_AMD64
    // The following are for initializing register allocator "constants" defined in targetamd64.h
    // that now depend upon runtime ISA information, e.g., the presence of AVX512, which increases
    // the number of simd (xmm, ymm, and zmm) registers from 16 to 32.
    // As only 64-bit xarch has the capability to have the additional registers, we limit the changes
    // to TARGET_AMD64 only.
    //
    // Users of these values need to define four accessor functions:
    //
    //    regMaskFlt get_RBM_ALLFLOAT();
    //    regMaskFlt get_RBM_FLT_CALLEE_TRASH();
    //    int get_CNT_CALLEE_TRASH_FLOAT();
    //    int get_AVAILABLE_REG_COUNT();
    //
    // which return the values of these variables.
    //
    // This was done to avoid polluting all `targetXXX.h` macro definitions with a compiler parameter, where only
    // TARGET_AMD64 requires one.

    private regMaskFlt rbmAllFloat;

    internal regMaskFlt rbmFltCalleeTrash;

    private int cntCalleeTrashFloat;

    internal regMaskInt rbmAllInt;

    internal regMaskInt rbmIntCalleeTrash;

    private int cntCalleeTrashInt;

    private regNumber regIntLast;
#endif

#if TARGET_XARCH
    // The following are for initializing register allocator "constants" defined in targetamd64.h
    // that now depend upon runtime ISA information, e.g., the presence of AVX512, which adds
    // 8 mask registers for use.
    //
    // Users of these values need to define four accessor functions:
    //
    //    regMaskMsk get_RBM_ALLMASK();
    //    regMaskMsk get_RBM_MSK_CALLEE_TRASH();
    //    int get_CNT_CALLEE_TRASH_MASK();
    //    int get_AVAILABLE_REG_COUNT();
    //
    // which return the values of these variables.
    //
    // This was done to avoid polluting all `targetXXX.h` macro definitions with a compiler parameter, where only
    // TARGET_XARCH requires one.

    private regMaskMsk rbmAllMask;
    
    internal regMaskMsk rbmMskCalleeTrash;

    private int cntCalleeTrashMask;

    private InlineArrayTypCount<int> varTypeCalleeTrashRegMasks;
#endif

#if DEBUG
    private static ConfigMethodRange fJitStressRange;
#endif

    public unsafe Compiler(CORINFO_METHOD_HANDLE methodHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, InlineInfo? inlineInfo)
    {
        impInlineInfo = inlineInfo;
        impPendingBlockMembers = [];
        impSpillCliquePredMembers = [];
        impSpillCliqueSuccMembers = [];
        genIPmappings = [];
        genRichIPmappings = [];

        info.compCompHnd = jitInfo;
        info.compMethodHnd = methodHandle;
        info.compMethodInfo = methodInfo;
        info.compClassHnd = jitInfo->getMethodClass(methodHandle);

#if DEBUG
        if (compIsForInlining)
        {
            verbose = impInlineInfo.InlinerCompiler.verbose;
        }
#endif

#if DEBUG || LATE_DISASM || DUMP_FLOWGRAPHS || DUMP_GC_TABLES
        info.compMethodName = eeGetMethodName(methodHandle);
        info.compClassName = eeGetClassName(info.compClassHnd);
        info.compFullName = eeGetMethodFullName(methodHandle);

        fixed (byte* pName = "SuperPMIMethodContextNumber"u8)
        {
            info.compMethodSpmiIndex = CILJit.s_jitHost->getIntConfigValue(pName, -1);
        }

        if (!compIsForInlining)
        {
            JitMetadata.report(this, JitMetadata.MethodFullName, info.compFullName);
        }
#endif

#if DEBUG
        // Opt-in to jit stress based on method hash ranges.
        //
        // Note the default (with JitStressRange not set) is that all
        // methods will be subject to stress.
        fJitStressRange.EnsureInit(JitConfig[ConfigString.JitStressRange]);
        assert(!fJitStressRange.Error);

        if (fJitStressRange.Contains(info.compMethodHash()))
        {
            var jitStressOnlyMethodSet = JitConfig[ConfigMethodSet.JitStressOnly];
            compAllowStress = jitStressOnlyMethodSet.isEmpty() || jitStressOnlyMethodSet.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args);
        }
#endif

        if (compIsForInlining)
        {
            _inlineStrategy = null;
            compInlineResult = impInlineInfo.inlineResult;
        }
        else
        {
            _inlineStrategy = new InlineStrategy(this);
            compInlineResult = null;
        }

        for (var i = 0; i < (int)(TYP_COUNT); i++)
        {
            fgBigOffsetMorphingTemps[i] = BAD_VAR_NUM;
        }

#if DEBUG
        if (!compIsForInlining)
        {
            var noStructPromotionValue = JitConfig[ConfigInteger.JitNoStructPromotion];
            assert(noStructPromotionValue is >= 0 and <= 2);

            if (noStructPromotionValue == 1)
            {
                fgNoStructPromotion = true;
            }

            if (noStructPromotionValue == 2)
            {
                fgNoStructParamPromotion = true;
            }
        }
#endif

        structPromotionHelper = new StructPromotionHelper(this);

        if (!compIsForInlining)
        {
            codeGen = getCodeGenerator(this);
            hashBv.Init(this);

            //
            // Initialize all the per-method statistics gathering data structures.
            //
#if MEASURE_NODE_SIZE
            genNodeSizeStatsPerFunc.Init();
#endif
        }

#if DEBUG
        if (!compIsForInlining)
        {
            compDoComponentUnitTestsOnce();
        }
#endif

        // check that HelperCallProperties are initialized
        assert(CORINFO_HELP_GET_GCSTATIC_BASE.IsPure);

        virtualStubParamInfo = new VirtualStubParamInfo(IsTargetAbi(CORINFO_NATIVEAOT_ABI));

        // compMatchedVM is set to true if both CPU/ABI and OS are matching the execution engine requirements
        //
        // Do we have a matched VM? Or are we "abusing" the VM to help us do JIT work (such as using an x86 native VM
        // with an ARM-targeting "altjit").
        // Match CPU/ABI for compMatchedVM
        info.compMatchedVM = info.compCompHnd->getExpectedTargetArchitecture() == CORINFO_ARCH_TARGET;

        // Match OS for compMatchedVM
        var eeInfo = eeGetEEInfo();

#if TARGET_OS_RUNTIMEDETERMINED
        noway_assert(TargetOS.OSSettingConfigured);
#endif

        if (TargetOS.IsApplePlatform)
        {
            info.compMatchedVM = info.compMatchedVM && (eeInfo.osType == CORINFO_APPLE);
        }
        else if (TargetOS.IsUnix)
        {
            if (TargetArchitecture.IsX64)
            {
                // Apple x64 uses the Unix jit variant in crossgen2, not a special jit
                info.compMatchedVM &= ((eeInfo.osType == CORINFO_UNIX) || (eeInfo.osType == CORINFO_APPLE));
            }
            else
            {
                info.compMatchedVM &= (eeInfo.osType == CORINFO_UNIX);
            }
        }
        else if (TargetOS.IsWindows)
        {
            info.compMatchedVM &= (eeInfo.osType == CORINFO_WINNT);
        }

        compMaxUncheckedOffsetForNullObject = (int)(eeInfo.maxUncheckedOffsetForNullObject);

#if DEBUG && TARGET_WASM
        // TODO-WASM: remove once we no longer need to use x86/arm collections for wasm replay
        // if we are cross-replaying wasm, override compMaxUncheckedOffsetForNullObject
        if (!info.compMatchedVM)
        {
            compMaxUncheckedOffsetForNullObject = 1024 - 1;
        }
#endif

        info.compCode = methodInfo->ILCode;
        info.compILCodeSize = methodInfo->ILCodeSize;
    }

    public BasicBlockSimpleList Blocks => new BasicBlockSimpleList(fgFirstBB);

    public int CurLVEpoch => lvaCurEpoch;

    public FieldSeqStore FieldSeqStore
    {
        get
        {
            var fieldSeqStore = impInlineRoot._fieldSeqStore;

            if (fieldSeqStore is null)
            {
                fieldSeqStore = new FieldSeqStore();
                impInlineRoot._fieldSeqStore = fieldSeqStore;
            }
            return fieldSeqStore;
        }
    }

    public unsafe bool IsAot => opts.jitFlags->IsSet(JitFlags.JIT_FLAG_AOT);

    public bool IsFullPtrRegMapRequired
    {
        get
        {
            assert(Debugger.IsAttached || (codeGen is not null));
            return (codeGen is not null) && codeGen.IsFullPtrRegMapRequired;
        }

        set
        {
            assert(codeGen is not null);
            codeGen.IsFullPtrRegMapRequired = value;
        }
    }

    public unsafe bool IsNativeAot => IsAot && IsTargetAbi(CORINFO_NATIVEAOT_ABI);

    public unsafe bool IsReadyToRun => IsAot && !IsTargetAbi(CORINFO_NATIVEAOT_ABI);

#if FEATURE_JIT_METHOD_PERF
    private static string? _jitTimeLogCsv;

    public static unsafe string JitTimeLogCsv
    {
        get
        {
            var jitTimeLogCsv = _jitTimeLogCsv;

            if (jitTimeLogCsv is null)
            {
                var pJitTimeLogCsvUtf8 = JitConfig[ConfigString.JitTimeLogCsv];
                var jitTimeLogCsvUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pJitTimeLogCsvUtf8);

                jitTimeLogCsv = Encoding.UTF8.GetString(jitTimeLogCsvUtf8);
                _jitTimeLogCsv = jitTimeLogCsv; 
            }
            return jitTimeLogCsv;
        }
    }
#endif

    public bool MethodHasBoundsChecks
    {
        get
        {
            return (optMethodFlags & OMF_HAS_BOUNDS_CHECKS) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_BOUNDS_CHECKS) | (value ? OMF_HAS_BOUNDS_CHECKS : 0);
        }
    }

    public bool MethodHasExpandableCasts
    {
        get
        {
            return (optMethodFlags & OMF_HAS_EXPANDABLE_CAST) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_EXPANDABLE_CAST) | (value ? OMF_HAS_EXPANDABLE_CAST : 0);
        }
    }

    public bool MethodHasExpRuntimeLookup
    {
        get
        {
            return (optMethodFlags & OMF_HAS_EXPRUNTIMELOOKUP) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_EXPRUNTIMELOOKUP) | (value ? OMF_HAS_EXPRUNTIMELOOKUP : 0);
        }
    }

    public bool MethodHasFatPointer
    {
        get
        {
            return (optMethodFlags & OMF_HAS_FATPOINTER) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_FATPOINTER) | (value ? OMF_HAS_FATPOINTER : 0);
        }
    }

    public bool MethodHasGuardedDevirtualization
    {
        get
        {
            return (optMethodFlags & OMF_HAS_GUARDEDDEVIRT) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_GUARDEDDEVIRT) | (value ? OMF_HAS_GUARDEDDEVIRT : 0);
        }
    }

    public bool MethodHasPatchpoint
    {
        get
        {
            return (optMethodFlags & OMF_HAS_PATCHPOINT) != 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_PATCHPOINT) | (value ? OMF_HAS_PATCHPOINT : 0);
        }
    }

    public bool MethodHasRecursiveTailCall
    {
        get
        {
            return (optMethodFlags & OMF_HAS_RECURSIVE_TAILCALL) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_RECURSIVE_TAILCALL) | (value ? OMF_HAS_RECURSIVE_TAILCALL : 0);
        }
    }

    public bool MethodHasSpecialIntrinsics
    {
        get
        {
            return (optMethodFlags & OMF_HAS_SPECIAL_INTRINSICS) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_SPECIAL_INTRINSICS) | (value ? OMF_HAS_SPECIAL_INTRINSICS : 0);
        }
    }

    public bool MethodHasStackAllocatedArray
    {
        get
        {
            return (optMethodFlags & OMF_HAS_STACK_ARRAY) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_STACK_ARRAY) | (value ? OMF_HAS_STACK_ARRAY : 0);
        }
    }

    public bool MethodHasStaticInit
    {
        get
        {
            return (optMethodFlags & OMF_HAS_STATIC_INIT) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_STATIC_INIT) | (value ? OMF_HAS_STATIC_INIT : 0);
        }
    }

    public bool MethodHasTlsFieldAccess
    {
        get
        {
            return (optMethodFlags & OMF_HAS_TLS_FIELD) is not 0;
        }

        set
        {
            optMethodFlags = (optMethodFlags & ~OMF_HAS_TLS_FIELD) | (value ? OMF_HAS_TLS_FIELD : 0);
        }
    }

    public bool NeedsGSSecurityCookie
    {
        get
        {
            return compNeedsGSSecurityCookie;
        }

        set
        {
#if TARGET_WASM
#if DEBUG
            compGSSecurityCheckBlocker = "Not currently enabled for Wasm";
#endif
            return;
#endif

            if (opts.compDbgEnC)
            {
#if DEBUG
                compGSSecurityCheckBlocker = "incompatible with EnC";
#endif
            }
            else
            {
                compGSReorderStackLayout = true;
                compNeedsGSSecurityCookie = true;
            }
        }
    }

    public NodeToLoopMemoryBlockMap NodeToLoopMemoryBlockMap
    {
        get
        {
            var nodeToLoopMemoryBlockMap = _nodeToLoopMemoryBlockMap;

            if (nodeToLoopMemoryBlockMap is null)
            {
                nodeToLoopMemoryBlockMap = [];
                _nodeToLoopMemoryBlockMap = nodeToLoopMemoryBlockMap;
            }
            return nodeToLoopMemoryBlockMap;
        }
    }

    public NodeToTestDataMap NodeTestData
    {
        get
        {
            var nodeTestData = impInlineRoot._nodeTestData;

            if (nodeTestData is null)
            {
                nodeTestData = [];
                impInlineRoot._nodeTestData = nodeTestData;
            }
            return nodeTestData;
        }
    }

    public bool PreciseRefCountsRequired =>  opts.OptimizationEnabled;

#if TARGET_AMD64
    public int CNT_CALLEE_TRASH_FLOAT => cntCalleeTrashFloat;

    public int CNT_CALLEE_TRASH_INT => cntCalleeTrashInt;

    public regMaskFlt RBM_ALLFLOAT => rbmAllFloat;

    public regMaskInt RBM_ALLINT => rbmAllInt;

    public regMaskFlt RBM_FLT_CALLEE_TRASH => rbmFltCalleeTrash;

    public regMaskInt RBM_INT_CALLEE_TRASH => rbmIntCalleeTrash;

    public int REG_INT_COUNT => REG_INT_LAST - REG_INT_FIRST + 1;

    public regNumber REG_INT_LAST => regIntLast;
#endif

#if TARGET_XARCH
    public regMaskMsk RBM_ALLMASK => rbmAllMask;

    public regMaskMsk RBM_MSK_CALLEE_TRASH => rbmMskCalleeTrash;

    public int CNT_CALLEE_TRASH_MASK => cntCalleeTrashMask;
#endif

    /// <summary>Are we running a replay under SuperPMI?</summary>
#if DEBUG
    public bool RunningSuperPmiReplay => info.compMethodSpmiIndex is not -1;
#else
    // Note: you can certainly run a SuperPMI replay with a non-DEBUG JIT, and if necessary and useful we could make compMethodSuperPMIIndex always available.
    public bool RunningSuperPmiReplay => false;
#endif

    /// <summary>Returns underlying type of handles returned by ldtoken instruction</summary>
    /// <remarks>RuntimeTypeHandle is backed by raw pointer on NativeAOT and by object reference on other runtimes</remarks>
    public var_types RuntimeHandleUnderlyingType => IsTargetAbi(CORINFO_NATIVEAOT_ABI) ? TYP_I_IMPL : TYP_REF;

    public SignatureToLookupInfoMap SignatureToLookupInfoMap
    {
        get
        {
            var signatureToLookupInfoMap = _signatureToLookupInfoMap;

            if (signatureToLookupInfoMap is null)
            {
                signatureToLookupInfoMap = [];
                _signatureToLookupInfoMap = signatureToLookupInfoMap;
            }
            return signatureToLookupInfoMap;
        }
    }

#if DEBUG
    /// <summary>Should we use only ASCII characters for tree dumps?</summary>
    /// <remarks>This is set to default to 1 in JitConfig</remarks>
    public bool ShouldDumpAsciiTrees => JitConfig[ConfigInteger.JitDumpASCII] == 1;

    public bool ShouldUseVerboseSsa => JitConfig[ConfigInteger.JitDumpVerboseSsa] == 1;

    public bool ShouldUseVerboseTrees => JitConfig[ConfigInteger.JitDumpVerboseTrees] == 1;
#endif

    private ClassLayoutTable typClassLayoutTable
    {
        get
        {
            var result = _classLayoutTable;

            if (result is null)
            {
                result = CreateClassLayoutTable(this);
                _classLayoutTable = result;
            }
            return result;

            static ClassLayoutTable CreateClassLayoutTable(Compiler compiler)
            {
                assert(compiler._classLayoutTable is null);
                ClassLayoutTable? result;

                if (compiler.compIsForInlining)
                {
                    var inlinerCompiler = compiler.impInlineInfo.InlinerCompiler;
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

    private bool UsesSimdTypes
    {
        get
        {
            return _usesSimdTypes;
        }

        set
        {
            _usesSimdTypes = value;
        }
    }

    /// <summary>Returns the codegen type for a given simd size.</summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static var_types GetSimdTypeForSize(int size) => size switch {
        8 => TYP_SIMD8,
        12 => TYP_SIMD12,
        16 => TYP_SIMD16,
#if TARGET_XARCH
        32 => TYP_SIMD32,
        64 => TYP_SIMD64,
#elif TARGET_ARM64
        SIZE_UNKNOWN => TYP_SIMD,
#endif
        _ => TYP_UNDEF,
    };

    [Conditional("DEBUG")]
    public void assertImp([DoesNotReturnIf(false)] bool condition, GenTree? op1 = null, GenTree? op2 = null, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "", [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        if (!condition)
        {
            assertAbort($"{conditionExpression} : Possibly bad IL with CEE_{impCurOpcName} at offset {impCurOpcOffs:X4} (op1={(op1 is not null ? op1.Type.Name : "NULL")} op2={(op2 is not null ? op2.Type.Name : "NULL")} stkDepth={stackState.esStackDepth})\"", filePath, lineNumber);
        }
    }

    /// <summary>begin execution of a phase</summary>
    /// <param name="phase">the phase that is about to begin</param>
    public void BeginPhase(Phases phase)
    {
        mostRecentlyActivePhase = phase;
    }

    public bool BlockNonDeterministicIntrinsics(bool mustExpand)
    {
        // We explicitly block these APIs from being expanded in R2R
        // since we know they are non-deterministic across hardware

        if (IsReadyToRun)
        {
            if (mustExpand)
            {
                implReadyToRunUnsupported();
            }
            return true;
        }
        return false;
    }

    /// <summary>finish execution of a phase</summary>
    /// <param name="phase">the phase that has just finished</param>
    public void EndPhase(Phases phase)
    {
#if FEATURE_JIT_METHOD_PERF
        compJitTimer?.EndPhase(this, phase);
#endif

        mostRecentlyActivePhase = phase;
    }

#if TARGET_XARCH
    /// <summary>Answer the question: Is Vex encoding supported on this target</summary>
    /// <returns></returns>
    public bool canUseVexEncoding() => compOpportunisticallyDependsOn(InstructionSet_AVX);

    /// <summary>Answer the question: Is Evex encoding supported on this target</summary>
    /// <returns></returns>
    public bool canUseEvexEncoding() => compOpportunisticallyDependsOn(InstructionSet_AVX512);

#if DEBUG
    /// <summary>Answer the question: Is Evex encoding supported on this target.</summary>
    /// <returns>`true` if Evex encoding is supported, `false` if not.</returns>
    public bool canUseEvexEncodingDebugOnly() => compIsaSupportedDebugOnly(InstructionSet_AVX512);
#endif

    /// <summary>Answer the question: Are APX encodings supported on this target.</summary>
    /// <returns></returns>
    public bool canUseApxEncoding() => compOpportunisticallyDependsOn(InstructionSet_APX);

    /// <summary>Answer the question: Are APX-EVEX encodings supported on this target.</summary>
    /// <returns></returns>
    public bool canUseApxEvexEncoding() => canUseApxEncoding() && canUseEvexEncoding();
#endif

    public unsafe bool checkTailCallConstraint(OPCODE opcode, in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_RESOLVED_TOKEN constrainedResolvedToken)
    {
        assert(impOpcodeIsCallOpcode(opcode));

        if (compIsForInlining)
        {
            return false;
        }

        CORINFO_SIG_INFO sig;
        CorInfoFlag mflags;
        CorInfoFlag methodClassFlgs;
        CORINFO_CLASS_HANDLE methodClassHnd;

        if (opcode is CEE_CALLI)
        {
            // For calli, check that this is not a virtual method.
            eeGetSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out sig);

            // We don't know the target method, so we have to infer the flags, or assume the worst-case.
            mflags = ((sig.callConv & CORINFO_CALLCONV_HASTHIS) is not 0) ? 0 : CORINFO_FLG_STATIC;

            methodClassFlgs = 0;
            methodClassHnd = NO_CLASS_HANDLE;
        }
        else
        {
            var methodHnd = resolvedToken.hMethod;
            mflags = info.compCompHnd->getMethodAttribs(methodHnd);

            // In generic code we pair the method handle with its owning class to get the exact method signature.
            methodClassHnd = resolvedToken.hClass;
            assert(methodClassHnd != NO_CLASS_HANDLE);

            eeGetMethodSig(methodHnd, out sig, methodClassHnd);

            // opcode specific check
            methodClassFlgs = info.compCompHnd->getClassAttribs(methodClassHnd);
        }

        if ((sig.callConv & CORINFO_CALLCONV_MASK) is CORINFO_CALLCONV_VARARG)
        {
            eeGetCallSiteSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out sig);
        }

        // Check compatibility of the arguments.
        var argCount = sig.numArgs;

        CORINFO_ARG_LIST_HANDLE args;
        args = sig.args;

        while (argCount-- is not 0)
        {
            // For unsafe code, we might have parameters containing pointer to the stack location.
            // Disallow the tailcall for this kind.

            CORINFO_CLASS_HANDLE classHandle;
            var ciType = strip(info.compCompHnd->getArgType(&sig, args, &classHandle));

            if (ciType is CORINFO_TYPE_PTR or CORINFO_TYPE_BYREF)
            {
                return false;
            }

            // Check that the argument is not a byref-like for tailcalls.
            if ((ciType is CORINFO_TYPE_VALUECLASS) && eeIsByrefLike(classHandle))
            {
                return false;
            }

            args = info.compCompHnd->getArgNext(args);
        }

        var popCount = sig.totalILArgs();

        // Check for 'this' which is on non-static methods, not called via NEWOBJ
        if ((mflags & CORINFO_FLG_STATIC) is 0)
        {
            if (opcode is CEE_CALLI)
            {
                // For CALLI, we don't know the methodClassHnd. Therefore, let's check the "this" object on the stack.
                if (impStackTop(popCount).val.Type is not TYP_REF)
                {
                    return false;
                }
            }
            else
            {
                // Check that the "this" argument is not a byref.
                if (TypeHandleToVarType(methodClassHnd) != TYP_REF)
                {
                    return false;
                }
            }
        }

        // Tail calls on constrained calls should be illegal too:
        // when instantiated at a value type, a constrained call may pass the address of a stack allocated value
        if (!Unsafe.IsNullRef(in constrainedResolvedToken))
        {
            return false;
        }

        // Get the exact view of the signature for an array method
        if (sig.retType != CORINFO_TYPE_VOID)
        {
            if ((methodClassFlgs & CORINFO_FLG_ARRAY) is not 0)
            {
                assert(opcode != CEE_CALLI);
                eeGetCallSiteSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out sig);
            }
        }

        var calleeRetType = sig.retType.VarType.ActualType;
        var callerRetType = info.compMethodInfo->args.retType.VarType.ActualType;

        if (calleeRetType is TYP_FLOAT)
        {
            // Normalize TYP_FLOAT to TYP_DOUBLE (it is ok to return one as the other and vice versa).
            calleeRetType = TYP_DOUBLE;
            callerRetType = TYP_DOUBLE;
        }

        // Make sure the types match.

        if (calleeRetType != callerRetType)
        {
            return false;
        }
        else if ((callerRetType is TYP_STRUCT) && (sig.retTypeClass != info.compMethodInfo->args.retTypeClass))
        {
            return false;
        }

        // For tailcall, stack must be empty.
        if (stackState.esStackDepth != popCount)
        {
            return false;
        }

        // Yes, tailcall is legal
        return true;
    }

    public nint dspOffset(nint offs)
    {
#if DEBUG
        if (offs != 0)
        {
            if (opts.dspDiffable)
            {
                offs = unchecked((nint)(0xD1FFAB1E));
            }
        }
#endif
        return offs;
    }

    public unsafe nint dspPtr(void* ptr) => dspOffset(unchecked((nint)(ptr)));

    private void FreeBlockListNode(BlockListNode node)
    {
        node.Next = impBlockListNodeFreeList;
        impBlockListNodeFreeList = node;
    }

    public void FinalizeEH()
    {
        // We should not make any more alterations to the EH table structure.
        ehTableFinalized = true;
    }

    public void generatePatchpointInfo()
    {
        // TODO: Port Compiler.generatePatchpointInfo
    }

    /// <summary>Return the length for an allocation whose length is represented by GT_ARR_LENGTH.</summary>
    /// <param name="tree">The array allocation helper call.</param>
    /// <returns>Return the array length node.</returns>
    public GenTree? getArrayLengthFromAllocation(GenTree tree)
    {
        assert(tree is not null);
        var arrayLength = null as GenTree;

        if (tree.Oper.IsCall)
        {
            var call = tree.AsCall();

            if (call.IsHelperCall())
            {
                var helper = call.HelperNum;

                switch (helper)
                {
                    case CORINFO_HELP_NEWARR_1_MAYBEFROZEN:
                    case CORINFO_HELP_NEWARR_1_DIRECT:
                    case CORINFO_HELP_NEWARR_1_PTR:
                    case CORINFO_HELP_NEWARR_1_VC:
                    case CORINFO_HELP_NEWARR_1_ALIGN8:
                    {
                        // This is an array allocation site. Grab the array length node.

                        var callArg = call.Args.GetUserArgByIndex(1);
                        assert(callArg is not null);

                        arrayLength = callArg.Node;
                        break;
                    }

                    default:
                        break;
                }

                assert((arrayLength is null) || ((optMethodFlags & OMF_HAS_NEWARRAY) != 0));
            }
            else if (call.IsSpecialIntrinsic(this, NI_System_String_FastAllocateString))
            {
                // String characters start at a different offset than array data, but string length itself is a GT_ARR_LENGTH.
                assert(call.Args.CountUserArgs() == 2);

                var callArg = call.Args.GetUserArgByIndex(1);
                assert(callArg is not null);

                arrayLength = callArg.Node;
            }
        }

        if (arrayLength is not null)
        {
            arrayLength = arrayLength.Oper.IsPutArg ? arrayLength.AsUnOp().Op1 : arrayLength;
        }
        return arrayLength;
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

    /// <summary>get a lookup tree</summary>
    /// <param name="lookup">the lookup to get the tree for</param>
    /// <param name="handleFlags">flags to set on the result node</param>
    /// <param name="compileTimeHandle">compile-time handle corresponding to the lookup</param>
    /// <returns>A node representing the lookup tree</returns>
    public unsafe GenTree getLookupTree(in CORINFO_LOOKUP lookup, GenTreeFlags handleFlags, void* compileTimeHandle)
    {
        if (!lookup.lookupKind.needsRuntimeLookup)
        {
            // No runtime lookup is required.
            // Access is direct or memory-indirect (of a fixed address) reference

            var handle = (CORINFO_GENERIC_HANDLE)(null);
            var pIndirection = (void*)(null);

            assert(lookup.constLookup.accessType is not IAT_PPVALUE and not IAT_RELPVALUE);

            if (lookup.constLookup.accessType is IAT_VALUE)
            {
                handle = lookup.constLookup.handle;
            }
            else if (lookup.constLookup.accessType is IAT_PVALUE)
            {
                pIndirection = lookup.constLookup.addr;
            }

            return gtNewIconEmbHndNode(handle, pIndirection, handleFlags, compileTimeHandle);
        }
        return getRuntimeLookupTree(lookup, compileTimeHandle);
    }

    // getMaxVectorByteLength
    // The minimum simd size supported by System.Numeric.Vectors or System.Runtime.Intrinsic
    // Arm.AdvSimd:  16-byte Vector<T> and Vector128<T>
    // X86.SSE:      16-byte Vector<T> and Vector128<T>
    // X86.AVX:      16-byte Vector<T> and Vector256<T>
    // X86.AVX2:     32-byte Vector<T> and Vector256<T>
    // X86.AVX512:   32-byte Vector<T> and Vector512<T>
    public int GetMaxVectorByteLength()
    {
#if TARGET_XARCH
        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            return ZMM_REGSIZE_BYTES;
        }
        else if (compOpportunisticallyDependsOn(InstructionSet_AVX))
        {
            return YMM_REGSIZE_BYTES;
        }
        else
        {
            return XMM_REGSIZE_BYTES;
        }
#elif TARGET_ARM64
        return FP_REGSIZE_BYTES;
#else
        unreached();
        return 0;
#endif
    }

    public unsafe CORINFO_CLASS_HANDLE getMethodInstantiationArgument(CORINFO_METHOD_HANDLE ftn, int index)
        => info.compCompHnd->getMethodInstantiationArgument(ftn, index);

    public int GetMinVectorByteLength() => (int)(TYP_SIMD8.EmitSize);

    /// <inheritdoc cref="GetReturnTypeForStruct(CORINFO_CLASS_HANDLE, CorInfoCallConvExtension, out structPassingKind, int)" />
    public unsafe var_types GetReturnTypeForStruct(CORINFO_CLASS_HANDLE clsHnd, CorInfoCallConvExtension callConv, int structSize = 0)
        => GetReturnTypeForStruct(clsHnd, callConv, out _, structSize);

    /// <summary>Get the type that is used to return values of the given struct type.</summary>
    /// <param name="clsHnd"></param>
    /// <param name="callConv"></param>
    /// <param name="wbPassStruct"></param>
    /// <param name="structSize"></param>
    /// <returns></returns>
    /// <remarks>If the size is unknown, pass 0 and it will be determined from 'clsHnd'.</remarks>
    public unsafe var_types GetReturnTypeForStruct(CORINFO_CLASS_HANDLE clsHnd, CorInfoCallConvExtension callConv, out structPassingKind wbPassStruct, int structSize = 0)
    {
        // TODO: Port getReturnTypeForStruct
        wbPassStruct = default;
        return TYP_UNKNOWN;
    }

    /// <summary>get a tree for a runtime lookup</summary>
    /// <param name="lookup">the lookup to get the tree for</param>
    /// <param name="compileTimeHandle">compile-time handle corresponding to the lookup</param>
    /// <returns>A node representing the runtime lookup tree</returns>
    public unsafe GenTree getRuntimeLookupTree(in CORINFO_LOOKUP lookup, void* compileTimeHandle)
    {
        ref var runtimeLookup = ref lookup.runtimeLookup;

        // If pRuntimeLookup->indirections is equal to CORINFO_USEHELPER, it specifies that a run-time helper should be
        // used; otherwise, it specifies the number of indirections via pRuntimeLookup->offsets array.

        if ((runtimeLookup.indirections is CORINFO_USEHELPER or CORINFO_USENULL) || runtimeLookup.testForNull)
        {
            return gtNewRuntimeLookupHelperCallNode(runtimeLookup, getRuntimeContextTree(lookup.lookupKind.runtimeLookupKind), compileTimeHandle);
        }

        var result = getRuntimeContextTree(lookup.lookupKind.runtimeLookupKind);
        GenTreeStack stmts = [];

        static GenTree cloneTree(Compiler compiler, GenTreeStack stmts, ref GenTree tree, string reason)
        {
            if ((tree.Flags & GTF_GLOB_EFFECT) is 0)
            {
                var clone = compiler.gtClone(tree, complexOK: true);

                if (clone is not null)
                {
                    return clone;
                }
            }

            var temp = compiler.lvaGrabTemp(shortLifetime: true, reason);
            stmts.Push(compiler.gtNewTempStore(temp, tree));

            var actualType = compiler.lvaGetDesc(temp).Type.ActualType;

            tree = compiler.gtNewLclvNode(actualType, temp);
            return compiler.gtNewLclvNode(actualType, temp);
        }

        // Apply repeated indirections
        for (var i = 0; i < runtimeLookup.indirections; i++)
        {
            var preInd = null as GenTree;

            var isFirstOrSecondOffset = ((i is 1) && runtimeLookup.indirectFirstOffset) || ((i is 2) && runtimeLookup.indirectSecondOffset);

            if (isFirstOrSecondOffset)
            {
                preInd = cloneTree(this, stmts, ref result, "getRuntimeLookupTree indirectOffset");
            }

            if (i is not 0)
            {
                result = gtNewIndir(TYP_I_IMPL, result, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
            }

            if (isFirstOrSecondOffset)
            {
                assert(preInd is not null);
                result = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, preInd, result);
            }

            if (runtimeLookup.offsets[i] is not 0)
            {
                result = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, result, gtNewIconNode(TYP_I_IMPL, runtimeLookup.offsets[i]));
            }
        }

        assert(!runtimeLookup.testForNull);

        if (runtimeLookup.indirections > 0)
        {
            result = gtNewIndir(TYP_I_IMPL, result, GTF_IND_NONFAULTING);
        }

        // Produces GT_COMMA(stmt1, GT_COMMA(stmt2, ... GT_COMMA(stmtN, result)))

        while (stmts.Count is not 0)
        {
            result = gtNewCommaNode(TYP_I_IMPL, stmts.Pop(), result);
        }

        DISPTREE(result);
        return result;
    }

    public unsafe int GetSimdTypeSizeInBytes(CORINFO_CLASS_HANDLE typeHnd)
    {
        _ = getBaseTypeAndSizeOfSimdType(typeHnd, out var sizeBytes);
        return sizeBytes;
    }

    public unsafe CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, int index)
        => info.compCompHnd->getTypeInstantiationArgument(cls, index);

    // Get the number of bytes in a System.Numeric.Vector<T> for the current compilation.
    // Note - cannot be used for System.Runtime.Intrinsic
    public int GetVectorTByteLength()
    {
        // We need to report the ISA dependency to the VM so that scenarios
        // such as R2R work correctly for larger vector sizes, so we always
        // do `compExactlyDependsOn` for such cases.

#if TARGET_XARCH
        if (compExactlyDependsOn(InstructionSet_VectorT512))
        {
            assert(!compIsaSupportedDebugOnly(InstructionSet_VectorT256));
            assert(!compIsaSupportedDebugOnly(InstructionSet_VectorT128));
            return ZMM_REGSIZE_BYTES;
        }
        else if (compExactlyDependsOn(InstructionSet_VectorT256))
        {
            assert(!compIsaSupportedDebugOnly(InstructionSet_VectorT128));
            return YMM_REGSIZE_BYTES;
        }
        else if (compExactlyDependsOn(InstructionSet_VectorT128))
        {
            return XMM_REGSIZE_BYTES;
        }
        else
        {
            // TODO: We should be returning 0 here, but there are a number of
            // places that don't quite get handled correctly in that scenario
            return XMM_REGSIZE_BYTES;
        }
#elif TARGET_ARM64
#if DEBUG
        if ((JitConfig[ConfigInteger.JitUseScalableVectorT] != 0) && compExactlyDependsOn(InstructionSet_VectorT))
        {
            return SIZE_UNKNOWN;
        }
        else
#endif
            if (compExactlyDependsOn(InstructionSet_VectorT128))
            {
                return FP_REGSIZE_BYTES;
            }
            else
            {
                // TODO: We should be returning 0 here, but there are a number of
                // places that don't quite get handled correctly in that scenario
                return FP_REGSIZE_BYTES;
            }
#else
            NO_WAY("getVectorTByteLength() unimplemented on target arch");
            unreached();
            return 0;
#endif
    }

    public unsafe bool IsHwSimdClass(CORINFO_CLASS_HANDLE clsHnd)
    {
#if FEATURE_HW_INTRINSICS
        if (isIntrinsicType(clsHnd))
        {
            _ = getClassNameFromMetadata(clsHnd, out var namespaceName);
            return namespaceName.Equals("System.Runtime.Intrinsics", StringComparison.Ordinal);
        }
#endif
        return false;
    }

    /// <summary>Returns true if the type is returned in multiple registers</summary>
    /// <param name="hClass">type handle</param>
    /// <param name="callConv"></param>
    /// <returns>true if type is returned in multiple registers, false otherwise.</returns>
    public unsafe bool IsMultiRegReturnedType(CORINFO_CLASS_HANDLE hClass, CorInfoCallConvExtension callConv)
    {
        if (hClass == NO_CLASS_HANDLE)
        {
            return false;
        }

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
        var returnType = GetReturnTypeForStruct(hClass, callConv, out var howToReturnStruct);
        return varTypeIsStruct(returnType) && (howToReturnStruct is not SPK_PrimitiveType);
#else
        var returnType = GetReturnTypeForStruct(hClass, callConv, out _);
        return varTypeIsStruct(returnType);
#endif
    }

    public unsafe bool IsSimdClass(CORINFO_CLASS_HANDLE clsHnd)
    {
        if (isIntrinsicType(clsHnd))
        {
            _ = getClassNameFromMetadata(clsHnd, out var namespaceName);
            return namespaceName.Equals("System.Numerics", StringComparison.Ordinal);
        }
        return false;
    }

    public unsafe bool IsSimdOrHwSimdClass(CORINFO_CLASS_HANDLE clsHnd)
    {
        return IsSimdClass(clsHnd) || IsHwSimdClass(clsHnd);
    }

    public unsafe bool isSpanClass(CORINFO_CLASS_HANDLE clsHnd)
    {
        if (isIntrinsicType(clsHnd))
        {
            var className = getClassNameFromMetadata(clsHnd, out var namespaceName);
            return namespaceName.Equals("System", StringComparison.Ordinal) &&
                   (className.Equals("Span`1", StringComparison.Ordinal) || className.Equals("ReadOnlySpan`1", StringComparison.Ordinal));
        }
        return false;
    }

    public static bool IsStaticHelperEligibleForExpansion(GenTree tree)
        => IsStaticHelperEligibleForExpansion(tree, out _, out _);

    /// <summary> Determine whether this node is a static init helper eligible for late expansion</summary>
    /// <param name="tree">tree node</param>
    /// <param name="isGc">whether the helper returns GCStaticBase or NonGCStaticBase</param>
    /// <param name="retValKind">describes its return value</param>
    /// <returns>Returns true if eligible for late expansion</returns>
    public static bool IsStaticHelperEligibleForExpansion(GenTree tree, out bool isGc, out StaticHelperReturnValue retValKind)
    {
        isGc = false;
        retValKind = SHRV_STATIC_BASE_PTR;

        if (!tree.Oper.IsCall)
        {
            return false;
        }

        var call = tree.AsCall();

        if (!call.IsHelperCall())
        {
            return false;
        }

        switch (call.HelperNum)
        {
            case CORINFO_HELP_READYTORUN_GCSTATIC_BASE:
            case CORINFO_HELP_GET_GCSTATIC_BASE:
            case CORINFO_HELP_GETPINNED_GCSTATIC_BASE:
            {
                isGc = true;
                break;
            }

            case CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE:
            case CORINFO_HELP_GET_NONGCSTATIC_BASE:
            case CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE:
            {
                break;
            }

            // TODO: other helpers

            default:
            {
                return false;
            }
        }
        return true;
    }

    public unsafe bool IsSystemHalfClass(CORINFO_CLASS_HANDLE clsHnd)
    {
        if (isIntrinsicType(clsHnd))
        {
            var className = getClassNameFromMetadata(clsHnd, out var namespaceName);
            return className.Equals("Half", StringComparison.Ordinal) && namespaceName.Equals("System", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>Can the given local address be represented as "LCL_ADDR"?</summary>
    /// <param name="lclNum">The local's number</param>
    /// <param name="offset">The address' offset</param>
    /// <returns>Whether "LCL_ADDR&lt;lclNum&gt; [+offset]" would be valid IR.</returns>
    /// <remarks>Local address nodes cannot point beyond the local and can only store 16 bits worth of offset.</remarks>
    public bool IsValidLclAddr(int lclNum, int offset)
    {
#if TARGET_ARM64
        if (lvaIsUnknownSizeLocal(lclNum))
        {
            return offset is 0;
        }
#endif
        return (offset < ushort.MaxValue) && (offset < lvaLclExactSize(lclNum));
    }

    /// <summary>One line log function.</summary>
    /// <param name="level"></param>
    /// <param name="message"></param>
    /// <remarks>Default level == 0. Increasing it gives you more log information</remarks>
    [Conditional("DEBUG")]
    public void JITLOG(int level, string message)
    {
#if DEBUG
        if (verbose)
        {
            vflogf(jitstdout(), message);
        }
        _ = vlogf(level, message);
#endif
    }

#if DEBUG
    private static ConfigMethodRange fJitRange;

    public unsafe bool SkipMethod()
    {        
        fJitRange.EnsureInit(JitConfig[ConfigString.JitRange]);
        assert(!fJitRange.Error);

        // Normally JitConfig.JitRange() is null, we don't want to skip jitting any methods.
        // So, the logic below relies on the fact that a null range string passed to ConfigMethodRange represents the set of all methods.

        if (!fJitRange.Contains(info.compMethodHash()))
        {
            return true;
        }

        var jitExcludeMethodSet = JitConfig[ConfigMethodSet.JitExclude];

        if (jitExcludeMethodSet.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            return true;
        }

        var jitIncludeMethodSet = JitConfig[ConfigMethodSet.JitInclude];

        if (!jitIncludeMethodSet.isEmpty() && !jitIncludeMethodSet.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            return true;
        }
        return false;
    }
#endif

    public unsafe typeInfo makeTypeInfoForLocal(int lclNum)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);
        return (varDsc.Type is TYP_REF) ? new typeInfo(varDsc.lvClassHnd) : new typeInfo(varDsc.Type);
    }

    public unsafe typeInfo makeTypeInfo(CORINFO_CLASS_HANDLE clsHnd)
    {
        assert(clsHnd != NO_CLASS_HANDLE);
        return makeTypeInfo(info.compCompHnd->asCorInfoType(clsHnd), clsHnd);
    }

    public unsafe typeInfo makeTypeInfo(CorInfoType ciType, CORINFO_CLASS_HANDLE clsHnd)
    {
        return (ciType == CORINFO_TYPE_CLASS) ? new typeInfo(clsHnd) : new typeInfo(ciType.VarType);
    }

    /// <summary>Use to determine if a struct *might* be a simd type. As this function only takes a size, many structs will fit the criteria.</summary>
    /// <param name="structSize"></param>
    /// <returns></returns>
    public bool structSizeMightRepresentSimdType(nint structSize)
    {
#if FEATURE_SIMD
        return (structSize >= GetMinVectorByteLength()) && (structSize <= GetMaxVectorByteLength());
#else
        return false;
#endif
    }

    public bool IsTargetAbi(CORINFO_RUNTIME_ABI abi)
        => eeGetEEInfo().targetAbi == abi;

    /// <summary>Assumes called as part of process shutdown; does any compiler-specific work associated with that.</summary>
    public static unsafe void ProcessShutdownWork(ICorStaticInfo* staticInfo)
    {
    }

#if MEASURE_NOWAY
    public void RecordNowayAssert(ReadOnlySpan<char> filePath, int lineNumber, ReadOnlySpan<char> message)
    {
        // TODO: Port RecordNowayAssert
    }
#endif

    public var_types roundDownMaxType(int size)
    {
        assert(size > 0);

#if FEATURE_SIMD
        if (roundDownSimdSize(size) > 0)
        {
            return GetSimdTypeForSize(roundDownSimdSize(size));
        }
#endif

        var nearestPow2 = 1 << int.Log2(size);
        return int.Min(nearestPow2, REGSIZE_BYTES) switch {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_INT,
#if TARGET_64BIT
            8 => TYP_LONG,
#endif
            _ => TYP_UNDEF,
        };
    }

    public var_types roundDownMaxType(int size, bool conservative)
    {
        var result = roundDownMaxType(size);
#if FEATURE_SIMD && TARGET_XARCH
        if (conservative)
        {
            if (result == TYP_SIMD32)
            {
                result = compOpportunisticallyDependsOn(InstructionSet_AVX2) ? result : TYP_SIMD16;
            }
        }
#endif
        return result;
    }

    /// <summary>rounds the given size down to the nearest SIMD size available on the target.</summary>
    /// <param name="size">size of the data to process with SIMD</param>
    /// <returns></returns>
    public int roundDownSimdSize(int size)
    {
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
        var maxSize = GetPreferredVectorByteLength();
        assert(maxSize is (>= XMM_REGSIZE_BYTES and <= ZMM_REGSIZE_BYTES));

        if (size >= maxSize)
        {
            size = maxSize;
        }
        else if (size >= YMM_REGSIZE_BYTES)
        {
            if (maxSize >= YMM_REGSIZE_BYTES)
            {
                size = YMM_REGSIZE_BYTES;
            }
        }
        else if (size >= XMM_REGSIZE_BYTES)
        {
            size = XMM_REGSIZE_BYTES;
        }
        else
        {
            size = 0;
        }
        return size;
#elif (TARGET_ARM64)
        assert(GetMaxVectorByteLength() is FP_REGSIZE_BYTES);
        return (size >= FP_REGSIZE_BYTES) ? FP_REGSIZE_BYTES : 0;
#else
        assert(!"roundDownSIMDSize unimplemented on target arch");
        unreached();
        return 0;
#endif
    }

    public static int roundUpGprSize(int size)
    {
#if TARGET_64BIT
        if (size > 4)
        {
            return 8;
        }
#endif
        return (size > 2) ? 4 : size;
    }

    public static var_types roundUpGprType(int size)
    {
        return roundUpGprSize(size) switch {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_INT,
#if TARGET_64BIT
            8 => TYP_LONG,
#endif
            _ => TYP_UNDEF,
        };
    }

    /// <summary>rounds the given size up to the nearest SIMD size available on the target.</summary>
    /// <param name="size">size of the data to process with SIMD</param>
    /// <returns></returns>
    /// <remarks>It's only supposed to be used for scenarios where we can perform an overlapped load/store.</remarks>
    public int roundUpSimdSize(int size)
    {
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
        var maxSize = GetPreferredVectorByteLength();
        assert(maxSize <= ZMM_REGSIZE_BYTES);

        if (size <= XMM_REGSIZE_BYTES)
        {
            if (maxSize > XMM_REGSIZE_BYTES)
            {
                maxSize = XMM_REGSIZE_BYTES;
            }
        }
        else if (size <= YMM_REGSIZE_BYTES)
        {
            if (maxSize > YMM_REGSIZE_BYTES)
            {
                maxSize = YMM_REGSIZE_BYTES;
            }
        }
        return maxSize;
#elif TARGET_ARM64
        assert(GetMaxVectorByteLength() is FP_REGSIZE_BYTES);
        return FP_REGSIZE_BYTES;
#else
        assert(!"roundUpSimdSize unimplemented on target arch");
        unreached();
        return 0;
#endif
    }

    public unsafe var_types TypeHandleToVarType(CORINFO_CLASS_HANDLE handle) => TypeHandleToVarType(handle, out _);

    public unsafe var_types TypeHandleToVarType(CORINFO_CLASS_HANDLE handle, out ClassLayout? layout)
    {
        var jitType = info.compCompHnd->asCorInfoType(handle);
        return TypeHandleToVarType(jitType, handle, out layout);
    }

    public unsafe var_types TypeHandleToVarType(CorInfoType jitType, CORINFO_CLASS_HANDLE handle)
        => TypeHandleToVarType(jitType, handle, out _);

    public unsafe var_types TypeHandleToVarType(CorInfoType jitType, CORINFO_CLASS_HANDLE handle, out ClassLayout? layout)
    {
        var type = jitType.VarType;

        if (type == TYP_STRUCT)
        {
            layout = typGetObjLayout(handle);
            type = layout.Type;
        }
        else
        {
            layout = null;
        }
        return type;
    }

    public ClassLayout typGetBlkLayout(int blockSize)
        => typGetCustomLayout(new ClassLayoutBuilder(this, blockSize));

    public int typGetBlkLayoutNum(int blockSize)
        => typGetCustomLayoutNum(new ClassLayoutBuilder(this, blockSize));

    public ClassLayout typGetCustomLayout(ClassLayoutBuilder builder)
        => typClassLayoutTable.GetCustomLayout(this, builder);

    public int typGetCustomLayoutNum(ClassLayoutBuilder builder)
        => typClassLayoutTable.GetCustomLayoutNum(this, builder);

    /// <summary>Get the layout for the specified class handle.</summary>
    /// <param name="classHandle"></param>
    /// <returns></returns>
    public unsafe ClassLayout typGetObjLayout(CORINFO_CLASS_HANDLE classHandle) => typClassLayoutTable.GetObjLayout(this, classHandle);

    public unsafe int typGetObjLayoutNum(CORINFO_CLASS_HANDLE classHandle) => typClassLayoutTable.GetObjLayoutNum(this, classHandle);

    // TODO: Port gsPhase
    public PhaseStatus gsPhase() => PhaseStatus.MODIFIED_NOTHING;

#if FEATURE_LOOP_ALIGN
    // TODO: Port: placeLoopAlignInstructions
    public PhaseStatus placeLoopAlignInstructions() => PhaseStatus.MODIFIED_NOTHING;
#endif

    // TODO: Port rangeCheckPhase
    public PhaseStatus rangeCheckPhase() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port SaveAsyncContexts
    public PhaseStatus SaveAsyncContexts() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port StressSplitTree
    public PhaseStatus StressSplitTree() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port TransformAsync
    public PhaseStatus TransformAsync() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port PhysicalPromotion
    private PhaseStatus PhysicalPromotion() => PhaseStatus.MODIFIED_NOTHING;

    /// <summary>Regenerate flow graph annotations; to be used between iterations when repeating opts.</summary>
    protected void RecomputeFlowGraphAnnotations()
    {
        // TODO: Port Compiler.RecomputeFlowGraphAnnotations
    }

    /// <summary>Clear annotations produced during optimizations; to be used between iterations when repeating opts.</summary>
    protected void ResetOptAnnotations()
    {
        // TODO: Port Compiler.ResetOptAnnotations
    }

#if FEATURE_SIMD
    /// <summary>Return the base type and size of simd vector type given its type handle.</summary>
    /// <param name="typeHnd">The handle of the type we're interested in.</param>
    /// <param name="sizeBytes">set to size in bytes.</param>
    /// <returns>base type of simd vector.</returns>
    /// <remarks>
    ///   <para>If the size of the struct is already known call <see cref="structSizeMightRepresentSimdType" /> to determine if this api needs to be called.</para>
    ///   <para>The type handle passed here can only be used in a subset of JIT-EE calls since it may be called by promotion during AOT of a method that does not version with SPC. See CORINFO_TYPE_LAYOUT_NODE for the contract on the supported JIT-EE calls.</para>
    /// </remarks>
    private unsafe var_types getBaseTypeAndSizeOfSimdType(CORINFO_CLASS_HANDLE typeHnd, out int sizeBytes)
    {
        var simdHandleCache = _simdHandleCache;

        if (simdHandleCache is null)
        {
            if (impInlineInfo is null)
            {
                simdHandleCache = new simdHandlesCache();
            }
            else
            {
                // Steal the inliner compiler's cache (create it if not available).

                var inlineRoot = impInlineInfo.InlineRoot;
                simdHandleCache = inlineRoot._simdHandleCache;

                if (simdHandleCache is null)
                {
                    simdHandleCache = new simdHandlesCache();
                    inlineRoot._simdHandleCache = simdHandleCache;
                }
            }
            _simdHandleCache = simdHandleCache;
        }

        sizeBytes = 0;

        if ((typeHnd is null) || !isIntrinsicType(typeHnd))
        {
            return TYP_UNDEF;
        }

        var className = getClassNameFromMetadata(typeHnd, out var namespaceName);

        // fast path search using cached type handles of important types
        var simdBaseType = TYP_UNDEF;
        var size = 0;

        if (namespaceName.Equals("System.Numerics", StringComparison.Ordinal))
        {
            switch (className[0])
            {
                case 'P':
                {
                    if (!className.Equals("Plane", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    JITDUMP("  Known type Plane\n");
                    simdHandleCache.PlaneHandle = typeHnd;

                    simdBaseType = TYP_FLOAT;
                    size = 4 * TYP_FLOAT.Size;
                    break;
                }

                case 'Q':
                {
                    if (!className.Equals("Quaternion", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    JITDUMP("  Known type Quaternion\n");
                    simdHandleCache.QuaternionHandle = typeHnd;

                    simdBaseType = TYP_FLOAT;
                    size = 4 * TYP_FLOAT.Size;
                    break;
                }

                case 'V':
                {
                    if (!className.StartsWith("Vector", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    switch (className[6])
                    {
                        case '\0':
                        {
                            JITDUMP(" Found type Vector\n");
                            simdHandleCache.VectorHandle = typeHnd;
                            break;
                        }

                        case '2':
                        {
                            if (className[7] != '\0')
                            {
                                return TYP_UNDEF;
                            }

                            JITDUMP(" Found Vector2\n");
                            simdHandleCache.Vector2Handle = typeHnd;

                            simdBaseType = TYP_FLOAT;
                            size = 2 * TYP_FLOAT.Size;
                            break;
                        }

                        case '3':
                        {
                            if (className[7] != '\0')
                            {
                                return TYP_UNDEF;
                            }

                            JITDUMP(" Found Vector3\n");
                            simdHandleCache.Vector3Handle = typeHnd;

                            simdBaseType = TYP_FLOAT;
                            size = 3 * TYP_FLOAT.Size;
                            break;
                        }

                        case '4':
                        {
                            if (className[7] != '\0')
                            {
                                return TYP_UNDEF;
                            }

                            JITDUMP(" Found Vector4\n");
                            simdHandleCache.Vector4Handle = typeHnd;

                            simdBaseType = TYP_FLOAT;
                            size = 4 * TYP_FLOAT.Size;
                            break;
                        }

                        case '`':
                        {
                            if ((className[7] != '1') || (className[8] != '\0'))
                            {
                                return TYP_UNDEF;
                            }

                            var typeArgHnd = info.compCompHnd->getTypeInstantiationArgument(typeHnd, 0);
                            simdBaseType = getBaseTypeForPrimitiveNumericClass(typeArgHnd);

                            if ((simdBaseType < TYP_BYTE) || (simdBaseType > TYP_DOUBLE))
                            {
                                return TYP_UNDEF;
                            }

                            JITDUMP($" Found Vector<{simdBaseType.Name}>\n");
                            size = GetVectorTByteLength();

                            if (size == 0)
                            {
                                return TYP_UNDEF;
                            }
                            break;
                        }

                        default:
                        {
                            return TYP_UNDEF;
                        }
                    }
                    break;
                }

                default:
                {
                    return TYP_UNDEF;
                }
            }
        }
#if FEATURE_HW_INTRINSICS
        else
        {
            size = info.compCompHnd->getClassSize(typeHnd);

            switch (size)
            {
#if TARGET_ARM64
                case 8:
                {
                    if (!className.Equals("Vector64`1", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    var typeArgHnd = info.compCompHnd->getTypeInstantiationArgument(typeHnd, 0);
                    simdBaseType = getBaseTypeForPrimitiveNumericClass(typeArgHnd);

                    if ((simdBaseType < TYP_BYTE) || (simdBaseType > TYP_DOUBLE))
                    {
                        return TYP_UNDEF;
                    }

                    JITDUMP($" Found Vector64<{simdBaseType.Name}>\n");
                    break;
                }
#endif

                case 16:
                {
                    if (!className.Equals("Vector128`1", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    var typeArgHnd = info.compCompHnd->getTypeInstantiationArgument(typeHnd, 0);
                    simdBaseType = getBaseTypeForPrimitiveNumericClass(typeArgHnd);

                    if ((simdBaseType < TYP_BYTE) || (simdBaseType > TYP_DOUBLE))
                    {
                        return TYP_UNDEF;
                    }

                    JITDUMP($" Found Vector128<{simdBaseType.Name}>\n");
                    break;
                }

#if TARGET_XARCH
                case 32:
                {
                    if (!className.Equals("Vector256`1", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    var typeArgHnd = info.compCompHnd->getTypeInstantiationArgument(typeHnd, 0);
                    simdBaseType = getBaseTypeForPrimitiveNumericClass(typeArgHnd);

                    if ((simdBaseType < TYP_BYTE) || (simdBaseType > TYP_DOUBLE))
                    {
                        return TYP_UNDEF;
                    }

                    if (!compOpportunisticallyDependsOn(InstructionSet_AVX))
                    {
                        // We must treat as a regular struct if AVX isn't supported
                        return TYP_UNDEF;
                    }

                    JITDUMP($" Found Vector256<{simdBaseType.Name}>\n");
                    break;
                }

                case 64:
                {
                    if (!className.Equals("Vector512`1", StringComparison.Ordinal))
                    {
                        return TYP_UNDEF;
                    }

                    var typeArgHnd = info.compCompHnd->getTypeInstantiationArgument(typeHnd, 0);
                    simdBaseType = getBaseTypeForPrimitiveNumericClass(typeArgHnd);

                    if ((simdBaseType < TYP_BYTE) || (simdBaseType > TYP_DOUBLE))
                    {
                        return TYP_UNDEF;
                    }

                    if (!compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        // We must treat as a regular struct if AVX512 isn't supported
                        return TYP_UNDEF;
                    }

                    JITDUMP($" Found Vector512<{simdBaseType.Name}>\n");
                    break;
                }
#endif

                default:
                {
                    return TYP_UNDEF;
                }
            }
        }
#endif

        sizeBytes = size;

        if (simdBaseType != TYP_UNDEF)
        {
            assert(size == info.compCompHnd->getClassSize(typeHnd));
            UsesSimdTypes = true;
        }
        return simdBaseType;
    }

    private unsafe var_types getBaseTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls)
    {
        var jitType = info.compCompHnd->getTypeForPrimitiveNumericClass(cls);

        if (jitType == CORINFO_TYPE_UNDEF)
        {
            return TYP_UNDEF;
        }
        return jitType.PreciseVarType;
    }

    private unsafe var_types getBaseTypeOfSimdType(CORINFO_CLASS_HANDLE typeHnd)
        => getBaseTypeAndSizeOfSimdType(typeHnd, out _);
#endif

    private unsafe string getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, out string namespaceName)
    {
        byte* pNamespaceNameUtf8;
        var pClassNameUtf8 = info.compCompHnd->getClassNameFromMetadata(cls, &pNamespaceNameUtf8);

        var classNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassNameUtf8);
        var namespaceNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pNamespaceNameUtf8);

        namespaceName = Encoding.UTF8.GetString(namespaceNameUtf8);
        return Encoding.UTF8.GetString(classNameUtf8);
    }

    /// <summary>Gets the preferred length, in bytes, to use for vectorization</summary>
    /// <returns></returns>
    public int GetPreferredVectorByteLength()
    {
        var maxVectorByteLength = GetMaxVectorByteLength();

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
        var preferredVectorByteLength = opts.preferredVectorByteLength;

        if (preferredVectorByteLength is not 0)
        {
            return int.Min(maxVectorByteLength, preferredVectorByteLength);
        }
#endif

        return maxVectorByteLength;
    }

    /// <summary>Calculates the unrolling threshold for the given operation</summary>
    /// <param name="type">kind of the operation (memset/memcpy)</param>
    /// <param name="canUseSimd">whether it is allowed to use SIMD or not</param>
    /// <returns>The unrolling threshold for the given operation in bytes</returns>
    public int GetUnrollThreshold(UnrollKind type, bool canUseSimd = true)
    {
        var maxRegSize = REGSIZE_BYTES;
        var threshold = maxRegSize;

#if FEATURE_SIMD
        if (canUseSimd)
        {
            maxRegSize = GetPreferredVectorByteLength();

#if TARGET_XARCH
            assert(maxRegSize <= ZMM_REGSIZE_BYTES);
            threshold = maxRegSize;
#elif TARGET_ARM64
            // ldp/stp instructions can load/store two 16-byte vectors at once, e.g.:
            //
            //   ldp q0, q1, [x1]
            //   stp q0, q1, [x0]
            //
            threshold = maxRegSize * 2;
#endif
        }
#if TARGET_XARCH
        else
        {
            // Compatibility with previous logic: we used to allow memset:128/memcpy:64
            // on AMD64 (and 64/32 on x86) for cases where we don't use SIMD
            // see https://github.com/dotnet/runtime/issues/83297
            threshold *= 2;
        }
#endif
#endif

        if (type is Memset)
        {
            // Typically, memset-like operations require less instructions than memcpy
            threshold *= 2;
        }

        // Use 4 as a multiplier by default, thus, the final threshold will be:
        //
        // | arch        | memset | memcpy |
        // |-------------|--------|--------|
        // | x86 avx512  |   512  |   256  |
        // | x86 avx     |   256  |   128  |
        // | x86 sse     |   128  |    64  |
        // | arm64       |   256  |   128  | ldp/stp (2x128bit)
        // | arm         |    32  |    16  | no SIMD support
        // | loongarch64 |    64  |    32  | no SIMD support
        // | riscv64     |    64  |    32  | no SIMD support
        //
        // We might want to use a different multiplier for truly hot/cold blocks based on PGO data
        //
        threshold *= 4;

        if (type is Memmove)
        {
            // NOTE: Memmove's unrolling is currently limited with LSRA -
            // up to LinearScan.MaxInternalCount number of temp regs, e.g. 5*16=80 bytes on arm64
            threshold = maxRegSize * 4;
        }

        if (type is MemcmpU16)
        {
            threshold = maxRegSize * 2;
#if TARGET_ARM64
            threshold = maxRegSize * 6;
#endif
        }

        // For profiled memcmp/memmove we don't want to unroll too much as it's just a guess, and it works better for small sizes.

        if (type is ProfiledMemcmp or ProfiledMemmove)
        {
            threshold = maxRegSize * 2;
        }
        return threshold;
    }

#if FEATURE_SIMD
    private unsafe bool isIntrinsicType(CORINFO_CLASS_HANDLE clsHnd)
        => info.compCompHnd->isIntrinsicType(clsHnd);
#endif

    private unsafe bool notifyInstructionSetUsage(CORINFO_InstructionSet isa, bool supported)
    {
        JITDUMP($"Notify VM instruction set ({InstructionSetToString(isa)}) {(supported ? "must" : "must not")} be supported.\n");
        return info.compCompHnd->notifyInstructionSetUsage(isa, supported);
    }

    /// <summary>Update the SQM state.</summary>
    /// <remarks>Assumes being called at the end of compilation.</remarks>
    private void RecordStateAtEndOfCompilation()
    {
        // TODO: Port RecordStateAtEndOfCompilation
    }

    /// <summary>Records the SQM-relevant (cycles and tick count)</summary>
    /// <remarks>
    ///   <para>Should be called after inlining is complete.</para>
    ///   <para>We do this after inlining because this marks the last point at which the JIT is likely to cause type-loading and class initialization</para>
    /// </remarks>
    private void RecordStateAtEndOfInlining()
    {
        // TODO: Port RecordStateAtEndOfInlining
    }

    public void setMethodHasNoReturnCalls()
    {
        optNoReturnCallCount++;
    }

    public void setCallDoesNotReturn(GenTreeCall call)
    {
        assert(call is not null);
        assert(!call.IsNoReturn);

        call.IsNoReturn = true;
        setMethodHasNoReturnCalls();
    }

    public static bool StructHasOverlappingFields(CorInfoFlag attribs)
        => (attribs & CORINFO_FLG_OVERLAPPING_FIELDS) != 0;

    public static bool StructHasIndexableFields(CorInfoFlag attribs)
        => (attribs & CORINFO_FLG_INDEXABLE_FIELDS) != 0;

#if TARGET_64BIT
    /// <summary>Returns true if 'type' is a struct that can be enregistered for call args or can be returned by value in multiple registers.</summary>
    /// <param name="type">the basic jit var_type for the item being queried</param>
    /// <param name="typeClass">the handle for the struct when 'type' is TYP_STRUCT</param>
    /// <param name="typeSize">updated with the size of 'type'.</param>
    /// <param name="isVarArg">whether or not this is a vararg fixed arg or variable argument, if so on arm64 windows getArgTypeForStruct will ignore HFA types</param>
    /// <param name="callConv">the calling convention of the call</param>
    /// <returns></returns>
    /// <remarks>if 'type' is not a struct the return value will be false.</remarks>
    public unsafe bool VarTypeIsMultiByteAndCanEnreg(var_types type, CORINFO_CLASS_HANDLE typeClass, out int typeSize, bool isVarArg, CorInfoCallConvExtension callConv)
    {
        var result = false;
        var size = 0;

        if (varTypeIsStruct(type))
        {
            assert(typeClass is not null);
            size = info.compCompHnd->getClassSize(typeClass);

            type = GetReturnTypeForStruct(typeClass, callConv, out var howToReturnStruct, size);

            if (type is not TYP_UNKNOWN)
            {
                result = true;
            }
        }
        else
        {
            size = type.Size;
        }

        typeSize = size;
        return result;
    }
#endif

#if DEBUG
    /// <summary>helper to determine if the local should not be promoted under a stress mode.</summary>
    /// <param name="lclNum">local number to test</param>
    /// <returns>true if this local should not be promoted.</returns>
    /// <remarks>Reject ~50% of the potential promotions if STRESS_PROMOTE_FEWER_STRUCTS is active.</remarks>
    public bool compPromoteFewerStructs(int lclNum)
    {
        var rejectThisPromo = false;
        var promoteLess = compStressCompile(STRESS_PROMOTE_FEWER_STRUCTS, 50);

        if (promoteLess)
        {
            rejectThisPromo = ((info.compMethodHash() ^ lclNum) & 1) == 0;
        }
        return rejectThisPromo;
    }
#endif
}
