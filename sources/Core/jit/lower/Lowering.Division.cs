// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckDivOrMod(GenTreeOp node)
    {
        if (Configuration.TARGET_XARCH)
        {
            assert(node.Oper is GT_DIV or GT_MOD or GT_UDIV or GT_UMOD);

            if (varTypeIsFloating(node.Type))
            {
                ContainCheckFloatBinary(node);
                return;
            }

            var divisor = node.Op2;
            var divisorCanBeRegOptional = true;
            if (Configuration.TARGET_X86)
            {
                var dividend = node.Op1;
                if (dividend.Oper is GT_LONG)
                {
                    divisorCanBeRegOptional = false;
                    MakeSrcContained(node, dividend);
                }
            }

            if (IsContainableMemoryOp(divisor) && (divisor.Type == node.Type) &&
                IsInvariantInRange(divisor, node))
            {
                MakeSrcContained(node, divisor);
            }
            else if (divisorCanBeRegOptional && IsSafeToMarkRegOptional(node, divisor))
            {
                MakeSrcRegOptional(node, divisor);
            }

            return;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(node.Oper is GT_DIV or GT_UDIV or GT_MOD);
            // ARM32 division has no immediate or memory containment.
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            assert(node.Oper is GT_DIV or GT_UDIV or GT_MOD);
            // Native ARM64 division has no immediate or memory containment.
            return;
        }

        if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
        {
            assert(node.Oper is GT_MOD or GT_UMOD or GT_DIV or GT_UDIV);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            // Wasm does not contain operands for integer division.
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Division containment is not ported for this target.");
    }

    private void LowerDivOrMod(GenTreeOp divMod)
    {
        if (Configuration.TARGET_WASM)
        {
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
        }
        ContainCheckDivOrMod(divMod);
    }

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
}
