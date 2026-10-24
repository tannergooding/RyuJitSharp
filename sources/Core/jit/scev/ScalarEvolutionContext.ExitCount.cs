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
        var store = _compiler.vnStore;
        if (store is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresValueNumbering);
        }

        if (store.IsVNConstant(vn))
        {
            assert(store.TypeOfVN(vn) is TYP_INT);
            return store.ConstantValue<int>(vn) != 0 ? RelopEvaluationResult.True : RelopEvaluationResult.False;
        }

        assert(_compiler._domTree is not null);
        var loop = _loop;
        if (loop is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresALoop);
        }

        return _compiler.EvaluateScalarEvolutionRelop(
            loop,
            vn);
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
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument,
                        exitOp);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument);
                }

                return default;
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
        var store = _compiler.vnStore;
        if (store is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresValueNumbering);
        }

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

        switch (oper)
        {
            case GT_EQ:
            case GT_NE:
            {
                return (VNFunc)oper;
            }

            case GT_LT:
            {
                return VNF_LT_UN;
            }

            case GT_LE:
            {
                return VNF_LE_UN;
            }

            case GT_GT:
            {
                return VNF_GT_UN;
            }

            case GT_GE:
            {
                return VNF_GE_UN;
            }

            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedRelationalOperationArgument,
                        oper);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedRelationalOperationArgument);
                }

                return default;
            }
        }
    }

    private static genTreeOps ReverseExitRelop(genTreeOps oper)
    {
        switch (oper)
        {
            case GT_LT:
            {
                return GT_GE;
            }

            case GT_LE:
            {
                return GT_GT;
            }

            case GT_GT:
            {
                return GT_LE;
            }

            case GT_GE:
            {
                return GT_LT;
            }

            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument,
                        oper);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument);
                }

                return default;
            }
        }
    }

    private static genTreeOps SwapExitRelop(genTreeOps oper)
    {
        switch (oper)
        {
            case GT_LT:
            {
                return GT_GT;
            }

            case GT_LE:
            {
                return GT_GE;
            }

            case GT_GT:
            {
                return GT_LT;
            }

            case GT_GE:
            {
                return GT_LE;
            }

            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument,
                        oper);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument);
                }

                return default;
            }
        }
    }

    public Scev? ComputeExitNotTakenCount(BasicBlock exiting)
    {
        var loop = _loop;
        if (loop is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresALoop);
        }

        assert(exiting.Kind is BBJ_COND);
        assert(loop.ContainsBlock(exiting.TrueTarget) != loop.ContainsBlock(exiting.FalseTarget));
        var jump = exiting.LastStmt?.RootNode;
        if (jump is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ExitingBlockMustHaveACondition);
        }

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
        if (Configuration.DEBUG && _compiler.verbose)
        {
            jitprintf($"  L{loop.Index:D2} exits when:\n  ");
            lhs.Dump(_compiler);
            string relation;
            switch (exitOp)
            {
                case GT_LT:
                {
                    relation = " < ";
                    break;
                }

                case GT_LE:
                {
                    relation = " <= ";
                    break;
                }

                case GT_GT:
                {
                    relation = " > ";
                    break;
                }

                case GT_GE:
                {
                    relation = " >= ";
                    break;
                }

                default:
                {
                    if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(
                            FatalJitErrorCode.UnexpectedExitOperationArgument,
                            exitOp);
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(
                            FatalJitErrorCode.UnexpectedExitOperationArgument);
                    }

                    relation = default!;
                    break;
                }
            }
            jitprintf(relation);
            rhs.Dump(_compiler);
            jitprintf("\n");
        }

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
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument,
                        exitOpVNF);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedExitOperationArgument);
                }

                return null;
            }
        }

        lowerBound = Simplify(lowerBound);
        upperBound = Simplify(upperBound);
        JITDUMP("  Need to prove ");
        if (Configuration.DEBUG && _compiler.verbose)
        {
            lowerBound.Dump(_compiler);
        }
        JITDUMP(" <= ");
        if (Configuration.DEBUG && _compiler.verbose)
        {
            upperBound.Dump(_compiler);
        }

        var store = _compiler.vnStore;
        if (store is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresValueNumbering);
        }

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
        if (Configuration.DEBUG)
        {
            switch (result)
            {
                case RelopEvaluationResult.Unknown:
                {
                    JITDUMP(": unknown\n");
                    break;
                }

                case RelopEvaluationResult.True:
                {
                    JITDUMP(": true\n");
                    break;
                }

                case RelopEvaluationResult.False:
                {
                    JITDUMP(": false\n");
                    break;
                }

                default:
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedRelationalResultArgument, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? result : default));
                    return null;
                }
            }
        }
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
        if (Configuration.DEBUG && _compiler.verbose)
        {
            backedgeCount.Dump(_compiler);
        }
        JITDUMP("\n");
        return backedgeCount;
    }
}
