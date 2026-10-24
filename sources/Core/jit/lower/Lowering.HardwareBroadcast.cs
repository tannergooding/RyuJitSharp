// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void TryFoldCnsVecForEmbeddedBroadcast(GenTreeHWIntrinsic parentNode, GenTreeVecCon cnsVec)
    {
        assert(!cnsVec.IsAllBitsSet);
        assert(!cnsVec.IsZero);

        if (JitConfig.EnableEmbeddedBroadcast == 0)
        {
            MakeSrcContained(parentNode, cnsVec);
            return;
        }

        var simdBaseType = parentNode.SimdBaseType;
        var ins = HWIntrinsicInfo.lookupIns(parentNode.HWIntrinsicId, simdBaseType, CompilerInstance);
        var broadcastSize = CodeGen.instInputSize(ins);
        var broadcastType = parentNode.SimdBaseTypeAsVarType;

        if (broadcastSize != simdBaseType.Size)
        {
            if (broadcastSize == 4)
            {
                broadcastType = varTypeIsFloating(simdBaseType) ? TYP_FLOAT : TYP_INT;
            }
            else
            {
                assert(broadcastSize == 8);
                broadcastType = varTypeIsFloating(simdBaseType) ? TYP_DOUBLE : TYP_LONG;
            }
        }

        if (!cnsVec.IsBroadcast(broadcastType))
        {
            var canUse8ByteBroadcast = false;

            if (broadcastSize == 4)
            {
                switch (ins)
                {
                    case INS_andps:
                    case INS_andnps:
                    case INS_orps:
                    case INS_xorps:
                    case INS_pandd:
                    case INS_pandnd:
                    case INS_pord:
                    case INS_pxord:
                    case INS_vpternlogd:
                    case INS_vshuff32x4:
                    case INS_vshufi32x4:
                    {
                        canUse8ByteBroadcast = cnsVec.IsBroadcast(TYP_LONG);
                        break;
                    }
                }
            }

            if (canUse8ByteBroadcast)
            {
                broadcastType = varTypeIsFloating(simdBaseType) ? TYP_DOUBLE : TYP_LONG;
                parentNode.SimdBaseType = broadcastType;
            }
            else
            {
                MakeSrcContained(parentNode, cnsVec);
                return;
            }
        }
        else if (broadcastSize == 8)
        {
            switch (ins)
            {
                case INS_andpd:
                case INS_andnpd:
                case INS_orpd:
                case INS_xorpd:
                case INS_vpandq:
                case INS_vpandnq:
                case INS_vporq:
                case INS_vpxorq:
                case INS_vpternlogq:
                case INS_vshuff64x2:
                case INS_vshufi64x2:
                {
                    if (cnsVec.IsBroadcast(TYP_INT))
                    {
                        broadcastType = varTypeIsFloating(simdBaseType) ? TYP_FLOAT : TYP_INT;
                        parentNode.SimdBaseType = broadcastType;
                    }
                    break;
                }
            }
        }

        var simdType = cnsVec.Type;
        global::RyuJitSharp.NamedIntrinsic __jitExceptionResult467;
        switch (simdType)
        {
            case TYP_SIMD16:
                __jitExceptionResult467 = NI_AVX2_BroadcastScalarToVector128;
                break;
            case TYP_SIMD32:
                __jitExceptionResult467 = NI_AVX2_BroadcastScalarToVector256;
                break;
            case TYP_SIMD64:
                __jitExceptionResult467 = NI_AVX512_BroadcastScalarToVector512;
                break;
            default:
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedBroadcastSizeArgument,
                        simdType);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedBroadcastSizeArgument);
                }

                __jitExceptionResult467 = default;
                break;
        }
        var broadcastName = __jitExceptionResult467;

        global::RyuJitSharp.GenTree __jitExceptionResult468;
        switch (broadcastType)
        {
            case TYP_FLOAT:
                __jitExceptionResult468 = CompilerInstance.gtNewDconNode(TYP_FLOAT, cnsVec.SimdVal.f32[0]);
                break;
            case TYP_DOUBLE:
                __jitExceptionResult468 = CompilerInstance.gtNewDconNode(TYP_DOUBLE, cnsVec.SimdVal.f64[0]);
                break;
            case TYP_INT:
                __jitExceptionResult468 = CompilerInstance.gtNewIconNode(TYP_INT, cnsVec.SimdVal.i32[0]);
                break;
            case TYP_LONG:
                __jitExceptionResult468 = CompilerInstance.gtNewLconNode(cnsVec.SimdVal.i64[0]);
                break;
            default:
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedBroadcastTypeArgument,
                        broadcastType);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedBroadcastTypeArgument);
                }

                __jitExceptionResult468 = default;
                break;
        }
        var constScalar = __jitExceptionResult468;

        var broadcastNode = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, broadcastName,
            broadcastType, checked((byte)simdType.Size), constScalar);
        BlockRange().InsertBefore(parentNode, constScalar, broadcastNode);
        BlockRange().Remove(cnsVec);

        var foundUse = false;
        for (var index = 1; index <= parentNode.Operands.Length; index++)
        {
            if (ReferenceEquals(parentNode.GetOp(index), cnsVec))
            {
                parentNode.ReplaceOperand(ref parentNode.GetOpRef(index), broadcastNode);
                foundUse = true;
                break;
            }
        }
        assert(foundUse);

        MakeSrcContained(broadcastNode, constScalar);
        MakeSrcContained(parentNode, broadcastNode);
    }
}
