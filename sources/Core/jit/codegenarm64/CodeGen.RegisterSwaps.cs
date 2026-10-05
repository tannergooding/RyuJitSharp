// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForSwap(GenTreeOp tree)
    {
        assert(tree.Oper is GT_SWAP);
        assert(genIsRegCandidateLocal(tree.Op1) && genIsRegCandidateLocal(tree.Op2));

        var local1 = tree.Op1.AsLclVarCommon();
        ref var descriptor1 = ref _compiler.lvaGetDesc(local1.LclNum);
        var type1 = descriptor1.Type;

        var local2 = tree.Op2.AsLclVarCommon();
        ref var descriptor2 = ref _compiler.lvaGetDesc(local2.LclNum);
        var type2 = descriptor2.Type;

        assert(!varTypeIsFloating(type1) || varTypeIsFloating(type2));
        assert(!varTypeIsFloating(type1));

        var oldReg1 = local1.RegNum;
        var oldMask1 = genRegMask(oldReg1);
        var oldReg2 = local2.RegNum;
        var oldMask2 = genRegMask(oldReg2);

        descriptor1.RegNum = oldReg2;
        descriptor2.RegNum = oldReg1;

        NYI("register swap");

        GCInfo.gcRegByrefSetCur &= ~(oldMask1 | oldMask2);
        GCInfo.gcRegGCrefSetCur &= ~(oldMask1 | oldMask2);
        GCInfo.gcMarkRegPtrVal(oldReg2, type1);
        GCInfo.gcMarkRegPtrVal(oldReg1, type2);
    }
}
#endif
