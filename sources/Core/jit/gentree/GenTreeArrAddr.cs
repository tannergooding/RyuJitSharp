// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Carries information about the array type from morph to VN.</summary>
/// <remarks>This node is just a wrapper (similar to GenTreeBox), the real address expression is contained in its first operand.</remarks>
public sealed class GenTreeArrAddr : GenTreeUnOp
{
    private readonly unsafe CORINFO_CLASS_HANDLE _elemClassHandle;
    private readonly var_types _elemType;
    private readonly byte _firstElemOffset;

    public unsafe GenTreeArrAddr(GenTree addr, var_types elemType, CORINFO_CLASS_HANDLE elemClassHandle, byte firstElemOffset)
        : base(GT_ARR_ADDR, addr.Type, addr)
    {
        _elemClassHandle = elemClassHandle;
        _elemType = elemType;
        _firstElemOffset = firstElemOffset;

        assert(addr.Type is TYP_BYREF or TYP_I_IMPL);
        assert(((elemType is TYP_STRUCT) && (elemClassHandle != NO_CLASS_HANDLE)) || (elemClassHandle == NO_CLASS_HANDLE));
    }

    public GenTree Addr => Op1;

    /// <summary>The array element class. Currently only used for arrays of TYP_STRUCT.</summary>
    public unsafe CORINFO_CLASS_HANDLE ElemClassHandle => _elemClassHandle;

    /// <summary>The normalized (TYP_SIMD != TYP_STRUCT) array element type.</summary>
    public var_types ElemType => _elemType;

    /// <summary>Offset to the first element of the array.</summary>
    public byte FirstElemOffset => _firstElemOffset;

    /// <summary>Recover the array and index VN. Failure clears the array and leaves the index VN unchanged.</summary>
    public unsafe void ParseArrayAddress(Compiler compiler, out GenTree? array, ref ValueNum indexVN)
    {
        array = null;
        var scaledIndexVN = ValueNumStore.NoVN;
        target_ssize_t offset = 0;
        ParseArrayAddressWork(Addr, compiler, 1, ref array, ref scaledIndexVN, ref offset);
        if (array is null)
        {
            return;
        }

        target_ssize_t firstElemOffset = FirstElemOffset;
        assert(firstElemOffset > 0);
        if (offset < firstElemOffset)
        {
            array = null;
            return;
        }

        var elemSizeUnsigned = ElemType is TYP_STRUCT
            ? compiler.typGetObjLayout(ElemClassHandle).Size
            : (uint)ElemType.Size;
        assert((ulong)elemSizeUnsigned <= (ulong)target_ssize_t.MaxValue);
        var elemSize = (target_ssize_t)elemSizeUnsigned;
        var constIndexOffset = offset - firstElemOffset;
        assert((constIndexOffset % elemSize) == 0);
        var constIndex = constIndexOffset / elemSize;
        assert(compiler.vnStore is not null);
        var store = compiler.vnStore;

        if (scaledIndexVN == ValueNumStore.NoVN)
        {
            indexVN = store.VNForPtrSizeIntCon(constIndex);
        }
        else if (store.IsVNConstant(scaledIndexVN))
        {
            var index = store.CoercedConstantValue<target_ssize_t>(scaledIndexVN);
            noway_assert((elemSize > 0) && ((index % elemSize) == 0));
            indexVN = store.VNForPtrSizeIntCon(unchecked((index / elemSize) + constIndex));
        }
        else
        {
            // The parsed VN is a byte offset. Cancel an element-size multiply
            // where possible, otherwise divide before adding the constant index.
            var canFoldDiv = false;
            var mulOp0 = ValueNumStore.NoVN;
            var mulOp1 = ValueNumStore.NoVN;
            if (store.IsVNBinFunc(scaledIndexVN, VNF_MUL, ref mulOp0, ref mulOp1))
            {
                var elemSizeVN = store.VNForLongCon(elemSize);
                if (mulOp1 == elemSizeVN)
                {
                    indexVN = mulOp0;
                    canFoldDiv = true;
                }
                else if (mulOp0 == elemSizeVN)
                {
                    indexVN = mulOp1;
                    canFoldDiv = true;
                }
            }

            if (!canFoldDiv)
            {
                var elemSizeVN = store.VNForPtrSizeIntCon(elemSize);
                indexVN = store.VNForFunc(TYP_I_IMPL, VNF_DIV, scaledIndexVN, elemSizeVN);
            }

            if (constIndex != 0)
            {
                var constIndexVN = store.VNForPtrSizeIntCon(constIndex);
                indexVN = store.VNForFunc(TYP_I_IMPL, VNF_ADD, indexVN, constIndexVN);
            }
        }
    }

