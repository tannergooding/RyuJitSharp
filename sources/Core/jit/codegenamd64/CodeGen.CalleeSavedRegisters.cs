// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64
using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public uint genPopCalleeSavedRegistersFromMaskAPX(regMaskTP popRegs)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert((_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT) && !_compiler.opts.IsOSR);
        assert((popRegs & RBM_RSP).IsEmpty);

        var alignReg = REG_NA;
        if (!IsFramePointerUsed && popRegs.IsNonEmpty)
        {
            alignReg = (popRegs & RBM_RBP).IsNonEmpty
                ? REG_RBP : (regNumber)BitOperations.TrailingZeroCount((ulong)popRegs.IntRegSet);
            popRegs &= ~regMaskTP.CreateFromRegNum(alignReg, alignReg.SingleTypeMask);
        }

        Span<regNumber> registers = stackalloc regNumber[REG_INT_LAST - REG_INT_FIRST + 1];
        var count = 0;
        while (popRegs.IsNonEmpty)
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)popRegs.IntRegSet);
            registers[count++] = reg;
            popRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        var popped = 0u;
        var index = 0;
        if ((count & 1) != 0)
        {
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, registers[index++], INS_OPTS_APX_ppx);
            popped++;
        }

        while (index < count - 1)
        {
            Emitter.emitIns_R_R(INS_pop2, EA_PTRSIZE, registers[index++], registers[index++],
                INS_OPTS_EVEX_nd | INS_OPTS_APX_ppx);
            popped += 2;
        }
        assert(index == count);

        if (alignReg != REG_NA)
        {
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, alignReg, INS_OPTS_APX_ppx);
            popped++;
        }

        return popped;
    }
}
#endif
