// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.IO;

namespace RyuJitSharp;

public sealed partial class LinearScan : IRegAlloc
{
    private readonly Compiler _compiler;

    // Native upper register-number bound, not the length of the compact register-index map.
    private int _availableRegCount;
    private readonly bool _evexIsSupported;
    private readonly bool _apxIsSupported;
    private bool _enregisterLocalVars;

    private regMask _rbmAllFloat;
    private regMask _rbmFltCalleeTrash;
    private regMask _rbmAllInt;
    private regMask _rbmIntCalleeTrash;
    private regNumber _regIntLast;
    private regMask _rbmAllMask;
    private regMask _rbmMskCalleeTrash;
    private regNumber[] _regIndices = [];
    private InlineArrayTypCount<regMask> _varTypeCalleeTrashRegs;

    private bool _allocationPassComplete;
    private bool _blockSequencingDone;
    private BasicBlock[]? _blockSequence;
    private int _currentBlockSequenceNumber;
    private int _blockSequenceCount;
    private LsraLocation _firstColdLocation;
    private bool _needNonIntegerRegisters;
    private SingleTypeRegSet _lowGprRegs;
    private readonly RegisterSelection _regSelector;
    private readonly RefInfoListNodePool _listNodePool;
    private RefPosition? _killHead;
    private RefPosition? _killTail;
    private LsraBlockInfo[]? _blockInfo;
    private bool _pendingDelayFree;
    private RefPosition? _targetPreferredUse;
    private readonly LsraLocation[] _nextFixedRef;
    private readonly LsraLocation[] _nextIntervalRef;
    private readonly weight_t[] _spillCost;
    private SingleTypeRegSet _fixedRegsLow;
    private SingleTypeRegSet _fixedRegsHigh;
    private regMaskTP _regsBusyUntilKill;
    private regMaskTP _regsInUseThisLocation;

    private SingleTypeRegSet _availableIntRegs;
    private SingleTypeRegSet _availableFloatRegs;
    private SingleTypeRegSet _availableDoubleRegs;
    private SingleTypeRegSet _availableMaskRegs;
    private InlineArrayTypCount<SingleTypeRegSet> _availableRegs;

    internal readonly List<Interval> intervals;
    internal readonly RegRecord[] physRegs;

#if DEBUG
    private uint _maxNodeLocation;
    private uint _consecutiveRegistersLocation;
    private RefPosition? _activeRefPosition;
    private int _lsraStressMask;
    private static ConfigMethodRange s_jitStressRegsRange;
#endif

