// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree NormalizeIndexToNativeSized(GenTree index)
    {
        if (index.Type.ActualType == TYP_I_IMPL)
        {
            return index;
        }

        if (index.Oper.IsConst)
        {
            index.Type = TYP_I_IMPL;
            return index;
        }

        var cast = CompilerInstance.gtNewCastNode(TYP_I_IMPL, index, true, TYP_I_IMPL);
        BlockRange().InsertAfter(index, cast);
        return cast;
    }

    private unsafe GenTree? LowerHWIntrinsicGetElement(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var intrinsicId = node.HWIntrinsicId;
        var baseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        assert(intrinsicId is NI_Vector_GetElement);
        assert(!varTypeIsSimd(node.Type));
        assert(varTypeIsArithmetic(baseType));
        assert(simdSize != 0);

        var operand = node.GetOp(1);
        var index = node.GetOp(2);
        if (index.IsIntegralConst(0))
        {
            BlockRange().Remove(index);
            node.ResetHWIntrinsicId(NI_Vector_ToScalar, operand);
            return LowerNode(node);
        }

        index = NormalizeIndexToNativeSized(index);
        node.SetOp(2, index);
        var elementSize = (uint)baseType.Size;
        var count = simdSize / elementSize;

        if (operand.Oper is GT_IND)
        {
            GenTree? newBase;
            GenTree? newIndex;
            byte newScale;
            int newOffset;
            var indirection = operand.AsIndir();
            var address = indirection.Addr;
            var canMoveIndirection = IsInvariantInRange(indirection, node);
            if (!canMoveIndirection)
            {
                // Preserve address evaluation and the original fault before moving the scalar load.
                if (!address.IsInvariant && !address.Oper.IsLocal)
                {
                    address.IsContained = false;
                    var addressUse = new LIR.Use(BlockRange(), ref indirection.AddrRef, indirection);
                    _ = addressUse.ReplaceWithLclVar(CompilerInstance);
                    address = indirection.Addr;
                }

                if (indirection.MayThrow(CompilerInstance))
                {
                    var clone = CompilerInstance.gtCloneExpr(address);
                    assert(clone is not null);
                    var nullcheck = CompilerInstance.gtNewNullCheck(clone);
                    BlockRange().InsertBefore(indirection, clone, nullcheck);
                    _ = LowerNode(nullcheck);
                    indirection.Flags |= GTF_IND_NONFAULTING;
                }
                indirection.Flags &= ~GTF_EXCEPT;
            }

            if (address.Oper is GT_LEA)
            {
                var addressMode = address.AsAddrMode();
                newBase = addressMode.BaseAddress;
                newIndex = addressMode.Index;
                newScale = addressMode.Scale;
                newOffset = addressMode.Offset;
                if (index.Oper.IsConst && (newOffset < int.MaxValue - simdSize))
                {
                    BlockRange().Remove(addressMode);
                    BlockRange().Remove(index);
                    var offset = (unchecked((byte)index.AsIntCon().IconValue) % count) * elementSize;
                    newOffset += (int)offset;
                }
                else if (newIndex is null)
                {
                    BlockRange().Remove(addressMode);
                    newIndex = index;
                    newScale = (byte)elementSize;
                }
                else if (addressMode.Scale == elementSize)
                {
                    BlockRange().Remove(addressMode);
                    newIndex = CompilerInstance.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, newIndex, index);
                    BlockRange().InsertBefore(node, newIndex);
                    _ = LowerNode(newIndex);
                }
                else
                {
                    newBase = addressMode;
                    newIndex = index;
                    newScale = (byte)elementSize;
                    newOffset = 0;
                }
            }
            else if (index.Oper.IsConst)
            {
                BlockRange().Remove(index);
                newBase = address;
                newIndex = null;
                newScale = 0;
                newOffset = (int)((unchecked((byte)index.AsIntCon().IconValue) % count) * elementSize);
            }
            else
            {
                newBase = address;
                newIndex = index;
                newScale = (byte)elementSize;
                newOffset = 0;
            }

            newBase?.IsContained = false;
            newIndex?.IsContained = false;

            var newAddress = new GenTreeAddrMode(address.Type, newBase, newIndex, newScale, newOffset);
            BlockRange().InsertBefore(node, newAddress);
            var load = CompilerInstance.gtNewIndir(node.SimdBaseTypeAsVarType, newAddress,
                indirection.Flags & GTF_IND_FLAGS);
            BlockRange().InsertBefore(node, load);
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(load);
            }
            else
            {
                load.IsUnusedValue = true;
            }

            BlockRange().Remove(operand);
            BlockRange().Remove(node);
            assert(newAddress.Next == load);
            return LowerNode(newAddress);
        }

        if (!index.Oper.IsConst)
        {
            ContainCheckHWIntrinsic(node);
            return node.Next;
        }

        // Import supplied bounds checks. Keep native imm8 truncation and masking
        // even for indices whose checks will throw at execution time.
        var immediate = unchecked((byte)index.AsIntCon().IconValue) % count;
        var simd16Count = 16 / elementSize;
        var simd16Index = immediate / simd16Count;
        assert(immediate < count);

        if (IsContainableMemoryOp(operand))
        {
            if (operand.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                var local = operand.AsLclVarCommon();
                var offset = local.LclOffs + (immediate * elementSize);
                ref var descriptor = ref CompilerInstance.lvaGetDesc(local.LclNum);
                if (descriptor.lvDoNotEnregister && (offset <= ushort.MaxValue) &&
                    ((offset + elementSize) <= descriptor.lvValueSize.ExactSize))
                {
                    var load = CompilerInstance.gtNewLclFldNode(node.SimdBaseTypeAsVarType, local.LclNum, (ushort)offset);
                    BlockRange().InsertBefore(node, load);
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        use.ReplaceWith(load);
                    }
                    else
                    {
                        load.IsUnusedValue = true;
                    }

                    BlockRange().Remove(operand);
                    BlockRange().Remove(index);
                    BlockRange().Remove(node);
                    return LowerNode(load);
                }
            }

            if (IsSafeToContainMem(node, operand))
            {
                index.AsIntCon().IconValue = (int)immediate;
                ContainCheckHWIntrinsic(node);
                return node.Next;
            }
        }

        BlockRange().Remove(index);
        if (simdSize == 64)
        {
            assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX512));
            GenTree lower;
            if (simd16Index == 0)
            {
                lower = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_GetLower128, baseType, simdSize, operand);
            }
            else
            {
                assert(simd16Index is >= 1 and <= 3);
                immediate -= simd16Index * simd16Count;
                var part = CompilerInstance.gtNewIconNode(TYP_INT, (int)simd16Index);
                BlockRange().InsertBefore(node, part);
                _ = LowerNode(part);
                lower = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_ExtractVector128, baseType, simdSize, operand, part);
            }
            BlockRange().InsertBefore(node, lower);
            _ = LowerNode(lower);
            operand = lower;
        }
        else if (simdSize == 32)
        {
            assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX));
            GenTree lower;
            if (simd16Index == 0)
            {
                lower = CompilerInstance.gtNewSimdGetLowerNode(TYP_SIMD16, operand, baseType, simdSize);
            }
            else
            {
                assert(simd16Index == 1);
                immediate -= count / 2;
                lower = CompilerInstance.gtNewSimdGetUpperNode(TYP_SIMD16, operand, baseType, simdSize);
            }
            BlockRange().InsertBefore(node, lower);
            _ = LowerNode(lower);
            operand = lower;
        }

        if (immediate == 0)
        {
            node.SimdSize = 16;
            node.ResetHWIntrinsicId(NI_Vector_ToScalar, operand);
            return LowerNode(node);
        }

        index = CompilerInstance.gtNewIconNode(TYP_INT, (int)immediate);
        BlockRange().InsertBefore(node, index);
        NamedIntrinsic resultIntrinsic;
        switch (baseType)
        {
            case TYP_LONG:
            case TYP_ULONG:
            {
                resultIntrinsic = NI_X86Base_X64_Extract;
                break;
            }

            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                resultIntrinsic = NI_Vector_GetElement;
                index.Type = TYP_I_IMPL;
                break;
            }

            case TYP_BYTE:
            case TYP_UBYTE:
            case TYP_INT:
            case TYP_UINT:
            case TYP_SHORT:
            case TYP_USHORT:
            {
                resultIntrinsic = NI_X86Base_Extract;
                break;
            }

            default:
            {
                throw new FatalJitException("Vector element extraction requires an arithmetic base type.");
            }
        }

        node.SimdSize = 16;
        node.ResetHWIntrinsicId(resultIntrinsic, operand, index);
        var next = node.Next;
        if (node.HWIntrinsicId != intrinsicId)
        {
            next = LowerNode(node);
        }
        else
        {
            ContainCheckHWIntrinsic(node);
        }

        if (baseType is TYP_BYTE or TYP_SHORT)
        {
            // Extract instructions zero-extend; signed small elements require an explicit cast.
            var foundUse = BlockRange().TryGetUse(node, out var use);
            var cast = CompilerInstance.gtNewCastNode(TYP_INT, node, false, baseType);
            BlockRange().InsertAfter(node, cast);
            if (foundUse)
            {
                use.ReplaceWith(cast);
            }
            else
            {
                node.IsUnusedValue = false;
                cast.IsUnusedValue = true;
            }
            next = LowerNode(cast);
        }

        return next;
#else
        throw new NotImplementedException("Non-xarch vector element lowering is not ported.");
#endif
    }
}
