// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Numerics;
using static RyuJitSharp.GenTree;

namespace RyuJitSharp;

public partial class Compiler
{
    private GenTree fgOptimizeCommutativeArithmetic(GenTreeOp tree)
    {
        assert(tree.Oper is GT_ADD or GT_MUL or GT_OR or GT_XOR or GT_AND);
        assert(!tree.HasOverflowCheckEx);
        fgPushConstantsRight(tree);
        if (fgOperIsBitwiseRotationRoot(tree.Oper))
        {
            var rotation = fgRecognizeAndMorphBitwiseRotation(tree);
            if (rotation is not null)
            {
                return rotation;
            }
        }

        if (varTypeIsIntegral(tree.Type))
        {
            var oldOperation = tree.Oper;
            var optimized = fgMorphCommutative(tree);
            if (optimized is not null)
            {
                if (optimized.Oper != oldOperation)
                {
                    return optimized;
                }
                tree = optimized;
            }
        }

        var result = tree.Oper switch {
            GT_ADD => fgOptimizeAddition(tree),
            GT_MUL => fgOptimizeMultiply(tree),
            GT_AND => fgOptimizeBitwiseAnd(tree),
            GT_XOR => fgOptimizeBitwiseXor(tree),
            _ => null,
        };
        return result ?? tree;
    }

    private GenTree? fgOptimizeAddition(GenTreeOp add)
    {
        assert((add.Oper is GT_ADD) && !add.HasOverflowCheck);
        var op1 = add.Op1;
        var op2 = add.Op2;

        // Reassociate constants only during global morph, where the new x+y
        // does not need a VN. Do not create an intermediate out-of-object byref.
        if ((op1.Oper is GT_ADD) && (op2.Oper is GT_ADD) && !op1.HasOverflowCheck && !op2.HasOverflowCheck
            && op1.AsOp().Op2.Oper.IsCnsIntOrI && op2.AsOp().Op2.Oper.IsCnsIntOrI
            && !varTypeIsGC(op1.AsOp().Op1.Type) && !varTypeIsGC(op2.AsOp().Op1.Type) && fgGlobalMorph)
        {
            var firstAdd = op1.AsOp();
            var secondAdd = op2.AsOp();
            var firstConstant = firstAdd.Op2.AsIntCon();
            firstAdd.Op2 = secondAdd.Op1;
            firstAdd.SetAllEffectsFlags(firstAdd.Op1, firstAdd.Op2);
            secondAdd.Op1 = firstConstant;
            add.Op2 = gtFoldExprConst(add.Op2);
            op2 = add.Op2;
        }

        if (op2.IsIntegralConst(0) && (add.Type.ActualType == op1.Type.ActualType))
        {
            // Retain annotated zero offsets for value numbering.
            if (!op2.Oper.IsCnsIntOrI || (op2.AsIntCon().FieldSeq is null))
            {
                return op1;
            }
            add.Flags |= GTF_DONT_CSE;
        }

        if (opts.OptimizationEnabled)
        {
            if ((op1.Oper is GT_LCL_ADDR) && op2.Oper.IsCnsIntOrI)
            {
                var address = op1.AsLclFld();
                var constant = op2.AsIntCon().IconValue;
                // Validate the accumulated offset, not just this addend.
                if (constant is >= 0 and <= ushort.MaxValue)
                {
                    var offset = address.LclOffs + (int)constant;
                    if ((offset <= ushort.MaxValue) && IsValidLclAddr(address.LclNum, offset))
                    {
                        address.SetOper(GT_LCL_ADDR);
                        address.LclOffs = (ushort)offset;
                        assert(lvaGetDesc(address.LclNum).lvDoNotEnregister);
                        address._vnPair = add._vnPair;
                        return address;
                    }
                }
            }

            if ((op1.Oper is GT_NEG) && (op2.Oper is not GT_NEG))
            {
                if (op2.Oper.IsIntegralConst)
                {
                    var range = IntegralRange.ForNode(op1.AsUnOp().Op1, this);
                    var constant = unchecked((ulong)op2.AsIntConCommon().IntegralValue);
                    var lower = unchecked((ulong)IntegralRange.SymbolicToRealValue(range.LowerBound));
                    var upper = unchecked((ulong)IntegralRange.SymbolicToRealValue(range.UpperBound));

                    // OR of every value in the range. At every possible set bit,
                    // the constant needs a one to make subtraction borrow-free.
                    var knownBits = lower == upper ? 0 : ulong.MaxValue >> BitOperations.LeadingZeroCount(lower ^ upper);
                    knownBits |= lower;
                    knownBits &= (1UL << ((add.Type.Size * BITS_PER_BYTE) - 1)) - 1;
                    if ((constant & knownBits) == knownBits)
                    {
                        add.SetOper(GT_XOR, PRESERVE_VN);
                        add.Op1 = op1.AsUnOp().Op1;
                        return fgMorphTree(add);
                    }
                }

                // Keep constants on the right for canonicalization.
                if (!op2.Oper.IsIntegralConst && gtCanSwapOrder(op1, op2))
                {
                    add.SetOper(GT_SUB);
                    add.Op1 = op2;
                    add.Op2 = op1.AsUnOp().Op1;
                    return add;
                }
            }

            if ((op1.Oper is not GT_NEG) && (op2.Oper is GT_NEG))
            {
                add.SetOper(GT_SUB);
                add.Op2 = op2.AsUnOp().Op1;
                return add;
            }
            if ((op1.Oper is GT_NOT) && op2.IsIntegralConst(1))
            {
                op1.SetOper(GT_NEG);
                op1._vnPair = add._vnPair;
                return op1;
            }
        }
        return null;
    }