    private static unsafe void ParseArrayAddressWork(GenTree tree, Compiler compiler, target_ssize_t inputMul,
        ref GenTree? array, ref ValueNum indexVN, ref target_ssize_t offset)
    {
        assert(compiler.vnStore is not null);
        var store = compiler.vnStore;
        var vn = store.VNNormalValue(tree._vnPair.Liberal);
        VNFuncApp app = default;
        var treeIsArrayRef = false;

        if ((tree.Type is TYP_REF) || store.IsVNNewArr(vn, ref app))
        {
            assert(array is null);
            array = tree;
            assert(inputMul == 1);
            treeIsArrayRef = true;
        }
        else if ((tree.Oper is GT_LCL_VAR) && (tree.Type is TYP_BYREF or TYP_I_IMPL))
        {
            var handle = compiler.lvaGetDesc(tree.AsLclVar().LclNum).lvClassHnd;
            if (handle != NO_CLASS_HANDLE)
            {
                var attributes = compiler.info.compCompHnd->getClassAttribs(handle);
                treeIsArrayRef = (attributes & CORINFO_FLG_ARRAY) != 0;
                if (treeIsArrayRef)
                {
                    assert(array is null);
                    array = tree;
                    assert(inputMul == 1);
                }
            }
        }

        if (!treeIsArrayRef)
        {
            switch (tree.Oper)
            {
                case GT_CNS_INT:
                {
                    assert(!tree.AsIntCon().ImmedValNeedsReloc(compiler));
                    offset = unchecked(offset + (inputMul * (target_ssize_t)tree.AsIntCon().IconValue));
                    return;
                }

                case GT_ADD:
                case GT_SUB:
                {
                    var op = tree.AsOp();
                    ParseArrayAddressWork(op.Op1, compiler, inputMul, ref array, ref indexVN, ref offset);
                    if (tree.Oper is GT_SUB)
                    {
                        inputMul = unchecked(-inputMul);
                    }
                    ParseArrayAddressWork(op.Op2, compiler, inputMul, ref array, ref indexVN, ref offset);
                    return;
                }

                case GT_MUL:
                {
                    target_ssize_t subMul = 0;
                    GenTree? nonConst = null;
                    var op = tree.AsOp();
                    if (op.Op1.Oper.IsCnsIntOrI)
                    {
                        // Prefer the non-field constant as multiplier so a field
                        // offset remains in the parsed constant contribution.
                        if ((op.Op2.Oper is GT_CNS_INT) && (op.Op2.AsIntCon().FieldSeq is null))
                        {
                            assert(!op.Op2.AsIntCon().ImmedValNeedsReloc(compiler));
                            subMul = unchecked((target_ssize_t)op.Op2.AsIntConCommon().IconValue);
                            nonConst = op.Op1;
                        }
                        else
                        {
                            assert(!op.Op1.AsIntCon().ImmedValNeedsReloc(compiler));
                            subMul = unchecked((target_ssize_t)op.Op1.AsIntConCommon().IconValue);
                            nonConst = op.Op2;
                        }
                    }
                    else if (op.Op2.Oper.IsCnsIntOrI)
                    {
                        assert(!op.Op2.AsIntCon().ImmedValNeedsReloc(compiler));
                        subMul = unchecked((target_ssize_t)op.Op2.AsIntConCommon().IconValue);
                        nonConst = op.Op1;
                    }

                    if (nonConst is not null)
                    {
                        ParseArrayAddressWork(nonConst, compiler, unchecked(inputMul * subMul),
                            ref array, ref indexVN, ref offset);
                        return;
                    }
                    break;
                }

                case GT_LSH:
                {
                    var op = tree.AsOp();
                    if (op.Op2.Oper.IsCnsIntOrI)
                    {
                        assert(!op.Op2.AsIntCon().ImmedValNeedsReloc(compiler));
                        var shift = unchecked((target_ssize_t)op.Op2.AsIntConCommon().IconValue);
                        var subMul = (target_ssize_t)1 << unchecked((int)shift);
                        ParseArrayAddressWork(op.Op1, compiler, unchecked(inputMul * subMul),
                            ref array, ref indexVN, ref offset);
                        return;
                    }
                    break;
                }

                case GT_COMMA:
                {
                    var op = tree.AsOp();
                    if ((op.Op1.Oper is GT_BOUNDS_CHECK) || op.Op1.IsNothingNode)
                    {
                        ParseArrayAddressWork(op.Op2, compiler, inputMul, ref array, ref indexVN, ref offset);
                        return;
                    }
                    break;
                }
            }

            // Unparsed terms contribute only to the variable byte-offset VN.
            if (inputMul != 1)
            {
                var multiplierVN = store.VNForLongCon(inputMul);
                vn = store.VNForFunc(tree.Type, VNF_MUL, multiplierVN, vn);
            }
            indexVN = indexVN == ValueNumStore.NoVN
                ? vn
                : store.VNForFunc(tree.Type, VNF_ADD, indexVN, vn);
        }
    }
}
