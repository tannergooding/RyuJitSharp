// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genFloatToFloatCast(GenTree treeNode)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Floating casts outside Windows AMD64 are not implemented.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(treeNode.Oper is GT_CAST);
        assert(!treeNode.HasOverflowCheck);
        var cast = treeNode.AsCast();
        var targetReg = cast.RegNum;
        assert(genIsValidFloatReg(targetReg));
        var op1 = cast.CastOp;
#if DEBUG
        if (op1.IsUsedFromReg)
        {
            assert(genIsValidFloatReg(op1.RegNum));
        }
#endif
        var dstType = cast.CastType;
        var srcType = op1.Type;
        assert(varTypeIsFloating(srcType) && varTypeIsFloating(dstType));

        genConsumeOperands(cast);
        if (srcType == dstType)
        {
            if (op1.IsUsedFromReg)
            {
                _ = Emitter.emitIns_Mov(INS_movaps, EA_16BYTE, targetReg, op1.RegNum, canSkip: true);
            }
            else
            {
                inst_RV_TT(ins_Move_Extend(dstType, srcInReg: false), dstType.EmitSize, targetReg, op1);
            }
        }
        else
        {
            var ins = ins_FloatConv(dstType, srcType);
            var isRmw = !_compiler.canUseVexEncoding();
            inst_RV_RV_TT(ins, dstType.EmitSize, targetReg, targetReg, op1, isRmw, INS_OPTS_NONE);
        }

        genProduceReg(cast);
#endif
    }

    public void genIntToFloatCast(GenTree treeNode)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Integral-to-floating casts outside Windows AMD64 are not implemented.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(treeNode.Oper is GT_CAST);
        assert(!treeNode.HasOverflowCheck);
        var cast = treeNode.AsCast();
        var targetReg = cast.RegNum;
        assert(genIsValidFloatReg(targetReg));
        var op1 = cast.CastOp;
#if DEBUG
        if (op1.IsUsedFromReg)
        {
            assert(genIsValidIntReg(op1.RegNum));
        }
#endif
        var dstType = cast.CastType;
        var srcType = op1.Type.ActualType;
        assert(!varTypeIsFloating(srcType) && varTypeIsFloating(dstType));

        // Only known stack addresses can retain GC typing at this point. Other GC
        // sources must have been copied to non-GC temporaries during morphing.
        if (srcType == TYP_BYREF)
        {
            noway_assert(op1.Oper is GT_LCL_ADDR);
            srcType = TYP_I_IMPL;
        }

        genConsumeOperands(cast);
        // Scalar conversions preserve upper lanes, creating a false dependency on
        // the destination. Clear it before both signed and unsigned conversions.
        Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
        if (cast.IsUnsigned)
        {
            srcType = varTypeToUnsigned(srcType);
        }

        if ((srcType == TYP_ULONG) && !_compiler.canUseEvexEncoding())
        {
            assert(op1.IsUsedFromReg);
            var convIns = ins_FloatConv(dstType, TYP_LONG);
            var addIns = dstType == TYP_FLOAT ? INS_addss : INS_addsd;
            var argReg = op1.RegNum;

            // Extract the APX-incompatible conversion operand first; the second
            // temporary can be an EGPR. LSRA's masks rely on lowest-register order.
            var tmpReg2 = _internalRegisters.Extract(cast);
            var tmpReg1 = _internalRegisters.Extract(cast);

            // Clang's strict-FP sequence converts (arg >> 1) | (arg & 1) for
            // negative signed inputs, then doubles the result. The sticky low bit
            // preserves rounding; nonnegative inputs use the original value.
            inst_Mov(TYP_LONG, tmpReg1, argReg, canSkip: false, EA_8BYTE);
            inst_RV_SH(INS_shr, EA_8BYTE, tmpReg1, 1);
            inst_Mov(TYP_INT, tmpReg2, argReg, canSkip: false, EA_4BYTE);
            Emitter.emitIns_R_I(INS_and, EA_4BYTE, tmpReg2, 1);
            Emitter.emitIns_R_R(INS_or, EA_8BYTE, tmpReg2, tmpReg1);
            Emitter.emitIns_R_R(INS_test, EA_8BYTE, argReg, argReg);
            Emitter.emitIns_R_R(INS_cmovns, EA_8BYTE, tmpReg2, argReg);
            Emitter.emitIns_R_R(convIns, EA_8BYTE, targetReg, tmpReg2);

            var label = genCreateTempLabel();
            inst_JMP(EJ_jns, label);
            Emitter.emitIns_R_R(addIns, dstType.EmitSize, targetReg, targetReg);
            genDefineTempLabel(label);
        }
        else
        {
#if DEBUG
            assert(varTypeIsIntOrI(srcType) || _compiler.canUseEvexEncodingDebugOnly());
#endif
            var ins = ins_FloatConv(dstType, srcType);
            var isRmw = !_compiler.canUseVexEncoding();
            inst_RV_RV_TT(ins, srcType.EmitSize, targetReg, targetReg, op1, isRmw, INS_OPTS_NONE);
        }

        genProduceReg(cast);
#endif
    }
}