    public LinearScan(Compiler compiler)
    {
        _compiler = compiler;
        _availableRegCount = (int)ACTUAL_REG_COUNT;
        intervals = [];
        physRegs = new RegRecord[(int)REG_COUNT];
        for (var index = 0; index < physRegs.Length; index++)
        {
            physRegs[index] = new RegRecord();
        }

        _allocationPassComplete = false;
        _killHead = null;
        _killTail = null;
        _listNodePool = new RefInfoListNodePool();
        _nextFixedRef = new LsraLocation[(int)REG_COUNT];
        _nextIntervalRef = new LsraLocation[(int)REG_COUNT];
        _spillCost = new weight_t[(int)REG_COUNT];
        Array.Fill(_nextFixedRef, MaxLocation);
        Array.Fill(_nextIntervalRef, MaxLocation);
        _fixedRegsLow = SRBM_NONE;
        _fixedRegsHigh = SRBM_NONE;
        _regsBusyUntilKill = RBM_NONE;
        _regsInUseThisLocation = RBM_NONE;

        _needNonIntegerRegisters = false;

#if TARGET_AMD64
        _evexIsSupported = compiler.canUseEvexEncoding();
        _rbmAllFloat = compiler.SRBM_ALLFLOAT;
        _rbmFltCalleeTrash = compiler.SRBM_FLT_CALLEE_TRASH;
        _rbmAllInt = compiler.SRBM_ALLINT;
        _rbmIntCalleeTrash = compiler.SRBM_INT_CALLEE_TRASH;
        _regIntLast = compiler.REG_INT_LAST;
        _apxIsSupported = compiler.canUseApxEncoding();

        initializeRegisterIndices();
        _rbmAllMask = compiler.SRBM_ALLMASK;
        _rbmMskCalleeTrash = compiler.SRBM_MSK_CALLEE_TRASH;
        initializeVarTypeCalleeTrashRegs();

        if (!_evexIsSupported)
        {
            _availableRegCount -= CNT_HIGHFLOAT + CNT_MASK_REGS;
        }
#else
        NYI("LinearScan constructor outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LinearScan constructor outside AMD64.");
#endif

        _firstColdLocation = MaxLocation;

#if DEBUG
        initializeDebugState();
#endif

        // Lowering runs after this constructor and may add tracked locals.
        _enregisterLocalVars = compiler.compEnregLocals;

        _regSelector = new RegisterSelection(this);

        var codeGen = compiler.codeGen;
        assert(codeGen is not null);
        if (codeGen is null)
        {
            throw new FatalJitException("LSRA requires initialized codegen state.");
        }

        initializeRegisterSets(codeGen);
        initializeAvailableRegs();

#if TARGET_AMD64
        _lowGprRegs = _availableIntRegs & SRBM_LOWINT;
#endif

        compiler.rpFrameType = FT_NOT_SET;
        compiler.rpMustCreateEBPCalled = false;

        _blockSequencingDone = false;
        _blockSequence = null;
        _currentBlockSequenceNumber = 0;
        _blockSequenceCount = 0;
        _blockInfo = null;
        _pendingDelayFree = false;
        _targetPreferredUse = null;
    }

#if TARGET_AMD64
    private void initializeRegisterIndices()
    {
        if (_apxIsSupported)
        {
            _regIndices = new regNumber[(int)ACTUAL_REG_COUNT + 1];
            for (var index = 0; index < _regIndices.Length; index++)
            {
                _regIndices[index] = (regNumber)index;
            }
        }
        else
        {
            _regIndices =
            [
                REG_RAX, REG_RCX, REG_RDX, REG_RBX, REG_RSP, REG_RBP, REG_RSI, REG_RDI,
                REG_R8, REG_R9, REG_R10, REG_R11, REG_R12, REG_R13, REG_R14, REG_R15,
                REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM7,
                REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15,
                REG_XMM16, REG_XMM17, REG_XMM18, REG_XMM19, REG_XMM20, REG_XMM21, REG_XMM22, REG_XMM23,
                REG_XMM24, REG_XMM25, REG_XMM26, REG_XMM27, REG_XMM28, REG_XMM29, REG_XMM30, REG_XMM31,
                REG_K0, REG_K1, REG_K2, REG_K3, REG_K4, REG_K5, REG_K6, REG_K7,
                REG_COUNT,
            ];
        }
    }

    private void initializeVarTypeCalleeTrashRegs()
    {
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            var type = (var_types)index;
#if FEATURE_MASKED_HW_INTRINSICS
            if (varTypeUsesMaskReg(type))
            {
                _varTypeCalleeTrashRegs[index] = _rbmMskCalleeTrash;
                continue;
            }
#endif
            _varTypeCalleeTrashRegs[index] = varTypeUsesFloatReg(type) ? _rbmFltCalleeTrash : _rbmIntCalleeTrash;
        }
    }
#endif

    private void initializeAvailableRegs()
    {
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            var type = (var_types)index;
            if (type is TYP_DOUBLE)
            {
                _availableRegs[index] = _availableDoubleRegs;
            }
#if FEATURE_MASKED_HW_INTRINSICS
            else if (varTypeUsesMaskReg(type))
            {
                _availableRegs[index] = _availableMaskRegs;
            }
#endif
            else if (varTypeUsesFloatReg(type))
            {
                _availableRegs[index] = _availableFloatRegs;
            }
            else
            {
                _availableRegs[index] = _availableIntRegs;
            }
        }
    }

#if DEBUG
    private unsafe void initializeDebugState()
    {
        _maxNodeLocation = 0;
        _consecutiveRegistersLocation = 0;
        _activeRefPosition = null;
        _currentBuildNode = null;
        _lsraStressMask = JitConfig.JitStressRegs;

        if (_lsraStressMask != 0)
        {
            s_jitStressRegsRange.EnsureInit(JitConfig.JitStressRegsRange);
            if (!s_jitStressRegsRange.Contains(_compiler.info.compMethodHash()))
            {
                _lsraStressMask = 0;
            }
        }
    }
