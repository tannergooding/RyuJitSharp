// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genBitCast(var_types targetType, regNumber targetReg, var_types srcType, regNumber srcReg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Bitcast generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(varTypeUsesFloatReg(srcType) == genIsValidFloatReg(srcReg));
        assert(varTypeUsesFloatReg(targetType) == genIsValidFloatReg(targetReg));
        inst_Mov(targetType, targetReg, srcReg, canSkip: true);
#endif
    }

    public void genCodeForBitCast(GenTreeUnOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Bitcast node generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Type == tree.Type.ActualType);
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var op1 = tree.Op1;
        genConsumeRegs(op1);

        if (op1.IsContained)
        {
            assert(op1.Oper is GT_LCL_VAR);
            var lclNum = op1.AsLclVarCommon().LclNum;
            var loadIns = ins_Load(targetType, _compiler.isSIMDTypeLocalAligned(lclNum));
            Emitter.emitIns_R_S(loadIns, targetType.EmitSize, targetReg, lclNum, 0);
        }
        else
        {
            genBitCast(targetType, targetReg, op1.Type, op1.RegNum);
        }

        genProduceReg(tree);
#endif
    }
}
