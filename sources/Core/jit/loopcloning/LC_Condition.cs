// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public readonly struct LC_Expr
{
    public enum ExprType
    {
        Invalid,
        Ident,
    }

    public readonly LC_Ident Ident;
    public readonly ExprType Type;

    public LC_Expr(LC_Ident ident)
    {
        Ident = ident;
        Type = ExprType.Ident;
    }

#if DEBUG
    public void Print()
    {
        if (Type is ExprType.Ident)
        {
            Ident.Print();
        }
        else
        {
            jitprintf("INVALID");
        }
    }
#endif

    public bool Matches(in LC_Expr that)
    {
        assert(Type is not ExprType.Invalid && that.Type is not ExprType.Invalid);
        return (Type == that.Type) && Ident.Matches(that.Ident);
    }

    public GenTree ToGenTree(Compiler compiler, BasicBlock block)
    {
        if (Type is not ExprType.Ident)
        {
            throw new FatalJitException("Cannot materialize an invalid loop-cloning expression.");
        }
        return Ident.ToGenTree(compiler, block);
    }
}

public readonly struct LC_Condition
{
    public readonly LC_Expr Op1;
    public readonly LC_Expr Op2;
    public readonly genTreeOps Oper;
    public readonly bool CompareUnsigned;

    public LC_Condition(genTreeOps oper, LC_Expr op1, LC_Expr op2, bool asUnsigned = false)
    {
        Oper = oper;
        Op1 = op1;
        Op2 = op2;
        CompareUnsigned = asUnsigned;
    }

#if DEBUG
    public void Print()
    {
        Op1.Print();
        jitprintf($" {Oper.ToString()[3..]}{(CompareUnsigned ? "U" : "")} ");
        Op2.Print();
    }
#endif

    public GenTree ToGenTree(Compiler compiler, BasicBlock block, bool invert)
    {
        var left = Op1.ToGenTree(compiler, block);
        var right = Op2.ToGenTree(compiler, block);
        assert(left.Type.ActualType.Size == right.Type.ActualType.Size);
        var result = compiler.gtNewBinaryNode(invert ? Oper.ReverseRelop : Oper, TYP_INT, left, right);
        if (CompareUnsigned)
        {
            result.Flags |= GTF_UNSIGNED;
        }
        return result;
    }

    public bool Evaluates(out bool result)
    {
        result = false;
        switch (Oper)
        {
            case GT_EQ:
            case GT_GE:
            case GT_LE:
            {
                if (Op1.Matches(Op2))
                {
                    result = true;
                    return true;
                }
                break;
            }

            case GT_GT:
            case GT_LT:
            case GT_NE:
            {
                if (Op1.Matches(Op2))
                {
                    return true;
                }
                break;
            }
        }
        return false;
    }

    public bool Combines(in LC_Condition that, out LC_Condition combined)
    {
        if (((Oper == that.Oper) && Op1.Matches(that.Op1) && Op2.Matches(that.Op2)) ||
            ((Oper is GT_LT or GT_LE or GT_GT or GT_GE) && (Oper.ReverseRelop == that.Oper) &&
             Op1.Matches(that.Op2) && Op2.Matches(that.Op1)))
        {
            combined = this;
            return true;
        }
        combined = default;
        return false;
    }
}
