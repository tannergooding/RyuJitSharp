// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_WASM
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe GenTree? impSpecialIntrinsicWasmCore(
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_HANDLE clsHnd,
        CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType,
        var_types retType,
        byte simdSize,
        bool mustExpand)
    {
        var isa = HWIntrinsicInfo.lookupIsa(intrinsic);
        if (isa is InstructionSet_Vector)
        {
            return impXplatIntrinsic(intrinsic, clsHnd, method, in sig, in entryPoint,
                simdBaseType, retType, simdSize, mustExpand);
        }

        assert(varTypeIsArithmetic(simdBaseType));

        GenTree? retNode = null;
        GenTree? op1;
        GenTree? op2;

        switch (intrinsic)
        {
            case NI_WasmBase_LeadingZeroCount:
            {
                assert(sig.numArgs == 1);
                op1 = impPopStack().val;
                retNode = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_LeadingZeroCount, methodHandle: null);
                break;
            }

            case NI_WasmBase_TrailingZeroCount:
            {
                assert(sig.numArgs == 1);
                op1 = impPopStack().val;
                retNode = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_TrailingZeroCount, methodHandle: null);
                break;
            }

            case NI_PackedSimd_CompareGreaterThan:
            {
                assert(sig.numArgs == 2);
                assert(simdSize == 16);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdCmpOpNode(GT_GT, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_CompareGreaterThanOrEqual:
            {
                assert(sig.numArgs == 2);
                assert(simdSize == 16);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdCmpOpNode(GT_GE, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_CompareLessThan:
            {
                assert(sig.numArgs == 2);
                assert(simdSize == 16);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdCmpOpNode(GT_LT, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_CompareLessThanOrEqual:
            {
                assert(sig.numArgs == 2);
                assert(simdSize == 16);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdCmpOpNode(GT_LE, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_LoadVector128:
            {
                assert(sig.numArgs == 1);
                assert(simdSize == 16);
                op1 = stripWasmByrefCast(impPopStack().val);
                retNode = gtNewSimdLoadNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_Store:
            {
                assert(sig.numArgs == 2);
                assert(simdSize == 16);
                op2 = impPopStack().val;
                op1 = stripWasmByrefCast(impPopStack().val);
                retNode = gtNewSimdStoreNode(op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_PackedSimd_StoreSelectedScalar:
            {
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return retNode;
    }

    private static GenTree stripWasmByrefCast(GenTree node)
    {
        if ((node.Oper is GT_CAST) && (node.AsCast().CastOp.Type is TYP_BYREF))
        {
            return node.AsCast().CastOp;
        }

        return node;
    }
}
#endif
