// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS && FEATURE_SIMD
    private GenTree? impSimdCreate(
        NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig, var_types simdBaseType, var_types retType, byte simdSize)
    {
        if (sig.numArgs == 1)
        {
            var op1 = impStackTop().val;

#if TARGET_WASM
            if (!op1.Oper.IsIntegralConst && !op1.Oper.IsCnsFltOrDbl)
            {
                return null;
            }
#endif

            op1 = impPopStack().val;
            return gtNewSimdCreateBroadcastNode(retType, op1, simdBaseType, simdSize);
        }

        var simdLength = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        assert(sig.numArgs == simdLength);

        var isConstant = true;

        if (varTypeIsFloating(simdBaseType))
        {
            for (var index = 0; index < sig.numArgs; index++)
            {
                if (!impStackTop(index).val.Oper.IsCnsFltOrDbl)
                {
                    isConstant = false;
                    break;
                }
            }
        }
        else
        {
            assert(varTypeIsIntegral(simdBaseType));

            for (var index = 0; index < sig.numArgs; index++)
            {
                if (!impStackTop(index).val.Oper.IsIntegralConst)
                {
                    isConstant = false;
                    break;
                }
            }
        }

        if (isConstant)
        {
            var vecCon = gtNewVconNode(retType);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.u8[simdLength - 1 - index] =
                            unchecked((byte)impPopStack().val.AsIntConCommon().IntegralValue);
                    }
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.u16[simdLength - 1 - index] =
                            unchecked((ushort)impPopStack().val.AsIntConCommon().IntegralValue);
                    }
                    break;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.u32[simdLength - 1 - index] =
                            unchecked((uint)impPopStack().val.AsIntConCommon().IntegralValue);
                    }
                    break;
                }

                case TYP_LONG:
                case TYP_ULONG:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.u64[simdLength - 1 - index] =
                            unchecked((ulong)impPopStack().val.AsIntConCommon().IntegralValue);
                    }
                    break;
                }

                case TYP_FLOAT:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.f32[simdLength - 1 - index] =
                            (float)impPopStack().val.AsDblCon().DconVal;
                    }
                    break;
                }

                case TYP_DOUBLE:
                {
                    for (var index = 0; index < sig.numArgs; index++)
                    {
                        vecCon.SimdVal.f64[simdLength - 1 - index] =
                            impPopStack().val.AsDblCon().DconVal;
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            return vecCon;
        }

#if TARGET_WASM
        return null;
#else
        var operands = new GenTree[sig.numArgs];

        for (var index = operands.Length - 1; index >= 0; index--)
        {
            operands[index] = impPopStack().val;
        }

        return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, operands);
#endif
    }
#endif
}
