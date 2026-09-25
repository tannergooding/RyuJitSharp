// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
public sealed partial class GenTreeHWIntrinsic
{
    public bool OperIsVectorFusedMultiplyOp => HWIntrinsicId is
        NI_AVX2_MultiplyAdd or NI_AVX2_MultiplyAddNegated or NI_AVX2_MultiplyAddNegatedScalar or
        NI_AVX2_MultiplyAddScalar or NI_AVX2_MultiplySubtract or NI_AVX2_MultiplySubtractNegated or
        NI_AVX2_MultiplySubtractNegatedScalar or NI_AVX2_MultiplySubtractScalar or
        NI_AVX512_FusedMultiplyAdd or NI_AVX512_FusedMultiplyAddNegated or
        NI_AVX512_FusedMultiplyAddNegatedScalar or NI_AVX512_FusedMultiplyAddScalar or
        NI_AVX512_FusedMultiplySubtract or NI_AVX512_FusedMultiplySubtractNegated or
        NI_AVX512_FusedMultiplySubtractNegatedScalar or NI_AVX512_FusedMultiplySubtractScalar;

    public int GetResultOpNumForRmwIntrinsic(GenTree? user, GenTree op1, GenTree op2, GenTree op3)
    {
        assert(HWIntrinsicInfo.IsFmaIntrinsic(HWIntrinsicId) || HWIntrinsicInfo.IsPermuteVar2x(HWIntrinsicId) ||
            HWIntrinsicId is NI_AVX512_TernaryLogic);

        if (user?.Oper is GT_STORE_LCL_VAR)
        {
            var overwrittenLclNum = user.AsLclVarCommon().LclNum;
            if (op1.Oper.IsLocal && op1.AsLclVarCommon().LclNum == overwrittenLclNum)
            {
                return 1;
            }
            if (op2.Oper.IsLocal && op2.AsLclVarCommon().LclNum == overwrittenLclNum)
            {
                return 2;
            }
            if (op3.Oper.IsLocal && op3.AsLclVarCommon().LclNum == overwrittenLclNum)
            {
                return 3;
            }
        }

        if (op1.Oper is GT_LCL_VAR && op1.IsLastUse(0))
        {
            return 1;
        }
        if (op2.Oper is GT_LCL_VAR && op2.IsLastUse(0))
        {
            return 2;
        }
        if (op3.Oper is GT_LCL_VAR && op3.IsLastUse(0))
        {
            return 3;
        }

        return 0;
    }

    public bool IsEmbeddedBroadcastCompatibleHWIntrinsic(Compiler compiler)
    {
        var id = HWIntrinsicId;
        var ins = HWIntrinsicInfo.lookupIns(id, SimdBaseType, compiler);
        var codeGen = compiler.codeGen ?? throw new System.InvalidOperationException("Code generation is not initialized.");
        if (!codeGen.Emitter.IsEvexEncodableInstruction(ins))
        {
            return false;
        }

        var tupleType = Emitter.insTupleTypeInfo(ins);
        if ((tupleType & INS_TT_IS_BROADCAST) == 0)
        {
            return false;
        }

        if ((tupleType & INS_TT_MEM128) != 0)
        {
            assert(Operands.Length == 2);
            return HWIntrinsicInfo.isImmOp(id, GetOp(2));
        }

        return true;
    }
}
#endif
