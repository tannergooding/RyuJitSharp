// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclVar(GenTreeLclVar tree)
    {
        var varNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        var targetType = varDsc.GetRegisterType(tree);
        var isRegCandidate = varDsc.lvIsRegCandidate;

        assert((tree.Flags & GTF_VAR_DEF) == 0);

        // If this is a register candidate that has been spilled, genConsumeReg() will reload it at its use.
        // Otherwise, if it's not in a register, we load it here.
        if (!isRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            assert(targetType is not TYP_STRUCT);

            var ins = ins_Load(targetType);
            Emitter.emitIns_R_S(ins, emitActualTypeSize(targetType), tree.RegNum, varNum, 0);
            genProduceReg(tree);
        }
    }
}
#endif
