// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForSwap(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local-register swap generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_SWAP);
        assert(tree.Op2 is not null);
        assert(genIsRegCandidateLocal(tree.Op1) && genIsRegCandidateLocal(tree.Op2));
        var local1 = tree.Op1.AsLclVarCommon();
        ref var descriptor1 = ref _compiler.lvaGetDesc(local1.LclNum);
        var type1 = descriptor1.Type;
        var local2 = tree.Op2.AsLclVarCommon();
        ref var descriptor2 = ref _compiler.lvaGetDesc(local2.LclNum);
        var type2 = descriptor2.Type;
        assert(varTypeUsesSameRegType(type1, type2));
        assert(varTypeUsesIntReg(type1));

        var oldReg1 = local1.RegNum;
        var oldMask1 = local1.RegMask;
        var oldReg2 = local2.RegNum;
        var oldMask2 = local2.RegMask;

        // Both locals remain live. Update their homes without consuming/producing the operand nodes.
        descriptor1.RegNum = oldReg2;
        descriptor2.RegNum = oldReg1;
        var size = varTypeIsGC(type1) != varTypeIsGC(type2) ? EA_GCREF : EA_PTRSIZE;
        inst_RV_RV(INS_xchg, oldReg1, oldReg2, TYP_I_IMPL, size);

        GCInfo.gcRegByrefSetCur &= ~(oldMask1 | oldMask2);
        GCInfo.gcRegGCrefSetCur &= ~(oldMask1 | oldMask2);
        GCInfo.gcMarkRegPtrVal(oldReg2, type1);
        GCInfo.gcMarkRegPtrVal(oldReg1, type2);
#endif
    }
}
#endif
