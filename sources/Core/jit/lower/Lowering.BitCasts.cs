// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool TryRemoveBitCast(GenTreeUnOp node)
    {
        if (CompilerInstance.opts.OptimizationDisabled)
        {
            return false;
        }

        var operand = node.Op1;
        assert(node.Type.Size == operand.Type.ActualType.Size);
        var changed = false;
        var isConstant = operand.Oper is GT_CNS_INT or GT_CNS_DBL;
#if FEATURE_SIMD
        isConstant |= operand.Oper is GT_CNS_VEC;
#endif
        if (isConstant)
        {
            Span<byte> bits = stackalloc byte[Unsafe.SizeOf<simd_t>()];
            assert(bits.Length >= operand.Type.ActualType.Size);
            if (operand.Oper is GT_CNS_INT)
            {
                var value = operand.AsIntCon().IconValue;
                assert(IntPtr.Size >= operand.Type.ActualType.Size);
                MemoryMarshal.Write(bits, in value);
            }
#if FEATURE_SIMD
            else if (operand.Oper is GT_CNS_VEC)
            {
                MemoryMarshal.Write(bits, in operand.AsVecCon().SimdVal);
            }
#endif
            else if (operand.Type is TYP_FLOAT)
            {
                var value = (float)operand.AsDblCon().DconVal;
                MemoryMarshal.Write(bits, in value);
            }
            else
            {
                var value = operand.AsDblCon().DconVal;
                MemoryMarshal.Write(bits, in value);
            }

            var constant = CompilerInstance.gtNewGenericCon(node.Type, bits[..node.Type.Size]);
            BlockRange().InsertAfter(operand, constant);
            BlockRange().Remove(operand);
            node.Op1 = operand = constant;
            changed = true;
        }
        else if ((operand.Oper is GT_LCL_FLD or GT_IND) && (operand.Type.Size == node.Type.Size))
        {
            operand.Type = node.Type;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(operand);
        }
        else
        {
            operand.IsUnusedValue = true;
        }

        BlockRange().Remove(node);
        return true;
    }

    private void ContainCheckBitCast(GenTreeUnOp node)
    {
        var operand = node.Op1;
        if ((operand.Oper is GT_LCL_VAR) && (operand.Type.Size == node.Type.Size))
        {
            if (IsContainableMemoryOp(operand) && IsSafeToContainMem(node, operand))
            {
                MakeSrcContained(node, operand);
            }
            else if (IsSafeToMarkRegOptional(node, operand))
            {
                MakeSrcRegOptional(node, operand);
            }
        }
    }
}
