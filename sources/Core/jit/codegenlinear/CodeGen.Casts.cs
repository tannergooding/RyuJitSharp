// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCast(GenTreeCast tree)
    {
        assert(tree.Oper == GT_CAST);
        var targetType = tree.Type;
        if (varTypeIsFloating(targetType) && varTypeIsFloating(tree.CastOp.Type))
        {
            genFloatToFloatCast(tree);
        }
        else if (varTypeIsFloating(tree.CastOp.Type))
        {
#if TARGET_XARCH
            // Xarch floating-to-integer casts must already be lowered to hardware intrinsics.
            unreached();
#else
            genFloatToIntCast(tree);
#endif
        }
        else if (varTypeIsFloating(targetType))
        {
            genIntToFloatCast(tree);
        }
#if !TARGET_64BIT && !TARGET_WASM
        else if (varTypeIsLong(tree.CastOp.Type))
        {
            genLongToIntCast(tree);
        }
#endif
        else
        {
            genIntToIntCast(tree);
        }
    }

#if !TARGET_XARCH && !TARGET_WASM
#if !TARGET_LOONGARCH64 && !TARGET_RISCV64
    private void genFloatToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-to-integer cast generation outside xarch is not ported.");
    }
#endif

    private void genIntToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Integer cast generation outside xarch is not ported.");
    }
#endif

#if TARGET_ARM
    private void genLongToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 long-to-integer cast generation is not ported.");
    }
#endif
}
