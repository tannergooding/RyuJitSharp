// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private regNumber getTempRegForResolution(BasicBlock fromBlock, BasicBlock? toBlock,
        var_types type, VARSET_TP sharedCriticalLiveSet, regMaskTP terminatorConsumedRegs)
    {
        var fromMap = getOutVarToRegMap(checked((uint)fromBlock.bbNum))
            ?? throw new FatalJitException("Edge resolution requires the predecessor's register map.");
        var toMap = toBlock is null ? null : getInVarToRegMap(checked((uint)toBlock.bbNum))
            ?? throw new FatalJitException("Edge resolution requires the successor's register map.");
        var freeRegs = getAvailableGPRsForType(allRegs(type), type is TYP_INT ? TYP_REF : type);
#if DEBUG
        // The small-set stress mode deliberately forces integer cycles through XCHG.
        if ((_lsraStressMask & 0x3) == 0x3)
        {
            return REG_NA;
        }
        freeRegs = stressLimitRegs(null, type, freeRegs);
#endif
        freeRegs &= ~terminatorConsumedRegs.GetRegSetForType(type);

        var liveVariables = toBlock is null ? fromBlock.bbLiveOut : toBlock.bbLiveIn;
        _ = VarSetOps.VisitBits(_compiler, liveVariables, index =>
        {
            var fromReg = getVarReg(fromMap, checked((uint)index));
            assert(fromReg != REG_NA);
            if (fromReg != REG_STK)
            {
                freeRegs &= ~genSingleTypeRegMask(fromReg);
            }
            if (toMap is not null)
            {
                var toReg = getVarReg(toMap, checked((uint)index));
                assert(toReg != REG_NA);
                if (toReg != REG_STK)
                {
                    freeRegs &= ~genSingleTypeRegMask(toReg);
                }
            }
            return freeRegs != SRBM_NONE;
        });
        if (toBlock is null)
        {
            var sharedMap = _sharedCriticalVarToRegMap
                ?? throw new FatalJitException("Shared critical resolution requires a register map.");
            _ = VarSetOps.VisitBits(_compiler, sharedCriticalLiveSet, index =>
            {
                var register = getVarReg(sharedMap, checked((uint)index));
                assert(register != REG_NA);
                if (register != REG_STK)
                {
                    freeRegs &= ~genSingleTypeRegMask(register);
                }
                return freeRegs != SRBM_NONE;
            });
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
}
