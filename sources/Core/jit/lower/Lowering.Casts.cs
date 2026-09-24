// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckCast(GenTreeCast node)
    {
#if TARGET_XARCH
        var castOp = node.CastOp;
        var castToType = node.CastType;
        var srcType = node.IsUnsigned ? varTypeToUnsigned(castOp.Type) : castOp.Type;

        if (!node.HasOverflowCheck)
        {
            var srcIsContainable = false;
            if (varTypeIsFloating(castToType) || varTypeIsFloating(srcType))
            {
                if (castOp.IsCnsNonZeroFltOrDbl)
                {
                    MakeSrcContained(node, castOp);
                }
                else
                {
                    // The SSE2 ulong-to-float fallback needs its source in a register.
                    srcIsContainable = !varTypeIsSmall(srcType) &&
                        ((srcType is not TYP_ULONG) || CompilerInstance.canUseEvexEncoding());
                }
            }
            else if (CompilerInstance.opts.Tier0OptimizationEnabled &&
                varTypeIsIntegral(castOp.Type) && varTypeIsIntegral(castToType))
            {
                // A contained load must perform the same sign or zero extension as the cast.
                srcIsContainable = !varTypeIsSmall(castOp.Type) ||
                    (varTypeIsUnsigned(castOp.Type) == node.IsZeroExtending);
            }

            if (srcIsContainable)
            {
                TryMakeSrcContainedOrRegOptional(node, castOp);
            }
        }

#if !TARGET_64BIT
        if (varTypeIsLong(srcType))
        {
            noway_assert(castOp.Oper is GT_LONG);
            castOp.IsContained = true;
        }
#endif
#else
        throw new System.NotImplementedException("Non-xarch cast containment is not ported.");
#endif
    }
}
