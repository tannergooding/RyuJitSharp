// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.insCFlags;

#if TARGET_AMD64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction JumpKindToCcmp(emitJumpKind condition)
    {
        ReadOnlySpan<instruction> table =
        [
            INS_none, INS_none, INS_ccmpo, INS_ccmpno, INS_ccmpb, INS_ccmpae, INS_ccmpe, INS_ccmpne, INS_ccmpbe,
            INS_ccmpa, INS_ccmps, INS_ccmpns, INS_none, INS_none, INS_ccmpl, INS_ccmpge, INS_ccmple, INS_ccmpg,
        ];
        assert((condition >= EJ_NONE) && (condition < EJ_COUNT));

        return table[(int)condition];
    }

    public static insOpts OptsFromCFlags(insCFlags flags)
    {
        var opts = INS_OPTS_NONE;
        if ((flags & INS_FLAGS_CF) != 0)
        {
            opts |= INS_OPTS_EVEX_dfv_cf;
        }
        if ((flags & INS_FLAGS_ZF) != 0)
        {
            opts |= INS_OPTS_EVEX_dfv_zf;
        }
        if ((flags & INS_FLAGS_SF) != 0)
        {
            opts |= INS_OPTS_EVEX_dfv_sf;
        }
        if ((flags & INS_FLAGS_OF) != 0)
        {
            opts |= INS_OPTS_EVEX_dfv_of;
        }

        return opts;
    }

    public void genCodeForCCMP(GenTreeCCMP tree)
    {
#if !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Conditional compare generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.UsePromotedEvexEncodings);
        genConsumeOperands(tree);
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var size = op1.Type.ActualType.EmitActualSize;
        var srcReg1 = op1.RegNum;
        assert(!varTypeIsFloating(op2.Type.ActualType));
        assert(!op1.IsContainedIntOrIImmed);

        var condition = GenConditionDesc.Get(tree.Condition);
        var ins = JumpKindToCcmp(condition.JumpKind1);
        var opts = OptsFromCFlags(tree.FlagsVal);
        if (op2.IsContainedIntOrIImmed)
        {
            var value = op2.AsIntConCommon().IconValue;
            if (value == 0)
            {
                // CTEST reg,reg is one byte shorter than CCMP reg,0.
                assert((FIRST_CTEST_INSTRUCTION - FIRST_CCMP_INSTRUCTION) == 32);
                var ctest = ins + (FIRST_CTEST_INSTRUCTION - FIRST_CCMP_INSTRUCTION);
                Emitter.emitIns_R_R(ctest, size, srcReg1, srcReg1, opts);
            }
            else
            {
                Emitter.emitIns_R_I(ins, size, srcReg1, unchecked((int)value), opts);
            }
        }
        else
        {
            Emitter.emitIns_R_R(ins, size, srcReg1, op2.RegNum, opts);
        }
#endif
    }
}
#endif
