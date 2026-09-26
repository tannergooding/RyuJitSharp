// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp;

public sealed partial class ScalarEvolutionContext
{
    public RelopEvaluationResult EvaluateRelop(ValueNum vn)
    {
        var store = _compiler.vnStore ?? throw new InvalidOperationException("Scalar evolution requires value numbering.");
        if (store.IsVNConstant(vn))
        {
            assert(store.TypeOfVN(vn) is TYP_INT);
            return store.ConstantValue<int>(vn) != 0 ? RelopEvaluationResult.True : RelopEvaluationResult.False;
        }

        assert(_compiler._domTree is not null);
        return _compiler.EvaluateScalarEvolutionRelop(
            _loop ?? throw new InvalidOperationException("Scalar evolution requires a loop."), vn);
    }

    private bool MayOverflowBeforeExit(ScevAddRec lhs, Scev rhs, VNFunc exitOp)
    {
        if (!lhs.Step.GetConstantValue(_compiler, out var stepCns))
        {
            return true;
        }

        switch (exitOp)
        {
            case VNF_GE:
            case VNF_GT:
            case VNF_GE_UN:
            case VNF_GT_UN:
            {
                if (stepCns < 0)
                {
                    return true;
                }

                break;
            }

            case VNF_LE:
            case VNF_LT:
            case VNF_LE_UN:
            case VNF_LT_UN:
            {
                if (stepCns > 0)
                {
                    return true;
                }

                break;
            }

            default:
            {
                throw new InvalidOperationException($"Unexpected exit operation {exitOp}.");
            }
        }

        if ((stepCns == 1) && exitOp is VNF_GE or VNF_GE_UN)
        {
            return false;
        }

        if ((stepCns == -1) && exitOp is VNF_LE or VNF_LE_UN)
        {
            return false;
        }

        var step = lhs.Step;
        if (exitOp is VNF_GE or VNF_GE_UN)
        {
            step = NewBinop(ScevOper.Add, step, NewConstant(rhs.Type, -1));
        }
        else if (exitOp is VNF_LE or VNF_LE_UN)
        {
            step = NewBinop(ScevOper.Add, step, NewConstant(rhs.Type, 1));
        }

        var steppedVal = Simplify(NewBinop(ScevOper.Add, rhs, step));
        var steppedValVNP = MaterializeVN(steppedVal);
        var rhsVNP = MaterializeVN(rhs);
        var store = _compiler.vnStore ?? throw new InvalidOperationException("Scalar evolution requires value numbering.");
        var relop = store.VNForFunc(TYP_INT, exitOp, steppedValVNP.Liberal, rhsVNP.Liberal);
        return EvaluateRelop(relop) is not RelopEvaluationResult.True;
    }

    private bool AddRecMayOverflow(ScevAddRec addRec, bool signedBound, SimplificationAssumptions assumptions)
    {
        var bounds = assumptions.BackEdgeTakenBound;
        if ((bounds is null) || (bounds.Length == 0) || addRec.Type is not TYP_INT)
        {
            return true;
        }

        if (signedBound || !addRec.Start.GetConstantValue(_compiler, out var startCns) || (startCns != 0) ||
            !addRec.Step.GetConstantValue(_compiler, out var stepCns) || (stepCns != 1))
        {
            return true;
        }

        foreach (var bound in bounds)
        {
            if (bound.Type is TYP_INT)
            {
                return false;
            }
        }

        return true;
    }

    private static VNFunc MapRelopToVNFunc(genTreeOps oper, bool isUnsigned)
    {
        if (!isUnsigned)
        {
            return (VNFunc)oper;
        }

        return oper switch {
            GT_EQ or GT_NE => (VNFunc)oper,
            GT_LT => VNF_LT_UN,
            GT_LE => VNF_LE_UN,
            GT_GT => VNF_GT_UN,
            GT_GE => VNF_GE_UN,
            _ => throw new InvalidOperationException($"Unexpected relational operation {oper}."),
        };
    }

    private static genTreeOps ReverseExitRelop(genTreeOps oper) => oper switch {
        GT_LT => GT_GE,
        GT_LE => GT_GT,
        GT_GT => GT_LE,
        GT_GE => GT_LT,
        _ => throw new InvalidOperationException($"Unexpected exit operation {oper}."),
    };

    private static genTreeOps SwapExitRelop(genTreeOps oper) => oper switch {
        GT_LT => GT_GT,
        GT_LE => GT_GE,
        GT_GT => GT_LT,
        GT_GE => GT_LE,
        _ => throw new InvalidOperationException($"Unexpected exit operation {oper}."),
    };

