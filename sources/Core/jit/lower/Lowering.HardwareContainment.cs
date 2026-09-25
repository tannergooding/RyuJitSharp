// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private void ContainHWIntrinsicOperand(GenTreeHWIntrinsic parentNode, GenTree childNode)
    {
        if (childNode is GenTreeHWIntrinsic intrinsic &&
            intrinsic.HWIntrinsicId is NI_Vector_CreateScalar or NI_Vector_CreateScalarUnsafe)
        {
            var scalar = intrinsic.GetOp(1);

            var decomposedLong = false;
#if TARGET_X86
            // A decomposed long still needs the vector move represented by CreateScalar.
            decomposedLong = scalar.Oper is GT_LONG;
#endif
            if (!decomposedLong)
            {
                var foundUse = BlockRange().TryGetUse(childNode, out var use);
                assert(foundUse);

                use.ReplaceWith(scalar);
                BlockRange().Remove(childNode);
                return;
            }
        }

        MakeSrcContained(parentNode, childNode);
    }

    private void ContainCheckHWIntrinsicAddr(GenTreeHWIntrinsic node, GenTree addr, uint size)
    {
        assert(addr.Type.ActualType is TYP_I_IMPL or TYP_BYREF);

        if (((addr.Oper is GT_LCL_ADDR) && IsContainableLclAddr(addr.AsLclFld(), size)) ||
            (addr.Oper.IsCnsIntOrI && addr.AsIntConCommon().FitsInAddrBase(CompilerInstance)))
        {
            MakeSrcContained(node, addr);
        }
        else
        {
            var foundOperand = false;
            for (var i = 1; i <= node.Operands.Length; i++)
            {
                if (ReferenceEquals(node.GetOp(i), addr))
                {
                    foundOperand = true;
                    _ = TryCreateAddrMode(ref node.GetOpRef(i), true, node);
                    addr = node.GetOp(i);
                    break;
                }
            }
            assert(foundOperand);

            if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
            {
                MakeSrcContained(node, addr);
            }
        }
    }
#endif
}
