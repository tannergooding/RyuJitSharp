// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_64BIT && !TARGET_WASM
namespace RyuJitSharp;

public partial class Compiler
{
    private GenTreeOp fgMorphLongMul(GenTreeOp mul)
    {
#if DEBUG
        mul.DebugCheckLongMul();
#endif

        var op1 = mul.Op1;
        var op2 = mul.Op2;

        var op1Cast = op1.AsCast();
        op1Cast.Op1 = fgMorphTree(op1Cast.CastOp);
        op1.SetAllEffectsFlags(op1Cast.CastOp);

        if (op2.Oper is GT_CAST)
        {
            var op2Cast = op2.AsCast();
            op2Cast.Op1 = fgMorphTree(op2Cast.CastOp);
            op2.SetAllEffectsFlags(op2Cast.CastOp);
        }

        mul.SetAllEffectsFlags(op1, op2);
        op1.CanCse = false;
        op1.SetMorphed(this);
        op2.CanCse = false;
        op2.SetMorphed(this);

        return mul;
    }

    private GenTreeOp fgRecognizeAndMorphLongMul(GenTreeOp mul)
    {
        assert(mul.Oper is GT_MUL);
        assert(mul.Type is TYP_LONG);

        var op1 = mul.Op1;
        var op2 = mul.Op2;

        if (op1.Oper.IsIntegralConst)
        {
            (op1, op2) = (op2, op1);
            mul.Op1 = op1;
            mul.Op2 = op2;
        }

        if (!mul.IsValidLongMul())
        {
            return mul;
        }

        mul.IsUnsigned = (op1.Flags & GTF_UNSIGNED) != 0;
        mul.ClearOverflow();
        mul.Set64RsltMul();

        return fgMorphLongMul(mul);
    }
}
#endif