    private GenTree fgMorphSmpOpOptional(GenTreeUnOp tree, ref bool assertionPropDone)
    {
        var operation = tree.Oper;
        var op1 = tree.Op1;
        var op2 = operation.IsBinary ? tree.AsOp().Op2 : null;
        var type = tree.Type;
        if (fgGlobalMorph && operation.IsCommutative)
        {
            assert(op2 is not null);
            if (tree.IsReverseOp)
            {
                tree.Op1 = op2;
                tree.AsOp().Op2 = op1;
                op2 = op1;
                op1 = tree.Op1;
                tree.IsReverseOp = false;
            }
            if ((operation == op2.Oper) && !varTypeIsFloating(type))
            {
                fgMoveOpsLeft(tree.AsOp());
                op1 = tree.Op1;
                op2 = tree.AsOp().Op2;
            }
        }

#if REARRANGE_ADDS
        // ((x + constant) + y) -> ((x + y) + constant), without an
        // intermediate byref that a GC could fail to update.
        if (fgGlobalMorph && (operation is GT_ADD) && !tree.HasOverflowCheck && (op1.Oper is GT_ADD)
            && !op1.HasOverflowCheck && varTypeIsIntegral(type))
        {
            assert(op2 is not null);
            var first = op1.AsOp().Op1;
            var second = op1.AsOp().Op2;
            if (!op2.Oper.IsConst && second.Oper.IsConst && !varTypeIsGC(first.Type) && !varTypeIsGC(op2.Type))
            {
                tree.AsOp().Op2 = second;
                op1.AsOp().Op2 = op2;
                op1.Flags |= op2.Flags & GTF_ALL_EFFECT;
                op2 = tree.AsOp().Op2;
            }
        }
#endif

        switch (operation)
        {
            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                if (varTypeIsStruct(type) && !tree.IsPhiDefn)
                {
                    // Block morph owns its assertion kill/gen ordering.
                    assertionPropDone = true;
                    return tree.IsCopyBlkOp ? fgMorphCopyBlock(tree) : fgMorphInitBlock(tree);
                }
                if (operation is GT_STOREIND)
                {
                    assert(op2 is not null);
                    if ((type is TYP_LONG) || ((op2.Flags & (GTF_ASG | GTF_CALL)) != 0))
                    {
                        break;
                    }
                    if ((op2.Oper is GT_CAST) && !op2.HasOverflowCheck)
                    {
                        var cast = op2.AsCast();
                        var sourceType = cast.CastOp.Type;
                        if ((cast.CastType.Size >= type.Size) && (type <= TYP_INT) && (sourceType <= TYP_INT))
                        {
                            tree.AsOp().Op2 = cast.CastOp;
                        }
                    }
                }
                break;
            }

            case GT_MUL:
            {
                assert(op2 is not null);
                if ((op2.Oper is GT_CNS_INT) && (op1.Oper is GT_ADD))
                {
                    var constant = op1.AsOp().Op2;
                    if (constant.Oper.IsCnsIntOrI && (op2.ScaleIndexMul != 0))
                    {
                        if (tree.HasOverflowCheck || op1.HasOverflowCheck)
                        {
                            break;
                        }
                        var multiplier = op2.AsIntCon().IconValue;
                        var addend = constant.AsIntCon().IconValue;
                        tree.SetOper(GT_ADD, PRESERVE_VN);
                        tree.Flags &= GTF_COMMON_MASK;
                        op2.AsIntCon().SetValueTruncating(unchecked(addend * multiplier));
                        fgUpdateConstTreeValueNumber(op2);
                        op1.SetOper(GT_MUL);
                        op1.Flags &= GTF_COMMON_MASK;
                        constant.AsIntCon().IconValue = multiplier;
                        fgValueNumberTreeConst(constant);
                    }
                }
                break;
            }

            case GT_DIV:
            {
                assert(op2 is not null);
                if (op2.IsIntegralConst(1))
                {
                    return op1;
                }
                tree.AsOp().CheckDivideByConstOptimized(this);
                break;
            }

            case GT_UDIV:
            case GT_UMOD:
            {
                tree.AsOp().CheckDivideByConstOptimized(this);
                break;
            }

            case GT_LSH:
            {
                assert(op2 is not null);
                if (op2.Oper.IsCnsIntOrI && (op1.Oper is GT_ADD) && !op1.HasOverflowCheck)
                {
                    var constant = op1.AsOp().Op2;
                    if (constant.Oper.IsCnsIntOrI && (op2.ScaleIndexShf != 0))
                    {
                        var shift = op2.AsIntConCommon().IconValue;
                        var addend = constant.AsIntConCommon().IconValue;
                        tree.SetOper(GT_ADD, PRESERVE_VN);
                        tree.Flags &= GTF_COMMON_MASK;
                        // Reuse the shift amount as a constant of the result type.
                        op2.Type = op1.Type;
                        op2.AsIntConCommon().SetValueTruncating(unchecked(addend << (int)shift));
                        fgUpdateConstTreeValueNumber(op2);
                        op1.SetOper(GT_LSH);
                        op1.Flags &= GTF_COMMON_MASK;
                        constant.AsIntConCommon().IconValue = shift;
                        fgUpdateConstTreeValueNumber(constant);
                    }
                }
                break;
            }

            case GT_INIT_VAL:
            {
                // Bare zero can carry VNForZero; nonzero init values retain
                // the low-byte replication semantics of INIT_VAL.
                if (op1.IsIntegralConst(0))
                {
                    return op1;
                }
                break;
            }
        }
        return tree;
    }
}
