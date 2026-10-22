// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    private GenTree fgMorphFieldAddr(GenTree tree)
    {
        assert(tree.Oper is GT_FIELD_ADDR);
        var field = tree.AsFieldAddr();
        var isAddress = (tree.Flags & GTF_FLD_DEREFERENCED) == 0;
        if (field.IsInstance)
        {
            tree = fgMorphExpandInstanceField(tree);
        }
        else if (field.IsTlsStatic)
        {
            tree = fgMorphExpandTlsFieldAddr(tree);
        }
        else
        {
            JitExceptionHelpers.ThrowUnreachableException(FatalJitErrorCode.NormalStaticsAreExpectedToBeHandledInTheImporter);
        }

        GenTree result;
        if (tree.Oper.IsSimple)
        {
            result = fgMorphSmpOp(tree, out _);
            if (isAddress && (result.Oper is GT_COMMA))
            {
                result.Flags |= GTF_DONT_CSE;
            }
        }
        else
        {
            result = fgMorphTree(tree);
        }
        JITDUMP("\nFinal value of Compiler::fgMorphFieldAddr after morphing:\n");
        DISPTREE(result);
        return result;
    }

    private unsafe GenTree fgMorphIndexAddr(GenTreeIndexAddr indexAddress)
    {
        const int MaxArrayComplexity = 4;
        const int MaxIndexComplexity = 4;
        var elementType = indexAddress.ElemType;
        var elementSize = unchecked((uint)indexAddress.ElemSize);
        var elementOffset = unchecked((byte)indexAddress.ElemOffset);
        var elementClass = indexAddress.StructElemClass;
        noway_assert(!varTypeIsStruct(elementType) || (elementClass != NO_CLASS_HANDLE));

        // Retain the compact representation in minopts; spilling to locals
        // there would also introduce unconditional stack loads and stores.
        if (MinOpts)
        {
            indexAddress.Op1 = fgMorphTree(indexAddress.Arr);
            indexAddress.Op2 = fgMorphTree(indexAddress.Index);
            indexAddress.AddAllEffectsFlags(indexAddress.Arr);
            indexAddress.AddAllEffectsFlags(indexAddress.Index);
            return indexAddress;
        }
        if (Configuration.FEATURE_SIMD && varTypeIsStruct(elementType))
        {
            elementType = impNormStructType(elementClass);
        }
        if (elementType is not TYP_STRUCT)
        {
            elementClass = NO_CLASS_HANDLE;
        }

        var array = indexAddress.Arr;
        var index = indexAddress.Index;
        GenTree? arrayDefinition = null;
        GenTree? indexDefinition = null;
        GenTreeBoundsChk? boundsCheck = null;
        if (indexAddress.IsBoundsChecked)
        {
            GenTree secondArray;
            GenTree secondIndex;
            if (((array.Flags & (GTF_ASG | GTF_CALL | GTF_GLOB_REF)) != 0)
                || gtComplexityExceeds(array, MaxArrayComplexity, static _ => 1) || (array.Oper is GT_LCL_FLD)
                || ((array.Oper is GT_LCL_VAR) && lvaIsLocalImplicitlyAccessedByRef(array.AsLclVar().LclNum)))
            {
                var local = lvaGrabTemp(true, "arr expr");
                arrayDefinition = gtNewTempStore(local, array);
                array = gtNewLclvNode(lvaGetDesc(local).Type, local);
                secondArray = gtNewLclvNode(lvaGetDesc(local).Type, local);
            }
            else
            {
                var clone = gtCloneExpr(array);
                noway_assert(clone is not null);
                secondArray = clone;
            }
            if (((index.Flags & (GTF_ASG | GTF_CALL | GTF_GLOB_REF)) != 0)
                || gtComplexityExceeds(index, MaxIndexComplexity, static _ => 1) || (index.Oper is GT_LCL_FLD)
                || ((index.Oper is GT_LCL_VAR) && lvaIsLocalImplicitlyAccessedByRef(index.AsLclVar().LclNum)))
            {
                var local = lvaGrabTemp(true, "index expr");
                indexDefinition = gtNewTempStore(local, index);
                index = gtNewLclvNode(lvaGetDesc(local).Type, local);
                secondIndex = gtNewLclvNode(lvaGetDesc(local).Type, local);
            }
            else
            {
                var clone = gtCloneExpr(index);
                noway_assert(clone is not null);
                secondIndex = clone;
            }

            var boundsType = TYP_INT;
            if (Configuration.TARGET_64BIT && ((index.Type == TYP_I_IMPL)))
            {
                boundsType = TYP_I_IMPL;
            }

            GenTree length = gtNewArrLen(TYP_INT, array, indexAddress.LenOffset);
            if (boundsType is not TYP_INT)
            {
                length = gtNewCastNode(boundsType, length, true, boundsType);
            }
            boundsCheck = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL) { InxType = elementType };
            array = secondArray;
            index = secondIndex;
        }

        if (Configuration.TARGET_64BIT && ((index.Type != TYP_I_IMPL)))
        {
            if (index.Oper is GT_CNS_INT)
            {
                index.Type = TYP_I_IMPL;
            }
            else
            {
                index = gtNewCastNode(TYP_I_IMPL, index, true, TYP_I_IMPL);
            }
        }

        GenTree address;
        if (elementSize > 1)
        {
            // Lowering expects the scale to remain a constant, not a CSE local.
            var size = gtNewIconNode(TYP_I_IMPL, unchecked((nint)elementSize));
            size.Flags |= GTF_DONT_CSE;
            address = gtNewBinaryNode(GT_MUL, TYP_I_IMPL, index, size);
        }
        else
        {
            address = index;
        }

        var groupArrayWithOffset = false;
        groupArrayWithOffset =
            Configuration.TARGET_ARMARCH && !varTypeIsStruct(elementType);

        var groupArrayWithIndex = false;
        groupArrayWithIndex =
            (Configuration.TARGET_RISCV64 || (Configuration.TARGET_WASM && varTypeIsGC(array.Type))) &&
            !varTypeIsStruct(elementType);
        var pointerType = (array.Type == TYP_I_IMPL) ? TYP_I_IMPL : TYP_BYREF;
        var firstElement = gtNewIconNode(TYP_I_IMPL, elementOffset);
        if (groupArrayWithOffset)
        {
            var basePlusOffset = gtNewBinaryNode(GT_ADD, pointerType, array, firstElement);
            address = gtNewBinaryNode(GT_ADD, pointerType, basePlusOffset, address);
        }
        else if (groupArrayWithIndex)
        {
            address = gtNewBinaryNode(GT_ADD, TYP_BYREF, array, address);
            address = gtNewBinaryNode(GT_ADD, TYP_BYREF, address, firstElement);
        }
        else
        {
            // Form the full offset before creating a GC-reportable byref.
            address = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, address, firstElement);
            address = gtNewBinaryNode(GT_ADD, pointerType, array, address);
        }

        address = new GenTreeArrAddr(address, elementType, elementClass, elementOffset);
        if (indexAddress.IsNotNull)
        {
            address.Flags |= GTF_ARR_ADDR_NONNULL;
        }
        var result = address;
        if (boundsCheck is not null)
        {
            boundsCheck.HasOrderingSideEffect = true;
            address.HasOrderingSideEffect = true;
            result = gtNewBinaryNode(GT_COMMA, result.Type, boundsCheck, result);
        }
        if (indexDefinition is not null)
        {
            result = gtNewBinaryNode(GT_COMMA, result.Type, indexDefinition, result);
        }
        if (arrayDefinition is not null)
        {
            result = gtNewBinaryNode(GT_COMMA, result.Type, arrayDefinition, result);
        }

        JITDUMP("fgMorphIndexAddr (before remorph):\n");
        DISPTREE(result);
        result = fgMorphTree(result);
        JITDUMP("fgMorphIndexAddr (after remorph):\n");
        DISPTREE(result);
        return result;
    }
}
