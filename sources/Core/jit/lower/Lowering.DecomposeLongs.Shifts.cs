// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LOWER_DECOMPOSE_LONGS
using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private sealed partial class DecomposeLongs
    {
        private unsafe GenTree? DecomposeShift(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            var shift = use.Def().AsOp();
            var gtLong = shift.Op1.AsOp();
            var loOp1 = gtLong.Op1;
            var hiOp1 = gtLong.Op2;
            var shiftByOp = shift.Op2;
            var oper = shift.Oper;
            assert(oper is GT_LSH or GT_RSH or GT_RSZ);
            if (shiftByOp.Oper is GT_CNS_INT)
            {
                // The helpers, constant folder and value numbering all reduce the count modulo 64.
                var count = unchecked((uint)shiftByOp.AsIntCon().IconValue) & 0x3F;
                Range().Remove(shiftByOp);
                if (count == 0)
                {
                    var next = shift.Next;
                    if (shift.IsUnusedValue)
                    {
                        gtLong.IsUnusedValue = true;
                    }

                    Range().Remove(shift);
                    use.ReplaceWith(gtLong);
                    return next;
                }

                GenTree loResult;
                GenTree hiResult;
                GenTree insertAfter;
                switch (oper)
                {
                    case GT_LSH:
                    {
                        if (count < 32)
                        {
                            loOp1 = RepresentOpAsLocalVar(loOp1, gtLong, ref gtLong.Op1Ref);
                            var loOp1LclNum = loOp1.AsLclVarCommon().LclNum;
                            Range().Remove(loOp1);
                            var shiftByHi = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            var shiftByLo = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            loResult = _compiler.gtNewBinaryNode(GT_LSH, TYP_INT, loOp1, shiftByLo);
                            var loCopy = _compiler.gtNewLclvNode(TYP_INT, loOp1LclNum);
                            var hiOp = new GenTreeOp(GT_LONG, TYP_LONG, loCopy, hiOp1);
                            hiResult = _compiler.gtNewBinaryNode(GT_LSH_HI, TYP_INT, hiOp, shiftByHi);
                            Range().InsertBefore(shift, loOp1, shiftByLo, loResult);
                            Range().InsertBefore(shift, loCopy, hiOp, shiftByHi, hiResult);
                            insertAfter = hiResult;
                        }
                        else
                        {
                            assert((count >= 32) && (count < 64));
                            if ((hiOp1.Flags & GTF_ALL_EFFECT) == 0)
                            {
                                Range().Remove(hiOp1, true);
                            }
                            else
                            {
                                hiOp1.IsUnusedValue = true;
                            }

                            if (count == 32)
                            {
                                // Save the low half before an in-place shift can overwrite it.
                                var loOp1Use = new LIR.Use(Range(), ref gtLong.Op1Ref, gtLong);
                                _ = loOp1Use.ReplaceWithLclVar(_compiler);
                                hiResult = loOp1Use.Def();
                            }
                            else
                            {
                                var shiftBy = _compiler.gtNewIconNode(TYP_INT, (nint)(count - 32));
                                hiResult = _compiler.gtNewBinaryNode(oper, TYP_INT, loOp1, shiftBy);
                                Range().InsertBefore(shift, shiftBy, hiResult);
                            }

                            loResult = _compiler.gtNewZeroConNode(TYP_INT);
                            Range().InsertBefore(shift, loResult);
                            insertAfter = loResult;
                        }

                        break;
                    }

                    case GT_RSZ:
                    {
                        if (count < 32)
                        {
                            hiOp1 = RepresentOpAsLocalVar(hiOp1, gtLong, ref gtLong.Op2Ref);
                            var hiOp1LclNum = hiOp1.AsLclVarCommon().LclNum;
                            var hiCopy = _compiler.gtNewLclvNode(TYP_INT, hiOp1LclNum);
                            var shiftByHi = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            var shiftByLo = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            hiResult = _compiler.gtNewBinaryNode(GT_RSZ, TYP_INT, hiOp1, shiftByHi);
                            var loOp = new GenTreeOp(GT_LONG, TYP_LONG, loOp1, hiCopy);
                            loResult = _compiler.gtNewBinaryNode(GT_RSH_LO, TYP_INT, loOp, shiftByLo);
                            Range().InsertBefore(shift, hiCopy, loOp);
                            Range().InsertBefore(shift, shiftByLo, loResult);
                            Range().InsertBefore(shift, shiftByHi, hiResult);
                        }
                        else
                        {
                            assert((count >= 32) && (count < 64));
                            if ((loOp1.Flags & (GTF_ALL_EFFECT | GTF_SET_FLAGS)) == 0)
                            {
                                Range().Remove(loOp1, true);
                            }
                            else
                            {
                                loOp1.IsUnusedValue = true;
                            }

                            if (count == 32)
                            {
                                loResult = hiOp1;
                            }
                            else
                            {
                                var shiftBy = _compiler.gtNewIconNode(TYP_INT, (nint)(count - 32));
                                loResult = _compiler.gtNewBinaryNode(oper, TYP_INT, hiOp1, shiftBy);
                                Range().InsertBefore(shift, shiftBy, loResult);
                            }

                            hiResult = _compiler.gtNewZeroConNode(TYP_INT);
                            Range().InsertBefore(shift, hiResult);
                        }

                        insertAfter = hiResult;
                        break;
                    }

                    case GT_RSH:
                    {
                        hiOp1 = RepresentOpAsLocalVar(hiOp1, gtLong, ref gtLong.Op2Ref);
                        var hiOp1LclNum = hiOp1.AsLclVarCommon().LclNum;
                        var hiCopy = _compiler.gtNewLclvNode(TYP_INT, hiOp1LclNum);
                        Range().Remove(hiOp1);
                        if (count < 32)
                        {
                            var shiftByHi = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            var shiftByLo = _compiler.gtNewIconNode(TYP_INT, (nint)count);
                            hiResult = _compiler.gtNewBinaryNode(GT_RSH, TYP_INT, hiOp1, shiftByHi);
                            var loOp = new GenTreeOp(GT_LONG, TYP_LONG, loOp1, hiCopy);
                            loResult = _compiler.gtNewBinaryNode(GT_RSH_LO, TYP_INT, loOp, shiftByLo);
                            Range().InsertBefore(shift, hiCopy, loOp);
                            Range().InsertBefore(shift, shiftByLo, loResult);
                            Range().InsertBefore(shift, shiftByHi, hiOp1, hiResult);
                        }
                        else
                        {
                            assert((count >= 32) && (count < 64));
                            if ((loOp1.Flags & (GTF_ALL_EFFECT | GTF_SET_FLAGS)) == 0)
                            {
                                Range().Remove(loOp1, true);
                            }
                            else
                            {
                                loOp1.IsUnusedValue = true;
                            }

                            if (count == 32)
                            {
                                loResult = hiOp1;
                                Range().InsertBefore(shift, loResult);
                            }
                            else
                            {
                                var shiftBy = _compiler.gtNewIconNode(TYP_INT, (nint)(count - 32));
                                loResult = _compiler.gtNewBinaryNode(oper, TYP_INT, hiOp1, shiftBy);
                                Range().InsertBefore(shift, hiOp1, shiftBy, loResult);
                            }

                            var signShiftBy = _compiler.gtNewIconNode(TYP_INT, 31);
                            hiResult = _compiler.gtNewBinaryNode(GT_RSH, TYP_INT, hiCopy, signShiftBy);
                            Range().InsertBefore(shift, signShiftBy, hiCopy, hiResult);
                        }

                        insertAfter = hiResult;
                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException("Unexpected long shift operator.");
                    }
                }

                Range().Remove(gtLong);
                Range().Remove(shift);

                return FinalizeDecomposition(ref use, loResult, hiResult, insertAfter);
            }

            // The helper call is morphed as HIR. Its operands cannot be shared LIR temporaries.
            shiftByOp = RepresentOpAsLocalVar(shiftByOp, shift, ref shift.Op2Ref);
            loOp1 = RepresentOpAsLocalVar(loOp1, gtLong, ref gtLong.Op1Ref);
            hiOp1 = RepresentOpAsLocalVar(hiOp1, gtLong, ref gtLong.Op2Ref);
            Range().Remove(shiftByOp);
            Range().Remove(gtLong);
            Range().Remove(loOp1);
            Range().Remove(hiOp1);
            var helper = oper switch {
                GT_LSH => CORINFO_HELP_LLSH,
                GT_RSH => CORINFO_HELP_LRSH,
                GT_RSZ => CORINFO_HELP_LRSZ,
                _ => throw new InvalidOperationException("Unexpected long shift operator."),
            };
            var call = _compiler.gtNewHelperCallNode(TYP_LONG, helper);
            var loArg = NewCallArg.CreateForPrimitive(loOp1).WithWellKnownArg(WellKnownArg.ShiftLow);
            var hiArg = NewCallArg.CreateForPrimitive(hiOp1).WithWellKnownArg(WellKnownArg.ShiftHigh);
            var shiftByArg = NewCallArg.CreateForPrimitive(shiftByOp);
            _ = call.Args.PushFront(shiftByArg);
            _ = call.Args.PushFront(hiArg);
            _ = call.Args.PushFront(loArg);
            call.Flags |= shift.Flags & GTF_ALL_EFFECT;
            if (shift.IsUnusedValue)
            {
                call.IsUnusedValue = true;
            }

            call = _compiler.fgMorphArgs(call);
            Range().InsertAfter(shift, LIR.SeqTree(_compiler, call));
            Range().Remove(shift);
            use.ReplaceWith(call);

            return call;
        }

        private GenTree? DecomposeRotate(ref LIR.Use use)
        {
            var tree = use.Def().AsOp();
            var gtLong = tree.Op1.AsOp();
            var rotateByOp = tree.Op2;
            var oper = tree.Oper;
            assert(oper is GT_ROL or GT_ROR);
            assert(rotateByOp.Oper.IsCnsIntOrI);
            oper = oper is GT_ROL ? GT_LSH_HI : GT_RSH_LO;
            var count = unchecked((uint)rotateByOp.AsIntCon().IconValue);
            Range().Remove(rotateByOp);
            assert((count < 64) && (count != 0));
            if (count == 32)
            {
                var loOp1Use = new LIR.Use(Range(), ref gtLong.Op1Ref, gtLong);
                _ = loOp1Use.ReplaceWithLclVar(_compiler);
                var hiOp1Use = new LIR.Use(Range(), ref gtLong.Op2Ref, gtLong);
                _ = hiOp1Use.ReplaceWithLclVar(_compiler);
                var hiResult = loOp1Use.Def();
                var loResult = hiOp1Use.Def();
                gtLong.Op1 = loResult;
                gtLong.Op2 = hiResult;
                if (tree.IsUnusedValue)
                {
                    gtLong.IsUnusedValue = true;
                }

                var next = tree.Next;
                Range().Remove(tree);
                use.ReplaceWith(gtLong);
                return next;
            }

            GenTree loOp1;
            GenTree hiOp1;
            if (count > 32)
            {
                hiOp1 = gtLong.Op1;
                loOp1 = gtLong.Op2;
                loOp1 = RepresentOpAsLocalVar(loOp1, gtLong, ref gtLong.Op2Ref);
                hiOp1 = RepresentOpAsLocalVar(hiOp1, gtLong, ref gtLong.Op1Ref);
                count -= 32;
            }
            else
            {
                loOp1 = gtLong.Op1;
                hiOp1 = gtLong.Op2;
                loOp1 = RepresentOpAsLocalVar(loOp1, gtLong, ref gtLong.Op1Ref);
                hiOp1 = RepresentOpAsLocalVar(hiOp1, gtLong, ref gtLong.Op2Ref);
            }

            if (oper is GT_RSH_LO)
            {
                (loOp1, hiOp1) = (hiOp1, loOp1);
            }

            Range().Remove(gtLong);
            var loOp1LclNum = loOp1.AsLclVarCommon().LclNum;
            var hiOp1LclNum = hiOp1.AsLclVarCommon().LclNum;
            Range().Remove(loOp1);
            Range().Remove(hiOp1);
            var rotateByHi = _compiler.gtNewIconNode(TYP_INT, (nint)count);
            var rotateByLo = _compiler.gtNewIconNode(TYP_INT, (nint)count);
            var hiCopy = _compiler.gtNewLclvNode(TYP_INT, hiOp1LclNum);
            var loOp = new GenTreeOp(GT_LONG, TYP_LONG, hiCopy, loOp1);
            var lowerResult = _compiler.gtNewBinaryNode(oper, TYP_INT, loOp, rotateByLo);
            var loCopy = _compiler.gtNewLclvNode(TYP_INT, loOp1LclNum);
            var hiOp = new GenTreeOp(GT_LONG, TYP_LONG, loCopy, hiOp1);
            var upperResult = _compiler.gtNewBinaryNode(oper, TYP_INT, hiOp, rotateByHi);
            Range().InsertBefore(tree, hiCopy, loOp1, loOp);
            Range().InsertBefore(tree, rotateByLo, lowerResult);
            Range().InsertBefore(tree, loCopy, hiOp1, hiOp);
            Range().InsertBefore(tree, rotateByHi, upperResult);
            Range().Remove(tree);

            return FinalizeDecomposition(ref use, lowerResult, upperResult, upperResult);
        }
    }
}
#endif
