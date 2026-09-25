// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local field stores require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STORE_LCL_FLD);
        var targetType = tree.Type;
        noway_assert(targetType is not TYP_STRUCT);

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(tree);
            return;
        }
#endif

        var op1 = tree.Op1;
        var targetReg = tree.RegNum;
        var lclNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        assert(varTypeUsesSameRegType(targetType, op1.Type));
        assert(targetType.ActualType.Size == op1.Type.ActualType.Size);
        genConsumeRegs(op1);

        if ((op1.Oper is GT_BITCAST) && op1.IsContained)
        {
            var bitCastSrc = op1.AsUnOp().Op1;
            var srcType = bitCastSrc.Type;
            noway_assert(!bitCastSrc.IsContained);

            if (targetReg == REG_NA)
            {
                Emitter.emitIns_S_R(ins_Store(srcType, _compiler.isSIMDTypeLocalAligned(lclNum)),
                    targetType.EmitSize, bitCastSrc.RegNum, lclNum, tree.LclOffs);
            }
            else
            {
                genBitCast(targetType, targetReg, srcType, bitCastSrc.RegNum);
            }
        }
        else
        {
            _ = Emitter.emitInsBinary(ins_Store(targetType), tree.Type.EmitSize, tree, op1);
        }

        genUpdateLifeStore(tree, targetReg, ref varDsc);
#endif
    }

    public void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local variable stores require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STORE_LCL_VAR);
        var targetReg = tree.RegNum;
        var op1 = tree.Op1;

        if (op1.SkipCopyOrReload.IsMultiRegNode)
        {
            genMultiRegStoreToLocal(tree);
        }
        else
        {
            var lclNum = tree.LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            var targetType = varDsc.GetRegisterType(tree);

#if DEBUG
            var op1Type = op1.Type;
            if (op1Type is TYP_STRUCT)
            {
                assert(op1.Oper.IsLocal);
                var op1LclVar = op1.AsLclVar();
                ref var op1VarDsc = ref _compiler.lvaGetDesc(op1LclVar.LclNum);
                op1Type = op1VarDsc.GetRegisterType(op1LclVar);
            }
            assert(varTypeUsesSameRegType(targetType, op1Type));
            assert(varTypeUsesIntReg(targetType) || (targetType.EmitSize == op1Type.EmitSize));
#endif

#if FEATURE_SIMD
            if (targetType is TYP_SIMD12)
            {
                genStoreLclTypeSimd12(tree);
                return;
            }
#endif

            genConsumeRegs(op1);

            if ((op1.Oper is GT_BITCAST) && op1.IsContained)
            {
                var bitCastSrc = op1.AsUnOp().Op1;
                var srcType = bitCastSrc.Type;
                noway_assert(!bitCastSrc.IsContained);

                if (targetReg == REG_NA)
                {
                    Emitter.emitIns_S_R(ins_Store(srcType, _compiler.isSIMDTypeLocalAligned(lclNum)),
                        targetType.EmitSize, bitCastSrc.RegNum, lclNum, 0);
                }
                else
                {
                    genBitCast(targetType, targetReg, srcType, bitCastSrc.RegNum);
                }
            }
            else if (targetReg == REG_NA)
            {
                Emitter.emitInsStoreLcl(ins_Store(targetType, _compiler.isSIMDTypeLocalAligned(lclNum)),
                    targetType.EmitSize, tree);
            }
            else
            {
                // A reused zero in a different register is cheaper to recreate than copy.
                if (op1.IsUsedFromReg && (op1.RegNum != targetReg) &&
                    (op1.IsIntegralConst(0) || op1.IsFloatPositiveZero))
                {
                    op1.RegNum = REG_NA;
                    op1.IsReuseRegVal = false;
                    op1.IsContained = true;
                }

                if (!op1.IsUsedFromReg)
                {
                    // Containment is only supported here for constants, not memory sources.
                    assert((op1.RegNum == REG_NA) && op1.Oper.IsConst);
                    genSetRegToConst(targetReg, targetType, op1);
                }
                else
                {
                    assert(targetReg == tree.RegNum);
                    assert(op1.RegNum != REG_NA);
                    inst_Mov_Extend(targetType, srcInReg: true, targetReg, op1.RegNum, canSkip: true,
                        targetType.EmitSize);
                }
            }

            genUpdateLifeStore(tree, targetReg, ref varDsc);
        }
#endif
    }

    public void genUpdateLifeStore(GenTree tree, regNumber targetReg, ref LclVarDsc varDsc)
    {
        if (targetReg != REG_NA)
        {
            genProduceReg(tree);
        }
        else
        {
            genUpdateLife(tree);
            varDsc.RegNum = REG_STK;
        }
    }

#if FEATURE_SIMD
    public void genStoreLclTypeSimd12(GenTreeLclVarCommon tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 local stores require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STORE_LCL_FLD or GT_STORE_LCL_VAR);
        var offset = tree.LclOffs;
        var varNum = tree.LclNum;
        assert((uint)varNum < _compiler.lvaCount);
        var data = tree.Data;
        assert(!data.IsContained);
        var targetReg = tree.RegNum;
        var dataReg = genConsumeReg(data);
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);

        if (targetReg != REG_NA)
        {
            assert(genIsValidFloatReg(targetReg));
            inst_Mov(tree.Type, targetReg, dataReg, canSkip: true);
        }
        else
        {
            genEmitStoreLclTypeSimd12(tree, varNum, offset);
        }

        genUpdateLifeStore(tree, targetReg, ref varDsc);
#endif
    }

    public void genEmitStoreLclTypeSimd12(GenTree store, int lclNum, uint offset)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 stack stores require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(store.Oper.IsLocalStore || (store.Oper is GT_STOREIND));
        var data = store.Data;
        var dataReg = data.RegNum;
        Emitter.emitIns_S_R(INS_movsd_simd, EA_8BYTE, dataReg, lclNum, unchecked((int)offset));

        if (data.IsVectorZero)
        {
            Emitter.emitIns_S_R(INS_movss, EA_4BYTE, dataReg, lclNum, unchecked((int)(offset + 8)));
        }
        else
        {
            Emitter.emitIns_S_R_I(INS_extractps, EA_16BYTE, lclNum, unchecked((int)(offset + 8)), dataReg, 2);
        }
#endif
    }
#endif
}
#endif
