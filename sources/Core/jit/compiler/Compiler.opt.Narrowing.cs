// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Probe whether a tree can use narrower integer precision, or apply a previously successful probe.</summary>
    public bool optNarrowTree(ref GenTree tree, var_types sourceType, var_types destinationType,
        ValueNumPair narrowedValueNumbers, bool doIt)
    {
        noway_assert(tree.Type.ActualType == sourceType.ActualType);
        noway_assert(varTypeIsIntegral(sourceType));
        noway_assert(varTypeIsIntegral(destinationType));
        var sourceSize = sourceType.Size;
        var destinationSize = destinationType.Size;
        if (destinationSize >= sourceSize)
        {
            return false;
        }

        var operation = tree.Oper;
        var noValueNumbers = new ValueNumPair();
        switch (operation)
        {
#if !TARGET_64BIT
            case GT_CNS_LNG:
            {
                var value = tree.AsIntConCommon().IntegralValue;
                long mask = destinationType switch {
                    TYP_BYTE => 0x7F,
                    TYP_UBYTE => 0xFF,
                    TYP_SHORT => 0x7FFF,
                    TYP_USHORT => 0xFFFF,
                    TYP_INT => 0x7FFFFFFF,
                    TYP_UINT => 0xFFFFFFFF,
                    _ => 0,
                };
                if ((mask == 0) || ((value & mask) != value))
                {
                    return false;
                }

                if (doIt)
                {
                    tree = tree.BashToZeroConst(TYP_INT);
                    tree.AsIntCon().IconValue = unchecked((int)value);
                    fgUpdateConstTreeValueNumber(tree);
                }

                return true;
            }
#endif
            case GT_CNS_INT:
            {
                if (tree.AsIntCon().ImmedValNeedsReloc(this))
                {
                    return false;
                }

                var value = tree.AsIntCon().IconValue;
                long mask = destinationType switch {
                    TYP_BYTE => 0x7F,
                    TYP_UBYTE => 0xFF,
                    TYP_SHORT => 0x7FFF,
                    TYP_USHORT => 0xFFFF,
#if TARGET_64BIT
                    TYP_INT => 0x7FFFFFFF,
                    TYP_UINT => 0xFFFFFFFF,
#endif
                    _ => 0,
                };
                if ((mask == 0) || ((value & mask) != value))
                {
                    return false;
                }

#if TARGET_64BIT
                if (doIt)
                {
                    tree.Type = TYP_INT;
                    tree.AsIntCon().IconValue = unchecked((int)value);
                    fgUpdateConstTreeValueNumber(tree);
                }
#endif
                return true;
            }

            case GT_LCL_VAR:
            {
                // Whole locals can only narrow from long to int.
                if (destinationSize != sizeof(int))
                {
                    noway_assert(!doIt);
                    return false;
                }

                goto case GT_IND;
            }

            case GT_LCL_FLD:
            case GT_IND:
            {
                if ((destinationSize > tree.Type.Size) &&
                    varTypeIsUnsigned(destinationType) && !varTypeIsUnsigned(tree.Type))
                {
                    return false;
                }

                if (doIt && (destinationSize <= tree.Type.Size))
                {
                    if (!varTypeIsSmall(destinationType))
                    {
                        destinationType = varTypeToSigned(destinationType);
                    }

                    tree.Type = destinationType;
                    tree._vnPair = narrowedValueNumbers;
                }

                return true;
            }

            case GT_AND:
            case GT_ADD:
            case GT_MUL:
            case GT_OR:
            case GT_XOR:
            {
                var binary = tree.AsOp();
                if (operation is GT_ADD or GT_MUL)
                {
                    if (tree.HasOverflowCheck || varTypeIsSmall(destinationType))
                    {
                        noway_assert(!doIt);
                        return false;
                    }
                }

                noway_assert(tree.Type.ActualType == binary.Op1.Type.ActualType);
                noway_assert(tree.Type.ActualType == binary.Op2.Type.ActualType);
                if (operation is GT_AND)
                {
                    var operandToNarrow = 0;
                    var foundBlockingOperand = false;

                    // One unsigned narrow operand (or fitting constant) bounds
                    // the AND result even if its other operand cannot narrow.
                    if ((binary.Op2.Oper is GT_CNS_INT) || varTypeIsUnsigned(destinationType))
                    {
                        if (optNarrowTree(ref binary.Op2Ref, sourceType, destinationType, noValueNumbers, false))
                        {
                            operandToNarrow = 2;
                        }
                        else
                        {
                            foundBlockingOperand = true;
                        }
                    }

                    if ((operandToNarrow == 0) &&
                        ((binary.Op1.Oper is GT_CNS_INT) || varTypeIsUnsigned(destinationType)))
                    {
                        if (optNarrowTree(ref binary.Op1Ref, sourceType, destinationType, noValueNumbers, false))
                        {
                            operandToNarrow = 1;
                        }
                        else
                        {
                            foundBlockingOperand = true;
                        }
                    }

                    if (operandToNarrow != 0)
                    {
                        if (doIt)
                        {
                            tree.Type = destinationType.ActualType;
                            tree._vnPair = narrowedValueNumbers;
                            ref var narrowOperand = ref ((operandToNarrow == 1) ? ref binary.Op1Ref : ref binary.Op2Ref);
                            _ = optNarrowTree(ref narrowOperand, sourceType, destinationType, noValueNumbers, true);
                            if (sourceSize == 8)
                            {
                                assert(tree.Type is TYP_INT);
                                ref var otherOperand = ref ((operandToNarrow == 1) ? ref binary.Op2Ref : ref binary.Op1Ref);
                                var cast = gtNewCastNode(TYP_INT, otherOperand, false, TYP_INT);
                                cast.SetMorphed(this);
                                otherOperand = cast;
                            }
                        }

                        return true;
                    }

                    if (foundBlockingOperand)
                    {
                        noway_assert(!doIt);
                        return false;
                    }
                }

                if (!optNarrowTree(ref binary.Op1Ref, sourceType, destinationType, noValueNumbers, doIt) ||
                    !optNarrowTree(ref binary.Op2Ref, sourceType, destinationType, noValueNumbers, doIt))
                {
                    noway_assert(!doIt);
                    return false;
                }

                if (doIt)
                {
                    if (operation is GT_MUL)
                    {
                        tree.Flags &= ~GTF_MUL_64RSLT;
                    }

                    tree.Type = destinationType.ActualType;
                    tree._vnPair = narrowedValueNumbers;
                }

                return true;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GT:
            case GT_GE:
            {
                // Comparison results are always zero or one.
                return true;
            }

            case GT_CAST:
            {
#if DEBUG
                if ((tree._debugFlags & GTF_DEBUG_CAST_DONT_FOLD) != 0)
                {
                    return false;
                }
#endif
                var cast = tree.AsCast();
                // An unsigned widening cast can have CastType == ULONG but
                // Type == LONG. Compare actual types before canceling int->long->int.
                if ((cast.CastType.ActualType != sourceType.ActualType) || cast.HasOverflowCheck)
                {
                    return false;
                }

                if (varTypeIsInt(cast.CastOp.Type) && varTypeIsInt(destinationType) && (cast.Type is TYP_LONG))
                {
                    if (doIt)
                    {
                        cast.CastType = TYP_INT;
                        cast.ChangeType(TYP_INT);
                        cast.Flags &= ~GTF_UNSIGNED;
                    }

                    return true;
                }

                return false;
            }

            case GT_COMMA:
            {
                if (optNarrowTree(ref tree.AsOp().Op2Ref, sourceType, destinationType, narrowedValueNumbers, doIt))
                {
                    if (doIt)
                    {
                        tree.Type = destinationType.ActualType;
                        tree._vnPair = narrowedValueNumbers;
                    }

                    return true;
                }

                return false;
            }

            default:
            {
                if (operation.IsLeaf || operation.IsBinary || operation.IsUnary)
                {
                    noway_assert(!doIt);
                }

                return false;
            }
        }
    }
}
