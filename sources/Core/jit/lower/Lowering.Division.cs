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
#elif TARGET_ARM
        assert(node.Oper is GT_DIV or GT_UDIV or GT_MOD);
        // ARM32 division has no immediate or memory containment.
#elif TARGET_ARM64
        assert(node.Oper is GT_DIV or GT_UDIV or GT_MOD);
        // Native ARM64 division has no immediate or memory containment.
#elif TARGET_RISCV64
        assert(node.Oper is GT_MOD or GT_UMOD or GT_DIV or GT_UDIV);
#elif TARGET_WASM
        // Wasm does not contain operands for integer division.
#elif TARGET_LOONGARCH64
        assert(node.Oper is GT_MOD or GT_UMOD or GT_DIV or GT_UDIV);
#else
        throw new System.NotImplementedException("Division containment is not ported for this target.");
#endif
    }

    private void LowerDivOrMod(GenTreeOp divMod)
    {
#if TARGET_WASM
        var exceptions = divMod.Exceptions(CompilerInstance);
        if ((exceptions & ExceptionSetFlags.ArithmeticException) is not ExceptionSetFlags.None)
        {
            SetMultiplyUsed(divMod.Op1
#if DEBUG
                , "LowerDivOrMod op1 (arithmetic exception)"
#endif
            );
            SetMultiplyUsed(divMod.Op2
#if DEBUG
                , "LowerDivOrMod op2 (arithmetic exception)"
#endif
            );
        }
        else if ((exceptions & ExceptionSetFlags.DivideByZeroException) is not ExceptionSetFlags.None)
        {
            SetMultiplyUsed(divMod.Op2
#if DEBUG
                , "LowerDivOrMod op2 (divide by zero exception)"
#endif
            );
        }
#endif
        ContainCheckDivOrMod(divMod);
    }

#if TARGET_WASM
    private static void SetMultiplyUsed(GenTree node
#if DEBUG
        , string reason
#endif
    )
    {
#if DEBUG
        JITDUMP($"Setting [{node.TreeId:D6}] as multiply-used: {reason}\n");
#endif
        assert(varTypeIsEnregisterable(node.Type));
        assert(!node.IsContained);
        node._lirFlags |= LIR.Flags.MultiplyUsed;
    }
#endif
}
