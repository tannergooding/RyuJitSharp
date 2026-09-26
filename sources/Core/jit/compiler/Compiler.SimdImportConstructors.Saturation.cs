// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdNarrowWithSaturationNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(op2 is not null && op2.Type == type);
        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            return gtNewSimdNarrowNode(type, op1, op2, TYP_FLOAT, simdSize);
        }

        var minCns = varTypeIsSigned(simdBaseType) ? gtNewVconNode(type) : null;
        var maxCns = gtNewVconNode(type);
        var narrowBaseType = simdBaseType switch
        {
            TYP_SHORT => TYP_BYTE,
            TYP_USHORT => TYP_UBYTE,
            TYP_INT => TYP_SHORT,
            TYP_UINT => TYP_USHORT,
            TYP_LONG => TYP_INT,
            TYP_ULONG => TYP_UINT,
            _ => throw new FatalJitException("Unsupported saturating narrowing type."),
        };
        switch (simdBaseType)
        {
            case TYP_SHORT:
            {
                assert(minCns is not null);
                minCns.EvaluateBroadcastInPlace(TYP_SHORT, sbyte.MinValue);
                maxCns.EvaluateBroadcastInPlace(TYP_SHORT, sbyte.MaxValue);
                break;
            }

            case TYP_USHORT:
            {
                maxCns.EvaluateBroadcastInPlace(TYP_USHORT, byte.MaxValue);
                break;
            }

            case TYP_INT:
            {
                assert(minCns is not null);
                minCns.EvaluateBroadcastInPlace(TYP_INT, short.MinValue);
                maxCns.EvaluateBroadcastInPlace(TYP_INT, short.MaxValue);
                break;
            }

            case TYP_UINT:
            {
                maxCns.EvaluateBroadcastInPlace(TYP_UINT, ushort.MaxValue);
                break;
            }

            case TYP_LONG:
            {
                assert(minCns is not null);
                minCns.EvaluateBroadcastInPlace(TYP_LONG, int.MinValue);
                maxCns.EvaluateBroadcastInPlace(TYP_LONG, int.MaxValue);
                break;
            }

            case TYP_ULONG:
            {
                maxCns.EvaluateBroadcastInPlace(TYP_ULONG, uint.MaxValue);
                break;
            }
        }

        if (minCns is not null)
        {
            op1 = gtNewSimdMinMaxNode(type, op1, minCns, simdBaseType, simdSize,
                isMax: true, isMagnitude: false, isNumber: false);
            op2 = gtNewSimdMinMaxNode(type, op2, gtCloneExpr(minCns), simdBaseType, simdSize,
                isMax: true, isMagnitude: false, isNumber: false);
        }
        op1 = gtNewSimdMinMaxNode(type, op1, maxCns, simdBaseType, simdSize,
            isMax: false, isMagnitude: false, isNumber: false);
        op2 = gtNewSimdMinMaxNode(type, op2, gtCloneExpr(maxCns), simdBaseType, simdSize,
            isMax: false, isMagnitude: false, isNumber: false);
        return gtNewSimdNarrowNode(type, op1, op2, narrowBaseType, simdSize);
    }
#endif
}
