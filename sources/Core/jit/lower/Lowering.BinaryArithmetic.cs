// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerBinaryArithmetic(GenTreeOp binOp)
    {
#if TARGET_XARCH
        if (CompilerInstance.opts.OptimizationEnabled && varTypeIsIntegral(binOp.Type) &&
            (binOp.Oper is GT_OR or GT_XOR or GT_AND))
        {
            var replacementNode = TryLowerBitwiseOpToBitOp(binOp);
            if (replacementNode is not null)
            {
                return replacementNode.Next;
            }
        }

#if FEATURE_HW_INTRINSICS
        if (CompilerInstance.opts.OptimizationEnabled && varTypeIsIntegral(binOp.Type))
        {
            if (binOp.Oper is GT_AND)
            {
                var replacementNode = TryLowerAndOpToAndNot(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToResetLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToExtractLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToZeroHighBits(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }
            }
            else if (binOp.Oper is GT_XOR)
            {
                var replacementNode = TryLowerXorOpToGetMaskUpToLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }
            }
        }
#endif

        ContainCheckBinary(binOp);
#if TARGET_AMD64
        if (CompilerInstance.canUseApxEvexEncoding() && (JitConfig.EnableApxConditionalChaining != 0))
        {
            if ((binOp.Oper is GT_AND or GT_OR) && TryLowerAndOrToCCMP(binOp, out var next))
            {
                return next;
            }
        }
#endif

        return binOp.Next;
#else
        throw new NotImplementedException("Non-xarch binary arithmetic lowering is not ported.");
#endif
    }
}
