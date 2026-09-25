// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genLeaInstruction(GenTreeAddrMode lea)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Address generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var size = lea.Type.EmitSize;
        genConsumeOperands(lea);

        if (lea.HasBaseAddress && lea.HasIndex)
        {
            Emitter.emitIns_R_ARX(INS_lea, size, lea.RegNum, lea.BaseAddress.RegNum, lea.Index.RegNum,
                lea.Scale, lea.Offset);
        }
        else if (lea.HasBaseAddress)
        {
            Emitter.emitIns_R_AR(INS_lea, size, lea.RegNum, lea.BaseAddress.RegNum, lea.Offset);
        }
        else if (lea.HasIndex)
        {
            Emitter.emitIns_R_ARX(INS_lea, size, lea.RegNum, REG_NA, lea.Index.RegNum, lea.Scale, lea.Offset);
        }

        genProduceReg(lea);
#endif
    }

    public void genCodeForNullCheck(GenTreeIndir tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Null-check generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_NULLCHECK);
        assert(tree.Op1.IsUsedFromReg);
        var reg = genConsumeReg(tree.Op1);
        Emitter.emitIns_AR_R(INS_cmp, tree.Type.EmitSize, reg, reg, 0);
#endif
    }

    public void genRangeCheck(GenTree oper)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Bounds-check generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        noway_assert(oper.Oper is GT_BOUNDS_CHECK);
        var bounds = oper.AsBoundsChk();
        var index = bounds.Index;
        var length = bounds.ArrayLength;

        genConsumeRegs(index);
        genConsumeRegs(length);

        GenTree src1;
        GenTree src2;
        emitJumpKind jumpKind;
        instruction compare;
        if (index.IsIntegralConst(0) && length.IsUsedFromReg)
        {
            // Array lengths are nonnegative, so index zero only needs a zero test.
            src1 = length;
            src2 = length;
            jumpKind = EJ_je;
            compare = INS_test;
        }
        else if (index.IsContainedIntOrIImmed)
        {
            assert(!length.IsContainedIntOrIImmed);
            src1 = length;
            src2 = index;
            jumpKind = EJ_jbe;
            compare = INS_cmp;
        }
        else
        {
            assert(!index.IsUsedFromMemory || !length.IsUsedFromMemory);
            src1 = index;
            src2 = length;
            jumpKind = EJ_jae;
            compare = INS_cmp;
        }

        var type = src2.Type;
        assert(type is TYP_INT or TYP_LONG);
        assert(type.EmitSize >= src1.Type.EmitSize);
        _ = Emitter.emitInsBinary(compare, type.EmitSize, src1, src2);
        genJumpToThrowHlpBlk(jumpKind, bounds.ThrowKind);
#endif
    }
}
#endif