#endif

    private void initializeRegisterSets(ICodeGen codeGen)
    {
#if TARGET_AMD64
        _availableIntRegs = _compiler.SRBM_ALLINT & ~codeGen.RegSet.rsMaskResvd.IntRegSet;
#if ETW_EBP_FRAMED
        _availableIntRegs &= ~SRBM_FPBASE;
#endif
        _availableFloatRegs = _rbmAllFloat;
        _availableDoubleRegs = _rbmAllFloat;
        _availableMaskRegs = _rbmAllMask;

        if (_compiler.opts.compDbgEnC)
        {
            _availableIntRegs &= (~SRBM_INT_CALLEE_SAVED | SRBM_ENC_CALLEE_SAVED);
            _availableFloatRegs &= ~SRBM_FLT_CALLEE_SAVED;
            _availableDoubleRegs &= ~SRBM_FLT_CALLEE_SAVED;
            _availableMaskRegs &= ~SRBM_MSK_CALLEE_SAVED;
        }

        if (_compiler.MethodHasPatchpoint)
        {
            _availableFloatRegs &= ~SRBM_FLT_CALLEE_SAVED;
            _availableDoubleRegs &= ~SRBM_FLT_CALLEE_SAVED;
            _availableMaskRegs &= ~SRBM_MSK_CALLEE_SAVED;
        }

        if (_evexIsSupported)
        {
            _availableFloatRegs |= SRBM_HIGHFLOAT;
            _availableDoubleRegs |= SRBM_HIGHFLOAT;
        }
#else
        NYI("LinearScan register sets outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LinearScan register sets outside AMD64.");
#endif
    }

    public PhaseStatus DoRegisterAllocation()
    {
#if TARGET_AMD64 && !UNIX_AMD64_ABI
        if (_enregisterLocalVars && (_compiler.lvaTrackedCount == 0))
        {
            _enregisterLocalVars = false;
        }
        _splitBBNumToTargetBBNumMap = null;

        assert(_compiler.codeGen is not null);
        _compiler.codeGen.RegSet.rsClearRegsModified();
        initMaxSpill();
        if (_enregisterLocalVars)
        {
            buildIntervalsWithLocals();
        }
        else
        {
            buildIntervalsMinimal();
        }
#if DEBUG
        if (VERBOSE)
        {
            tupleStyleDump(LsraTupleDumpMode.LSRA_DUMP_REFPOS);
        }
#endif
        _compiler.EndPhase(PHASE_LINEAR_SCAN_BUILD);
#if DEBUG
        if (VERBOSE)
        {
            dumpLsraIntervals("after buildIntervals");
        }
#endif

        initVarRegMaps();
        if (_enregisterLocalVars || _compiler.opts.OptimizationEnabled)
        {
            allocateRegisters();
        }
        else
        {
            allocateRegistersMinimal();
        }
        _allocationPassComplete = true;
        _compiler.EndPhase(PHASE_LINEAR_SCAN_ALLOC);

        if (_enregisterLocalVars)
        {
            resolveRegistersWithLocals();
        }
        else
        {
            resolveRegistersMinimal();
        }
        _compiler.EndPhase(PHASE_LINEAR_SCAN_RESOLVE);
        assert(_blockSequencingDone);

#if TRACK_LSRA_STATS
        if ((JitConfig.DisplayLsraStats == 1)
#if DEBUG
            || VERBOSE
#endif
        )
        {
            dumpLsraStats(jitstdout());
        }
#endif
#if DEBUG
        if (VERBOSE)
        {
            tupleStyleDump(LsraTupleDumpMode.LSRA_DUMP_POST);
        }
#endif
        _compiler.compRegAllocDone = true;

        if (_compiler.fgBBcount != _blockSequenceCount)
        {
            assert(_compiler.fgBBcount > _blockSequenceCount);
            _compiler.fgInvalidateDfsTree();
        }
        return PhaseStatus.MODIFIED_EVERYTHING;
#else
        const string message = "LinearScan.DoRegisterAllocation outside Windows AMD64 is not implemented.";
        JITDUMP($"\nCOMPILATION FAILED: {message}\n");
        throw new FatalJitException(CORJIT_SKIPPED, message);
#endif
    }

#if TRACK_LSRA_STATS
    public void dumpLsraStatsCsv(StreamWriter streamWriter)
    {
        dumpLsraStatsCsvCore(streamWriter);
    }
#endif
}