    public Scev? ComputeExitNotTakenCount(BasicBlock exiting)
    {
        var loop = _loop ?? throw new InvalidOperationException("Scalar evolution requires a loop.");
        assert(exiting.Kind is BBJ_COND);
        assert(loop.ContainsBlock(exiting.TrueTarget) != loop.ContainsBlock(exiting.FalseTarget));
        var jump = exiting.LastStmt?.RootNode ?? throw new InvalidOperationException("An exiting block must have a condition.");
        assert(jump.Oper is GT_JTRUE);
        var cond = jump.AsUnOp().Op1;
        if (cond.Oper is not (GT_LT or GT_LE or GT_GT or GT_GE))
        {
            return null;
        }

        if (!varTypeIsIntegralOrI(cond.AsOp().Op1.Type))
        {
            return null;
        }

        var op1 = Analyze(exiting, cond.AsOp().Op1);
        var op2 = Analyze(exiting, cond.AsOp().Op2);
        if ((op1 is null) || (op2 is null) || varTypeIsGC(op1.Type) || varTypeIsGC(op2.Type))
        {
            return null;
        }

        var lhs = Simplify(op1);
        var rhs = Simplify(op2);
        var exitOp = cond.Oper;
        if (!loop.ContainsBlock(exiting.FalseTarget))
        {
            exitOp = ReverseExitRelop(exitOp);
        }

        if (lhs is not ScevAddRec && rhs is not ScevAddRec)
        {
            return null;
        }

        var lhsInvariant = lhs.IsInvariant();
        var rhsInvariant = rhs.IsInvariant();
        if (lhsInvariant == rhsInvariant)
        {
            return null;
        }

        if (lhsInvariant)
        {
            exitOp = SwapExitRelop(exitOp);
            (lhs, rhs) = (rhs, lhs);
        }

        assert(lhs is ScevAddRec);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"  L{loop.Index:D2} exits when:\n  ");
            lhs.Dump(_compiler);
            jitprintf(exitOp switch {
                GT_LT => " < ",
                GT_LE => " <= ",
                GT_GT => " > ",
                GT_GE => " >= ",
                _ => throw new InvalidOperationException($"Unexpected exit operation {exitOp}."),
            });
            rhs.Dump(_compiler);
            jitprintf("\n");
        }
#endif

        var exitOpVNF = MapRelopToVNFunc(exitOp, cond.AsOp().IsUnsigned);
        if (MayOverflowBeforeExit((ScevAddRec)lhs, rhs, exitOpVNF))
        {
            JITDUMP("  May overflow, cannot determine backedge count\n");
            return null;
        }

        JITDUMP("  Does not overflow past the test\n");
        Scev lowerBound;
        Scev upperBound;
        Scev divisor;
        var addRec = (ScevAddRec)lhs;
        switch (exitOpVNF)
        {
            case VNF_GE:
            case VNF_GE_UN:
            {
                var stepNegOne = NewBinop(ScevOper.Add, addRec.Step, NewConstant(rhs.Type, -1));
                var rhsWithStep = NewBinop(ScevOper.Add, rhs, stepNegOne);
                lowerBound = addRec.Start;
                upperBound = rhsWithStep;
                divisor = addRec.Step;
                break;
            }

            case VNF_GT:
            case VNF_GT_UN:
            {
                lowerBound = addRec.Start;
                upperBound = NewBinop(ScevOper.Add, rhs, addRec.Step);
                divisor = addRec.Step;
                break;
            }

            case VNF_LE:
            case VNF_LE_UN:
            {
                var stepPlusOne = NewBinop(ScevOper.Add, addRec.Step, NewConstant(rhs.Type, 1));
                var rhsWithStep = NewBinop(ScevOper.Add, rhs, stepPlusOne);
                lowerBound = rhsWithStep;
                upperBound = addRec.Start;
                divisor = NewBinop(ScevOper.Mul, addRec.Step, NewConstant(lhs.Type, -1));
                break;
            }

            case VNF_LT:
            case VNF_LT_UN:
            {
                lowerBound = NewBinop(ScevOper.Add, rhs, addRec.Step);
                upperBound = addRec.Start;
                divisor = NewBinop(ScevOper.Mul, addRec.Step, NewConstant(lhs.Type, -1));
                break;
            }

            default:
            {
                throw new InvalidOperationException($"Unexpected exit operation {exitOpVNF}.");
            }
        }

        lowerBound = Simplify(lowerBound);
        upperBound = Simplify(upperBound);
        JITDUMP("  Need to prove ");
#if DEBUG
        if (_compiler.verbose)
        {
            lowerBound.Dump(_compiler);
        }
#endif
        JITDUMP(" <= ");
#if DEBUG
        if (_compiler.verbose)
        {
            upperBound.Dump(_compiler);
        }
#endif

        var store = _compiler.vnStore ?? throw new InvalidOperationException("Scalar evolution requires value numbering.");
        var relopFunc = ValueNumStore.VNFuncIsSignedComparison(exitOpVNF) ? VNF_LE : VNF_LE_UN;
        var lowerBoundVNP = MaterializeVN(lowerBound);
        if (lowerBoundVNP.Liberal == ValueNumStore.NoVN)
        {
            return null;
        }

        var upperBoundVNP = MaterializeVN(upperBound);
        if (upperBoundVNP.Liberal == ValueNumStore.NoVN)
        {
            return null;
        }

        var relop = store.VNForFunc(TYP_INT, relopFunc, lowerBoundVNP.Liberal, upperBoundVNP.Liberal);
        var result = EvaluateRelop(relop);
        JITDUMP(result switch {
            RelopEvaluationResult.Unknown => ": unknown\n",
            RelopEvaluationResult.True => ": true\n",
            RelopEvaluationResult.False => ": false\n",
            _ => throw new InvalidOperationException($"Unexpected relational result {result}."),
        });
        if (result is not RelopEvaluationResult.True)
        {
            return null;
        }

        divisor = Simplify(divisor);
        if (!divisor.GetConstantValue(_compiler, out var divisorVal) || divisorVal is not (1 or -1))
        {
            return null;
        }

        Scev backedgeCount = NewBinop(ScevOper.Add, upperBound,
            NewBinop(ScevOper.Mul, lowerBound, NewConstant(lowerBound.Type, -1)));
        if (divisorVal == -1)
        {
            backedgeCount = NewBinop(ScevOper.Mul, backedgeCount, NewConstant(backedgeCount.Type, -1));
        }

        backedgeCount = Simplify(backedgeCount);
        JITDUMP("  Backedge count: ");
#if DEBUG
        if (_compiler.verbose)
        {
            backedgeCount.Dump(_compiler);
        }
#endif
        JITDUMP("\n");
        return backedgeCount;
    }
}
