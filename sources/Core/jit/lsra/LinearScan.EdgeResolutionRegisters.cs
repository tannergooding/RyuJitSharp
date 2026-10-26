// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private struct AvailableRegistersVisitor : IBitSetVisitor<AvailableRegistersVisitor>
    {
        private readonly LinearScan _linearScan;
        private readonly regNumberSmall[] _fromMap;
        private readonly regNumberSmall[]? _toMap;
        private regMask _freeRegisters;

        public AvailableRegistersVisitor(
            LinearScan linearScan,
            regNumberSmall[] fromMap,
            regNumberSmall[]? toMap,
            regMask freeRegisters)
        {
            _linearScan = linearScan;
            _fromMap = fromMap;
            _toMap = toMap;
            _freeRegisters = freeRegisters;
        }

        public readonly regMask FreeRegisters => _freeRegisters;

        public bool Visit(int bitIndex)
        {
            var trackedIndex = checked((uint)bitIndex);
            var fromRegister = _linearScan.getVarReg(_fromMap, trackedIndex);
            assert(fromRegister != REG_NA);

            if (fromRegister != REG_STK)
            {
                _freeRegisters &= ~_linearScan.resolutionRegisterMask(fromRegister, trackedIndex);
            }

            if (_toMap is not null)
            {
                var toRegister = _linearScan.getVarReg(_toMap, trackedIndex);
                assert(toRegister != REG_NA);

                if (toRegister != REG_STK)
                {
                    _freeRegisters &= ~_linearScan.resolutionRegisterMask(toRegister, trackedIndex);
                }
            }

            return _freeRegisters != SRBM_NONE;
        }
    }

    private regNumber getTempRegForResolution(BasicBlock fromBlock, BasicBlock? toBlock,
        var_types type, VARSET_TP sharedCriticalLiveSet, regMaskTP terminatorConsumedRegs)
    {
        var fromMap = getOutVarToRegMap(checked((uint)fromBlock.bbNum));
        if (fromMap is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.EdgeResolutionRequiresThePredecessorSRegisterMap
    );
            return default!;
        }
        regNumberSmall[]? toMap = null;
        if (toBlock is not null)
        {
            toMap = getInVarToRegMap(checked((uint)toBlock.bbNum));
            if (toMap is null)
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.EdgeResolutionRequiresTheSuccessorSRegisterMap
    );
                return default;
            }
        }
        var freeRegs = Configuration.TARGET_ARM
            ? allRegs(type is TYP_DOUBLE ? TYP_FLOAT : type)
            : getAvailableGPRsForType(allRegs(type), type is TYP_INT ? TYP_REF : type);
        if (Configuration.DEBUG)
        {
            // The small-set stress mode deliberately forces integer cycles through XCHG.
            if ((_lsraStressMask & 0x3) == 0x3)
            {
                return REG_NA;
            }

            freeRegs = stressLimitRegs(null, type, freeRegs);
        }
        freeRegs &= ~terminatorConsumedRegs.GetRegSetForType(type);

        var liveVariables = toBlock is null ? fromBlock.bbLiveOut : toBlock.bbLiveIn;
        var liveVariableVisitor = new AvailableRegistersVisitor(this, fromMap, toMap, freeRegs);
        _ = VarSetOps.VisitBits(_compiler, liveVariables, ref liveVariableVisitor);
        freeRegs = liveVariableVisitor.FreeRegisters;

        if (toBlock is null)
        {
            var sharedMap = _sharedCriticalVarToRegMap;
            if (sharedMap is null)
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.SharedCriticalResolutionRequiresARegisterMap
    );
                return default!;
            }
            var sharedRegisterVisitor = new AvailableRegistersVisitor(this, sharedMap, null, freeRegs);
            _ = VarSetOps.VisitBits(_compiler, sharedCriticalLiveSet, ref sharedRegisterVisitor);
            freeRegs = sharedRegisterVisitor.FreeRegisters;
        }

        if (Configuration.TARGET_ARM && type is TYP_DOUBLE)
        {
            var freePairs = SRBM_NONE;
            for (var low = REG_ARM32_F0; low < REG_ARM32_F31; low += 2)
            {
                var pair = low.GetSingleTypeFloatMask(TYP_DOUBLE);
                if ((freeRegs & pair) == pair)
                {
                    freePairs |= genSingleTypeRegMask(low);
                }
            }
            freeRegs = freePairs;
        }
        if (freeRegs == SRBM_NONE)
        {
            return REG_NA;
        }
        var calleeTrash = regType(type) is TYP_INT
            ? _compiler.SRBM_INT_CALLEE_TRASH : _compiler.SRBM_FLT_CALLEE_TRASH;
        if ((freeRegs & calleeTrash) != SRBM_NONE)
        {
            freeRegs &= calleeTrash;
        }
        var lowestBit = (SingleTypeRegSet)(1UL << BitOperations.TrailingZeroCount(unchecked((ulong)freeRegs)));
        return genRegNumFromMask(lowestBit, type);
    }

    private SingleTypeRegSet resolutionRegisterMask(regNumber register, uint trackedIndex)
    {
        if (Configuration.TARGET_ARM && genIsValidFloatReg(register))
        {
            return register.GetSingleTypeFloatMask(getIntervalForLocalVar(trackedIndex).registerType);
        }
        return genSingleTypeRegMask(register);
    }
}
