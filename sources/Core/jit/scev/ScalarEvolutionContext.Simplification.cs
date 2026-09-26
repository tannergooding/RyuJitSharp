// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class ScalarEvolutionContext
{
    public Scev Simplify(Scev scev, SimplificationAssumptions assumptions = default)
    {
        switch (scev)
        {
            case ScevConstant:
            {
                return scev;
            }

            case ScevLocal local:
            {
                return local.GetConstantValue(_compiler, out var value) ? NewConstant(local.Type, value) : local;
            }

            case ScevUnop { Oper: ScevOper.ZeroExtend or ScevOper.SignExtend } unop:
            {
                assert(unop.Type.Size >= unop.Op1.Type.Size);
                var op1 = Simplify(unop.Op1, assumptions);
                if (unop.Type == op1.Type)
                {
                    return op1;
                }

                assert(unop.Type is TYP_LONG && op1.Type is TYP_INT);
                if (op1 is ScevConstant constant)
                {
                    // Native converts the signed 32-bit value directly to uint64_t for either extension.
                    return NewConstant(unop.Type, unchecked((int)constant.Value));
                }

                if (op1 is ScevAddRec addRec && !AddRecMayOverflow(addRec,
                        unop.Oper is ScevOper.SignExtend, assumptions))
                {
                    var newStart = Simplify(NewExtension(unop.Oper, TYP_LONG, addRec.Start), assumptions);
                    var newStep = Simplify(NewExtension(unop.Oper, TYP_LONG, addRec.Step), assumptions);
                    return NewAddRec(newStart, newStep);
                }

                return ReferenceEquals(op1, unop.Op1) ? unop : NewExtension(unop.Oper, unop.Type, op1);
            }

            case ScevBinop binop:
            {
                var op1 = Simplify(binop.Op1, assumptions);
                var op2 = Simplify(binop.Op2, assumptions);

                if (binop.Oper is ScevOper.Add or ScevOper.Mul)
                {
                    if (op2 is ScevAddRec && op1 is not ScevAddRec)
                    {
                        (op1, op2) = (op2, op1);
                    }

                    if (op1 is ScevConstant && op2 is not ScevConstant)
                    {
                        (op1, op2) = (op2, op1);
                    }
                }

                if (op1 is ScevAddRec addRec)
                {
                    var newStart = Simplify(NewBinop(binop.Oper, addRec.Start, op2), assumptions);
                    var newStep = binop.Oper is ScevOper.Mul or ScevOper.Lsh
                        ? Simplify(NewBinop(binop.Oper, addRec.Step, op2), assumptions) : addRec.Step;
                    return NewAddRec(newStart, newStep);
                }

                if ((op1 is ScevConstant cns1) && (op2 is ScevConstant cns2))
                {
                    long newValue;
                    if (binop.Type.Size == 4)
                    {
                        var left = unchecked((uint)cns1.Value);
                        var right = unchecked((uint)cns2.Value);
                        newValue = binop.Oper switch {
                            ScevOper.Add => unchecked(left + right),
                            ScevOper.Mul => unchecked(left * right),
                            ScevOper.Lsh => left << (int)right,
                            _ => throw new InvalidOperationException($"Unexpected operation {binop.Oper}."),
                        };
                    }
                    else
                    {
                        assert(binop.Type.Size == 8);
                        var left = unchecked((ulong)cns1.Value);
                        var right = unchecked((ulong)cns2.Value);
                        newValue = unchecked((long)(binop.Oper switch {
                            ScevOper.Add => unchecked(left + right),
                            ScevOper.Mul => unchecked(left * right),
                            ScevOper.Lsh => left << (int)right,
                            _ => throw new InvalidOperationException($"Unexpected operation {binop.Oper}."),
                        }));
                    }

                    return NewConstant(binop.Type, newValue);
                }

                if (op2 is ScevConstant rightConstant)
                {
                    if ((binop.Oper is ScevOper.Add or ScevOper.Lsh) && rightConstant.Value == 0)
                    {
                        return op1;
                    }

                    if (binop.Oper is ScevOper.Add &&
                        op1 is ScevBinop { Oper: ScevOper.Add, Op2: ScevConstant } leftAdd)
                    {
                        return Simplify(NewBinop(ScevOper.Add, leftAdd.Op1,
                            NewBinop(ScevOper.Add, leftAdd.Op2, rightConstant)), assumptions);
                    }

                    if (binop.Oper is ScevOper.Mul)
                    {
                        if (rightConstant.Value == 0)
                        {
                            return rightConstant;
                        }

                        if (rightConstant.Value == 1)
                        {
                            return op1;
                        }

                        if (op1 is ScevBinop { Oper: ScevOper.Mul, Op2: ScevConstant } leftMul)
                        {
                            return Simplify(NewBinop(ScevOper.Mul, leftMul.Op1,
                                NewBinop(ScevOper.Mul, leftMul.Op2, rightConstant)), assumptions);
                        }
                    }
                }
                else if (op1 is ScevConstant leftConstant &&
                         binop.Oper is ScevOper.Lsh && leftConstant.Value == 0)
                {
                    return leftConstant;
                }

                if (binop.Oper is ScevOper.Add &&
                    op1 is ScevBinop { Oper: ScevOper.Add, Op2: ScevConstant } first &&
                    op2 is ScevBinop { Oper: ScevOper.Add, Op2: ScevConstant } second)
                {
                    return Simplify(NewBinop(ScevOper.Add,
                        NewBinop(ScevOper.Add, first.Op1, second.Op1),
                        NewBinop(ScevOper.Add, first.Op2, second.Op2)), assumptions);
                }

                return ReferenceEquals(op1, binop.Op1) && ReferenceEquals(op2, binop.Op2)
                    ? binop : NewBinop(binop.Oper, op1, op2);
            }

            case ScevAddRec addRec:
            {
                var start = Simplify(addRec.Start, assumptions);
                var step = Simplify(addRec.Step, assumptions);
                return ReferenceEquals(start, addRec.Start) && ReferenceEquals(step, addRec.Step)
                    ? addRec : NewAddRec(start, step);
            }

            default:
            {
                throw new InvalidOperationException($"Unexpected scalar evolution operation {scev.Oper}.");
            }
        }
    }
}
