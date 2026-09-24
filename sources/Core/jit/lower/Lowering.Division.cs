// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckDivOrMod(GenTreeOp node)
    {
#if TARGET_XARCH
        assert(node.Oper is GT_DIV or GT_MOD or GT_UDIV or GT_UMOD);

        if (varTypeIsFloating(node.Type))
        {
            ContainCheckFloatBinary(node);
            return;
        }

        var divisor = node.Op2;
        var divisorCanBeRegOptional = true;
#if TARGET_X86
        var dividend = node.Op1;
        if (dividend.Oper is GT_LONG)
        {
            divisorCanBeRegOptional = false;
            MakeSrcContained(node, dividend);
        }
#endif

        if (IsContainableMemoryOp(divisor) && (divisor.Type == node.Type) &&
            IsInvariantInRange(divisor, node))
        {
            MakeSrcContained(node, divisor);
        }
        else if (divisorCanBeRegOptional && IsSafeToMarkRegOptional(node, divisor))
        {
            MakeSrcRegOptional(node, divisor);
        }
#else
        throw new System.NotImplementedException("Division containment outside xarch is not ported.");
#endif
    }

    private void LowerDivOrMod(GenTreeOp divMod)
    {
        ContainCheckDivOrMod(divMod);
    }
}
