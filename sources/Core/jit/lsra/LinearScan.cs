// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private uint[]?[]? _blockStats;
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

    private int _lsraStressMask;

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

        _evexIsSupported = Configuration.TARGET_XARCH && compiler.canUseEvexEncoding();

        if (Configuration.TARGET_AMD64)
        {
            _rbmAllFloat = compiler.SRBM_ALLFLOAT;
            _rbmFltCalleeTrash = compiler.SRBM_FLT_CALLEE_TRASH;
            _rbmAllInt = compiler.SRBM_ALLINT;
            _rbmIntCalleeTrash = compiler.SRBM_INT_CALLEE_TRASH;
            _regIntLast = compiler.REG_INT_LAST;
            _apxIsSupported = compiler.canUseApxEncoding();
        }
        else if (Configuration.TARGET_WASM)
        {
            _rbmAllFloat = RBM_ALLFLOAT.FltRegSet;
            _rbmFltCalleeTrash = RBM_FLT_CALLEE_TRASH.FltRegSet;
            _rbmAllInt = RBM_ALLINT.IntRegSet;
            _rbmIntCalleeTrash = RBM_INT_CALLEE_TRASH.IntRegSet;
            _regIntLast = TargetRuntimeMetadata.IntegerRegisterLast;
            _apxIsSupported = false;
        }
        else
        {
            _rbmAllFloat = SRBM_ALLFLOAT;
            _rbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH;
            _rbmAllInt = SRBM_ALLINT;
            _rbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH;
            _regIntLast = TargetRuntimeMetadata.IntegerRegisterLast;
            _apxIsSupported = false;
        }

        initializeRegisterIndices();

        if (Configuration.TARGET_XARCH)
        {
            _rbmAllMask = compiler.SRBM_ALLMASK;
            _rbmMskCalleeTrash = compiler.SRBM_MSK_CALLEE_TRASH;
        }
        else if (Configuration.TARGET_ARM64)
        {
            _rbmAllMask = SRBM_ALLMASK;
            _rbmMskCalleeTrash = compiler.SRBM_MSK_CALLEE_TRASH;
        }

        initializeVarTypeCalleeTrashRegs();

        if (Configuration.TARGET_XARCH && !_evexIsSupported)
        {
            _availableRegCount -= CNT_HIGHFLOAT + CNT_MASK_REGS;
        }

        _firstColdLocation = MaxLocation;

        initializeDebugState();

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

        if (Configuration.TARGET_AMD64)
        {
            _lowGprRegs = _availableIntRegs & SRBM_LOWINT;
        }
        else if (Configuration.TARGET_X86)
        {
            _lowGprRegs = _availableIntRegs;
        }

        compiler.rpFrameType = FT_NOT_SET;
        compiler.rpMustCreateEBPCalled = false;

        _blockSequencingDone = false;
        _blockSequence = null;
        _currentBlockSequenceNumber = 0;
        _blockSequenceCount = 0;
        _blockInfo = null;
        _blockStats = null;
        _pendingDelayFree = false;
        _targetPreferredUse = null;
    }

    private void initializeRegisterIndices()
    {
        if (Configuration.TARGET_AMD64)
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
                    REG_X64_RAX, REG_X64_RCX, REG_X64_RDX, REG_X64_RBX, REG_X64_RSP, REG_X64_RBP, REG_X64_RSI, REG_X64_RDI,
                    REG_X64_R8, REG_X64_R9, REG_X64_R10, REG_X64_R11, REG_X64_R12, REG_X64_R13, REG_X64_R14, REG_X64_R15,
                    REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5, REG_X64_XMM6, REG_X64_XMM7,
                    REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10, REG_X64_XMM11, REG_X64_XMM12, REG_X64_XMM13, REG_X64_XMM14, REG_X64_XMM15,
                    REG_X64_XMM16, REG_X64_XMM17, REG_X64_XMM18, REG_X64_XMM19, REG_X64_XMM20, REG_X64_XMM21, REG_X64_XMM22, REG_X64_XMM23,
                    REG_X64_XMM24, REG_X64_XMM25, REG_X64_XMM26, REG_X64_XMM27, REG_X64_XMM28, REG_X64_XMM29, REG_X64_XMM30, REG_X64_XMM31,
                    REG_X64_K0, REG_X64_K1, REG_X64_K2, REG_X64_K3, REG_X64_K4, REG_X64_K5, REG_X64_K6, REG_X64_K7,
                    REG_X64_COUNT,
                ];
            }
        }
        else
        {
            _regIndices = new regNumber[(int)ACTUAL_REG_COUNT + 1];
            for (var index = 0; index < _regIndices.Length; index++)
            {
                _regIndices[index] = (regNumber)index;
            }
        }
    }

    private void initializeVarTypeCalleeTrashRegs()
    {
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            var type = (var_types)index;
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS && varTypeUsesMaskReg(type))
            {
                _varTypeCalleeTrashRegs[index] = _rbmMskCalleeTrash;
                continue;
            }
            _varTypeCalleeTrashRegs[index] = varTypeUsesFloatReg(type) ? _rbmFltCalleeTrash : _rbmIntCalleeTrash;
        }
    }

    private void initializeAvailableRegs()
    {
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            var type = (var_types)index;
            if (type is TYP_DOUBLE || varTypeIsSimd(type))
            {
                _availableRegs[index] = _availableDoubleRegs;
            }
            else if (Configuration.FEATURE_MASKED_HW_INTRINSICS && varTypeUsesMaskReg(type))
            {
                _availableRegs[index] = _availableMaskRegs;
            }
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

    [Conditional("DEBUG")]
    private unsafe void initializeDebugState()
    {
        _maxNodeLocation = 0;
        _consecutiveRegistersLocation = 0;
        _activeRefPosition = null;
        _currentBuildNode = null;
        _lsraStressMask = JitConfig.JitStressRegs;

        if (_lsraStressMask != 0)
        {
            AllocationDebugDataStorage.StressRegsRange.EnsureInit(JitConfig.JitStressRegsRange);
            if (!AllocationDebugDataStorage.StressRegsRange.Contains(_compiler.info.compMethodHash()))
            {
                JITDUMP($"*** JitStressRegs = 0x{_lsraStressMask:x} -- disabled by JitStressRegsRange\n");
                _lsraStressMask = 0;
            }
            else
            {
                JITDUMP($"*** JitStressRegs = 0x{_lsraStressMask:x}\n");
            }
        }
    }

    private void initializeRegisterSets(ICodeGen codeGen)
    {
        if (Configuration.TARGET_ARM64)
        {
            // NativeAOT depends on LR not being used as a GPR (dotnet/runtime#101932).
            _availableIntRegs = _rbmAllInt & ~(SRBM_ARM64_PR | SRBM_ARM64_FP | SRBM_ARM64_LR) &
                ~codeGen.RegSet.rsMaskResvd.IntRegSet;
        }
        else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            var frameRegisters = Configuration.TARGET_LOONGARCH64
                ? SRBM_LOONGARCH64_FP | SRBM_LOONGARCH64_RA
                : SRBM_RISCV64_FP | SRBM_RISCV64_RA;
            _availableIntRegs = _rbmAllInt & ~frameRegisters &
                ~codeGen.RegSet.rsMaskResvd.IntRegSet;
        }
        else
        {
            _availableIntRegs = _rbmAllInt & ~codeGen.RegSet.rsMaskResvd.IntRegSet;
        }

        if (Configuration.ETW_EBP_FRAMED)
        {
            _availableIntRegs &= ~SRBM_FPBASE;
        }
        _availableFloatRegs = _rbmAllFloat;
        if (Configuration.TARGET_AMD64)
        {
            _availableDoubleRegs = _rbmAllFloat;
        }
        else if (Configuration.TARGET_WASM)
        {
            _availableDoubleRegs = RBM_ALLDOUBLE.FltRegSet;
        }
        else
        {
            _availableDoubleRegs = SRBM_ALLDOUBLE;
        }
        if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64)
        {
            _availableMaskRegs = _rbmAllMask;
        }

        if ((Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64) &&
            _compiler.opts.compDbgEnC)
        {
            _availableIntRegs &= (~TargetRuntimeMetadata.IntegerCalleeSavedMask | TargetRuntimeMetadata.EncodedCalleeSavedMask);
            _availableFloatRegs &= ~TargetRuntimeMetadata.FloatingCalleeSavedMask;
            _availableDoubleRegs &= ~TargetRuntimeMetadata.FloatingCalleeSavedMask;
            if (Configuration.TARGET_XARCH)
            {
                _availableMaskRegs &= ~TargetRuntimeMetadata.MaskCalleeSavedMask;
            }
        }

        if (Configuration.TARGET_AMD64 && _compiler.MethodHasPatchpoint)
        {
            _availableFloatRegs &= ~TargetRuntimeMetadata.FloatingCalleeSavedMask;
            _availableDoubleRegs &= ~TargetRuntimeMetadata.FloatingCalleeSavedMask;
            _availableMaskRegs &= ~TargetRuntimeMetadata.MaskCalleeSavedMask;
        }

        if (Configuration.TARGET_AMD64 && _evexIsSupported)
        {
            _availableFloatRegs |= SRBM_HIGHFLOAT;
            _availableDoubleRegs |= SRBM_HIGHFLOAT;
        }
    }

    public PhaseStatus DoRegisterAllocation()
    {
        if (_enregisterLocalVars && (_compiler.lvaTrackedCount == 0))
        {
            _enregisterLocalVars = false;
        }
        _splitBBNumToTargetBBNumMap = null;

        assert(_compiler.codeGen is not null);
        _compiler.codeGen.RegSet.rsClearRegsModified();
        initMaxSpill();
        if (Configuration.TARGET_ARM64)
        {
            _nextConsecutiveRefPositions.Clear();
        }
        if (_enregisterLocalVars)
        {
            buildIntervalsWithLocals();
        }
        else
        {
            buildIntervalsMinimal();
        }
        if (VERBOSE)
        {
            tupleStyleDump(LsraTupleDumpMode.LSRA_DUMP_REFPOS);
        }
        _compiler.EndPhase(PHASE_LINEAR_SCAN_BUILD);
        if (VERBOSE)
        {
            dumpLsraIntervals("after buildIntervals");
        }

        initVarRegMaps();
        if (Configuration.TARGET_ARM64 && _compiler.info.compNeedsConsecutiveRegisters)
        {
            allocateRegistersWithConsecutiveRegisters();
        }
        else
        {
            if (_enregisterLocalVars || _compiler.opts.OptimizationEnabled)
            {
                allocateRegisters();
            }
            else
            {
                allocateRegistersMinimal();
            }
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

        if (Configuration.TRACK_LSRA_STATS &&
            ((JitConfig.DisplayLsraStats == 1) || (Configuration.DEBUG && VERBOSE)))
        {
            dumpLsraStats(jitstdout());
        }
        if (VERBOSE)
        {
            tupleStyleDump(LsraTupleDumpMode.LSRA_DUMP_POST);
        }
        _compiler.compRegAllocDone = true;

        if (_compiler.fgBBcount != _blockSequenceCount)
        {
            assert(_compiler.fgBBcount > _blockSequenceCount);
            _compiler.fgInvalidateDfsTree();
        }
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private void allocateRegistersWithConsecutiveRegisters()
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new FatalJitException(CORJIT_SKIPPED);
        }

        NYI("LinearScan.allocateRegisters<true> consecutive-register allocation");
        throw new FatalJitException("Consecutive-register allocation is not implemented.");
    }

    public void dumpLsraStatsCsv(StreamWriter streamWriter)
    {
        lock (JitTextWriter.GetSyncRoot(streamWriter))
        {
            dumpLsraStatsCsvCore(streamWriter);
        }
    }
}
