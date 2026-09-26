// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTreeHWIntrinsic? impNonConstFallback(NamedIntrinsic intrinsic, var_types simdType, var_types simdBaseType)
    {
        assert((HWIntrinsicInfo.lookupFlags(intrinsic) &
            (HW_Flag_NoJmpTableIMM | HW_Flag_MaybeNoJmpTableIMM)) != 0);

        switch (intrinsic)
        {
            case NI_X86Base_ShiftLeftLogical:
            case NI_X86Base_ShiftRightArithmetic:
            case NI_X86Base_ShiftRightLogical:
            case NI_AVX2_ShiftLeftLogical:
            case NI_AVX2_ShiftRightArithmetic:
            case NI_AVX2_ShiftRightLogical:
            case NI_AVX512_ShiftLeftLogical:
            case NI_AVX512_ShiftRightArithmetic:
            case NI_AVX512_ShiftRightLogical:
            {
                // The alternative consumes only the low eight bits of the shift count.
                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    "Spilling op1 side effects for HWIntrinsic");

                var op2 = impPopStack().val;
                var op1 = impSIMDPopStack();
                var tmpOp = gtNewSimdCreateScalarNode(TYP_SIMD16, op2, TYP_INT, 16);

                return gtNewSimdHWIntrinsicNode(simdType, intrinsic, simdBaseType,
                    (byte)simdType.Size, op1, tmpOp);
            }

            case NI_AVX512_RotateLeft:
            case NI_AVX512_RotateRight:
            {
                var variableIntrinsic = (NamedIntrinsic)((int)intrinsic + 1);
                assert((NI_AVX512_RotateLeftVariable == NI_AVX512_RotateLeft + 1) &&
                    (NI_AVX512_RotateRightVariable == NI_AVX512_RotateRight + 1));

                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    "Spilling op1 side effects for HWIntrinsic");

                var op2 = impPopStack().val;
                var op1 = impSIMDPopStack();

                if (varTypeIsLong(simdBaseType))
                {
                    op2 = gtNewCastNode(TYP_LONG, op2, true, TYP_LONG);
                }

                var tmpOp = gtNewSimdCreateBroadcastNode(simdType, op2, simdBaseType,
                    (byte)simdType.Size);
                return gtNewSimdHWIntrinsicNode(simdType, variableIntrinsic, simdBaseType,
                    (byte)simdType.Size, op1, tmpOp);
            }

            default:
            {
                return null;
            }
        }
    }
#endif
}
