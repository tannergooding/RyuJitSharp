// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCast(GenTreeCast tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Scalar cast generation requires AMD64.");
#else
        assert(tree.Oper == GT_CAST);
        var targetType = tree.Type;
        if (varTypeIsFloating(targetType) && varTypeIsFloating(tree.CastOp.Type))
        {
            genFloatToFloatCast(tree);
        }
        else if (varTypeIsFloating(tree.CastOp.Type))
        {
            // Xarch floating-to-integer casts must already be lowered to hardware intrinsics.
            unreached();
        }
        else if (varTypeIsFloating(targetType))
        {
            genIntToFloatCast(tree);
        }
        else
        {
            genIntToIntCast(tree);
        }
#endif
    }
}
