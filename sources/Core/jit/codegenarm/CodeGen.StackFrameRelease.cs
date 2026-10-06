// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFreeLclFrame(uint frameSize, ref bool unwindStarted)
    {
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        if (frameSize == 0)
        {
            return;
        }

        // Large frames need an immediate materialization before the unwindable stack adjustment.
        if (arm_Valid_Imm_For_Instr(INS_add, unchecked((int)frameSize), INS_FLAGS_DONT_CARE))
        {
            if (!unwindStarted)
            {
                _compiler.unwindBegEpilog();
                unwindStarted = true;
            }

            Emitter.emitIns_R_I(INS_add, EA_PTRSIZE, REG_SPBASE, unchecked((int)frameSize), INS_FLAGS_DONT_CARE);
        }
        else
        {
            // LR is restored after this adjustment; r12 may hold a fast-tailcall target.
            var tmpReg = REG_LR;
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, unchecked((nint)frameSize));
            if (unwindStarted)
            {
                // Account for the materialization before recording the stack adjustment.
                _compiler.unwindPadding();
            }

            if (!unwindStarted)
            {
                _compiler.unwindBegEpilog();
                unwindStarted = true;
            }

            Emitter.emitIns_R_R(INS_add, EA_PTRSIZE, REG_SPBASE, tmpReg, INS_FLAGS_DONT_CARE);
        }

        _compiler.unwindAllocStack(frameSize);
    }
}
#endif
