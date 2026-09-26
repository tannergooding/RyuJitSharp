// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_SIMD
    private GenTree impSIMDPopStack()
    {
        var tree = impPopStack().val;
        assert(varTypeIsSimdOrMask(tree.Type));

        // A call-like SIMD value may need a return buffer; normalize it before using it as an operand.
        if (tree.Oper is GT_CALL or GT_RET_EXPR)
        {
            tree = impNormStructVal(tree, CHECK_SPILL_ALL);
        }

        return tree;
    }

#if FEATURE_HW_INTRINSICS
    private unsafe GenTree getArgForHWIntrinsic(var_types argType, CORINFO_CLASS_HANDLE argClass)
    {
        GenTree arg;

        if (varTypeIsStruct(argType))
        {
            if (!varTypeIsSimd(argType))
            {
                _ = getBaseTypeAndSizeOfSimdType(argClass, out var argSizeBytes);
                argType = GetSimdTypeForSize(argSizeBytes);
            }
            assert(varTypeIsSimd(argType));

            arg = impSIMDPopStack();
            assert(varTypeIsSimdOrMask(arg.Type));
        }
        else
        {
            assert(varTypeIsArithmetic(argType) || (argType is TYP_BYREF));

            arg = impPopStack().val;
            assert(varTypeIsArithmetic(arg.Type) || ((argType is TYP_BYREF) && (arg.Type is TYP_BYREF)));

            if (!impCheckImplicitArgumentCoercion(argType, arg.Type))
            {
                BADCODE("the hwintrinsic argument has a type that can't be implicitly converted to the signature type");
            }
        }

        return arg;
    }
#endif
#endif
}
