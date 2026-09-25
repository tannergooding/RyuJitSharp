// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree NewMemcmpBinaryOp(genTreeOps operation, var_types type, GenTree left, GenTree right)
    {
#if FEATURE_SIMD
        if (varTypeIsSimd(left.Type))
        {
            var compiler = CompilerInstance;
            if (operation is GT_EQ)
            {
                assert(type is TYP_INT);
                return compiler.gtNewSimdCmpOpAllNode(operation, TYP_INT, left, right, TYP_U_IMPL, left.Type.Size);
            }
            return compiler.gtNewSimdBinOpNode(operation, left.Type, left, right, TYP_U_IMPL, left.Type.Size);
        }
#endif
        return CompilerInstance.gtNewBinaryNode(operation, type, left, right);
    }

    private unsafe bool LowerCallMemcmp(GenTreeCall call, out GenTree? next)
    {
#if !TARGET_AMD64
        throw new NotImplementedException("LowerCallMemcmp outside AMD64 is not ported.");
#else
        var compiler = CompilerInstance;
        next = null;
#if DEBUG
        JITDUMP($"Considering Memcmp [{call.TreeId:D6}] for unrolling.. ");
#endif
        assert(compiler.lookupNamedIntrinsic(call._callMethHnd) is NI_System_SpanHelpers_SequenceEqual);
        assert(call.Args.CountUserArgs() == 3);

        if (!compiler.opts.OptimizationEnabled)
        {
            JITDUMP("Optimizations aren't allowed - bail out.\n");
            return false;
        }
        if (compiler.info.compHasNextCallRetAddr)
        {
            JITDUMP("compHasNextCallRetAddr=true so we won't be able to remove the call - bail out.\n");
            return false;
        }

        var lengthArg = call.Args.GetUserArgByIndex(2);
        assert(lengthArg is not null);
        var length = lengthArg.Node;
        if (!length.Oper.IsIntegralConst)
        {
            JITDUMP("size is not a constant.\n");
            return false;
        }

        var size = (long)length.AsIntConCommon().IconValue;
        JITDUMP($"Size={size}.. ");
        if (size <= 0)
        {
            JITDUMP("Size is either 0 or too big to unroll.\n");
            return false;
        }

        var leftArg = call.Args.GetUserArgByIndex(0);
        var rightArg = call.Args.GetUserArgByIndex(1);
        assert((leftArg is not null) && (rightArg is not null));
        var left = leftArg.Node;
        var right = rightArg.Node;

        var maxUnrollSize = 16;
#if FEATURE_SIMD
#if TARGET_XARCH
        if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            maxUnrollSize = 128;
        }
        else if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            maxUnrollSize = 64;
        }
        else
#endif
        {
            maxUnrollSize = 32;
        }
#endif
        if (size > maxUnrollSize)
        {
            JITDUMP("Size is either 0 or too big to unroll.\n");
            return false;
        }

        var loadWidth = 1 << BitOperations.Log2((uint)size);
        var loadType = loadWidth switch {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_INT,
            8 => TYP_LONG,
            _ => TYP_UNDEF,
        };
#if !FEATURE_SIMD
        if (loadWidth > 8)
        {
            loadWidth = 8;
            loadType = TYP_LONG;
        }
#else
        if ((loadWidth == 16) || (maxUnrollSize == 32 && loadWidth > 8))
        {
            loadWidth = 16;
            loadType = TYP_SIMD16;
        }
#if TARGET_XARCH
        else if ((loadWidth == 32) || (maxUnrollSize == 64 && loadWidth > 16))
        {
            loadWidth = 32;
            loadType = TYP_SIMD32;
        }
        else if ((loadWidth == 64) || (maxUnrollSize == 128 && loadWidth > 32))
        {
            loadWidth = 64;
            loadType = TYP_SIMD64;
        }
#endif
#endif
        if (loadType is TYP_UNDEF)
        {
            throw new InvalidOperationException("Unsupported memory comparison load width.");
        }
        var actualLoadType = loadType.ActualType;

        GenTree result;
        if (loadWidth == size)
        {
            var leftLoad = compiler.gtNewIndir(loadType, left);
            var rightLoad = compiler.gtNewIndir(loadType, right);
            result = NewMemcmpBinaryOp(GT_EQ, TYP_INT, leftLoad, rightLoad);
            BlockRange().InsertBefore(call, leftLoad, rightLoad, result);
            next = leftLoad;
        }
        else
        {
            var foundLeftUse = BlockRange().TryGetUse(left, out var leftUse);
            var foundRightUse = BlockRange().TryGetUse(right, out var rightUse);
            assert(foundLeftUse && foundRightUse);
            var leftClone = compiler.gtNewLclvNode(left.Type.ActualType, leftUse.ReplaceWithLclVar(compiler));
            var rightClone = compiler.gtNewLclvNode(right.Type.ActualType, rightUse.ReplaceWithLclVar(compiler));
            BlockRange().InsertBefore(call, leftClone, rightClone);
            next = leftClone;

            var leftFirst = compiler.gtNewIndir(loadType, leftUse.Def());
            var rightFirst = compiler.gtNewIndir(loadType, rightUse.Def());
            var leftOffset = compiler.gtNewIconNode(TYP_I_IMPL, (nint)(size - loadWidth));
            var leftAddress = NewMemcmpBinaryOp(GT_ADD, left.Type, leftClone, leftOffset);
            var leftSecond = compiler.gtNewIndir(loadType, leftAddress);
            var rightOffset = compiler.gtNewIconNode(TYP_I_IMPL, (nint)(size - loadWidth));
            var rightAddress = NewMemcmpBinaryOp(GT_ADD, right.Type, rightClone, rightOffset);
            var rightSecond = compiler.gtNewIndir(loadType, rightAddress);

            BlockRange().InsertAfter(rightClone, leftFirst, leftOffset, leftAddress, leftSecond);
            BlockRange().InsertAfter(leftSecond, rightFirst, rightOffset, rightAddress, rightSecond);

            var leftXor = NewMemcmpBinaryOp(GT_XOR, actualLoadType, leftFirst, rightFirst);
            var rightXor = NewMemcmpBinaryOp(GT_XOR, actualLoadType, leftSecond, rightSecond);
            var combined = NewMemcmpBinaryOp(GT_OR, actualLoadType, leftXor, rightXor);
            var zero = compiler.gtNewZeroConNode(actualLoadType);
            result = NewMemcmpBinaryOp(GT_EQ, TYP_INT, combined, zero);
            BlockRange().InsertAfter(rightSecond, leftXor, rightXor, combined, zero);
            BlockRange().InsertAfter(zero, result);
        }

        JITDUMP("\nUnrolled to:\n");
        DISPTREE(result);
        if (BlockRange().TryGetUse(call, out var use))
        {
            use.ReplaceWith(result);
        }
        else
        {
            result.IsUnusedValue = true;
        }

        BlockRange().Remove(length);
        BlockRange().Remove(call);
        foreach (var arg in call.Args.Args)
        {
            if (!arg.IsUserArg)
            {
                arg.Node.IsUnusedValue = true;
            }
        }
        return true;
#endif
    }
}
