// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genBitCast(var_types targetType, regNumber targetReg, var_types srcType, regNumber srcReg)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm register bitcast generation is not ported.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(varTypeUsesFloatReg(srcType) == genIsValidFloatReg(srcReg));
        assert(varTypeUsesFloatReg(targetType) == genIsValidFloatReg(targetReg));
        inst_Mov(targetType, targetReg, srcReg, canSkip: true);
#endif
    }

    public void genCodeForBitCast(GenTreeUnOp tree)
    {
#if TARGET_WASM
        assert(tree.Oper is GT_BITCAST);

        if (tree.Op1.IsContained)
        {
            assert(tree.Op1.Oper is GT_LCL_VAR);
            genCodeForLclVar(tree.Op1.AsLclVar());
        }
        else
        {
            genConsumeOperands(tree);
        }

        var toType = tree.Type;
        var fromType = genActualType(tree.Op1.Type);
        assert(toType == genActualType(tree));

        var ins = WasmBitCastInstruction(toType, fromType);
        if (ins is not INS_none)
        {
            GetEmitter().emitIns(ins);
        }

        WasmProduceReg(tree);
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
