// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed class OptIfConversionDsc
    {
        private readonly Compiler _compiler;
        private readonly BasicBlock _startBlock;
        private BasicBlock? _finalBlock;
        private GenTree _cond = null!;
        private IfConvertOperation _thenOperation;
        private IfConvertOperation _elseOperation;
        private genTreeOps _mainOper = GT_COUNT;

        private struct IfConvertOperation
        {
            public BasicBlock? Block;
            public Statement? Stmt;
            public GenTree? Node;
        }

#if TARGET_RISCV64
        private readonly struct IntConstSelectOper
        {
            public genTreeOps Oper { get; }
            public var_types Type { get; }
            public uint BitIndex { get; }

            public bool IsMatched => Oper is not GT_NONE;

            public IntConstSelectOper(genTreeOps oper, var_types type, uint bitIndex = 0)
            {
                Oper = oper;
                Type = type;
                BitIndex = bitIndex;
            }
        }

        private static IntConstSelectOper MatchIntConstSelectValues(long trueValue, long falseValue)
        {
            if (trueValue == unchecked(falseValue + 1))
            {
                return new IntConstSelectOper(GT_ADD, TYP_LONG);
            }

            if (trueValue == unchecked((long)(unchecked((int)falseValue + 1))))
            {
                return new IntConstSelectOper(GT_ADD, TYP_INT);
            }

            if (falseValue is 0)
            {
                var bitIndex = unchecked((uint)System.Numerics.BitOperations.Log2(unchecked((ulong)trueValue)));
                assert(bitIndex > 0);
                if (trueValue == unchecked(1L << (int)bitIndex))
                {
                    return new IntConstSelectOper(GT_LSH, TYP_LONG, bitIndex);
                }

                bitIndex = unchecked((uint)System.Numerics.BitOperations.Log2(unchecked((uint)trueValue)));
                assert(bitIndex > 0);
                if (trueValue == unchecked((long)(1 << (int)bitIndex)))
                {
                    return new IntConstSelectOper(GT_LSH, TYP_INT, bitIndex);
                }
            }

            if (trueValue == unchecked(falseValue << 1))
            {
                return new IntConstSelectOper(GT_LSH, TYP_LONG);
            }

            if (trueValue == unchecked((long)(unchecked((int)falseValue) << 1)))
            {
                return new IntConstSelectOper(GT_LSH, TYP_INT);
            }

            if (trueValue == (falseValue >> 1))
            {
                return new IntConstSelectOper(GT_RSH, TYP_LONG);
            }

            if (trueValue == unchecked((long)(unchecked((int)falseValue) >> 1)))
            {
                return new IntConstSelectOper(GT_RSH, TYP_INT);
            }

            if (trueValue == unchecked((long)(unchecked((ulong)falseValue) >> 1)))
            {
                return new IntConstSelectOper(GT_RSZ, TYP_LONG);
            }

            if (trueValue == unchecked((long)(unchecked((uint)falseValue) >> 1)))
            {
                return new IntConstSelectOper(GT_RSZ, TYP_INT);
            }

            return new IntConstSelectOper(GT_NONE, default);
        }
#endif

        public OptIfConversionDsc(Compiler compiler, BasicBlock startBlock)
        {
            _compiler = compiler;
            _startBlock = startBlock;
        }

        private bool HasElseBlock => _startBlock.TrueTarget.GetUniquePred(_compiler) is not null;

        private bool IfConvertCheck()
        {
            if (!IfConvertCheckFlow())
            {
                return false;
            }

            if (!IfConvertCheckStmts(_startBlock.FalseTarget, ref _thenOperation))
            {
                _thenOperation = default;
                return false;
            }

            var thenNode = _thenOperation.Node!;
            _mainOper = thenNode.Oper;
            assert(_mainOper is GT_RETURN or GT_STORE_LCL_VAR);

            if (HasElseBlock)
            {
                if (!IfConvertCheckStmts(_startBlock.TrueTarget, ref _elseOperation))
                {
                    _elseOperation = default;
                    return false;
                }
            }
            else if (_startBlock.FirstStmt != _startBlock.LastStmt)
            {
                assert(_mainOper is GT_STORE_LCL_VAR);
                _ = IfConvertTryGetElseFromJtrueBlock(thenNode.AsLclVar(), ref _elseOperation);
            }

            if (_elseOperation.Block is not null)
            {
                assert(thenNode.Oper == _elseOperation.Node!.Oper);
                if ((_mainOper is GT_STORE_LCL_VAR) &&
                    (thenNode.AsLclVarCommon().LclNum != _elseOperation.Node.AsLclVarCommon().LclNum))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IfConvertCheckFlow()
        {
            var falseBlock = _startBlock.FalseTarget;
            var trueBlock = _startBlock.TrueTarget;

            if ((falseBlock.GetUniquePred(_compiler) is null) ||
                !BasicBlock.sameEHRegion(falseBlock, _startBlock))
            {
                return false;
            }

            _finalBlock = HasElseBlock ? trueBlock.UniqueSucc : trueBlock;
            if (HasElseBlock && !BasicBlock.sameEHRegion(trueBlock, _startBlock))
            {
                return false;
            }

            if ((_finalBlock is null) &&
                ((falseBlock.Kind is not BBJ_RETURN) || (trueBlock.Kind is not BBJ_RETURN)))
            {
                return false;
            }

            return falseBlock.UniqueSucc == _finalBlock;
        }

        private bool IfConvertCheckStmts(BasicBlock block, ref IfConvertOperation operation)
        {
            var found = false;
            foreach (var stmt in block.Statements)
            {
                var tree = stmt.RootNode;
                if (tree.Oper is GT_STORE_LCL_VAR or GT_RETURN)
                {
                    if (found || !(varTypeIsIntegralOrI(tree.Type) || varTypeIsFloating(tree.Type)))
                    {
                        return false;
                    }

                    var source = tree.AsUnOp().Op1;
                    if ((source.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                    {
                        return false;
                    }

                    if (((_cond.Flags & GTF_ORDER_SIDEEFF) != 0) &&
                        !source.IsInvariant && !source.Oper.IsLocal)
                    {
                        return false;
                    }

                    found = true;
                    operation.Block = block;
                    operation.Stmt = stmt;
                    operation.Node = tree;
                }
                else if (tree.Oper is not GT_NOP)
                {
                    return false;
                }
            }

            return found;
        }

        private bool IfConvertTryGetElseFromJtrueBlock(GenTreeLclVar thenStore, ref IfConvertOperation operation)
        {
            assert(!HasElseBlock);
            var local = thenStore.LclNum;
            if (_compiler.lvaGetDesc(local).IsAddressExposed)
            {
                return false;
            }

            assert((thenStore.Data.Flags & GTF_SIDE_EFFECT) == 0);
            if (_compiler.gtTreeHasLocalRead(thenStore.Data, local))
            {
                return false;
            }

            var budget = 8;
            var hasEhSuccs = _startBlock.HasPotentialEHSuccs(_compiler);
            var last = _startBlock.LastStmt!;
            var stmt = last;
            do
            {
                if (budget-- <= 0)
                {
                    break;
                }

                var tree = stmt.RootNode;
                if (tree.Oper is GT_STORE_LCL_VAR)
                {
                    var previous = tree.AsLclVar();
                    if (previous.LclNum == local)
                    {
                        if ((previous.Flags & GTF_VAR_EXPLICIT_INIT) != 0)
                        {
                            return false;
                        }

                        if (previous.Data.IsInvariant)
                        {
                            operation.Block = _startBlock;
                            operation.Stmt = stmt;
                            operation.Node = tree;
                            return true;
                        }

                        return false;
                    }
                }

                if (((tree.Flags & GTF_EXCEPT) != 0) && hasEhSuccs)
                {
                    break;
                }

                if (_compiler.gtTreeHasLocalRead(tree, local) ||
                    _compiler.gtTreeHasLocalStore(tree, local))
                {
                    break;
                }

                stmt = stmt.PrevStmt!;
            }
            while (stmt != last);

            return false;
        }

#if DEBUG
        private void IfConvertDump()
        {
            _compiler.fgDumpBlock(_startBlock);
            if (_startBlock.Kind is BBJ_COND)
            {
                JITDUMP("\n------------------------------------");
                _compiler.fgDumpStmtTree(_thenOperation.Block!, _thenOperation.Stmt!);
                if (_elseOperation.Block is not null)
                {
                    _compiler.fgDumpStmtTree(_elseOperation.Block, _elseOperation.Stmt!);
                }
                JITDUMP("------------------------------------\n");
            }
        }
#endif

        public bool optIfConvert(int[] reachabilityBudget)
        {
            if ((reachabilityBudget[0] <= 0) || (_startBlock.FirstStmt is null))
            {
                return false;
            }

            var last = _startBlock.LastStmt!.RootNode;
            if (last.Oper is not GT_JTRUE)
            {
                return false;
            }

            _cond = last.AsUnOp().Op1;
            assert(_cond.Oper.IsCompare);
            if (!IfConvertCheck())
            {
                return false;
            }

#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"JTRUE block is {FMT_BB(_startBlock.bbNum)}. ");
                JITDUMP($"Statement {FMT_STMT(_thenOperation.Stmt!.Id)} (Then) ");
                if (_elseOperation.Block is not null)
                {
                    JITDUMP($"and {FMT_STMT(_elseOperation.Stmt!.Id)} (Else) ");
                }
                JITDUMP("can be expressed as SELECT:\n");
                IfConvertDump();
            }
#endif

            if (!_compiler.compStressCompile(Compiler.STRESS_IF_CONVERSION_COST, 25))
            {
                int thenCost;
                var elseCost = 0;
                if (_mainOper is GT_STORE_LCL_VAR)
                {
                    thenCost = _thenOperation.Node!.AsLclVar().Data.CostEx +
                        (_compiler.gtIsLikelyRegVar(_thenOperation.Node) ? 0 : 2);
                    if (HasElseBlock)
                    {
                        elseCost = _elseOperation.Node!.AsLclVar().Data.CostEx +
                            (_compiler.gtIsLikelyRegVar(_elseOperation.Node) ? 0 : 2);
                    }
                }
                else
                {
                    assert(_mainOper is GT_RETURN && HasElseBlock);
                    thenCost = _thenOperation.Node!.AsUnOp().Op1.CostEx;
                    elseCost = _elseOperation.Node!.AsUnOp().Op1.CostEx;
                }

                if ((thenCost > 7) || (elseCost > 7))
                {
                    JITDUMP($"Skipping if-conversion that will evaluate RHS unconditionally at costs {thenCost},{elseCost}\n");
                    return false;
                }
            }

            if (!_compiler.compStressCompile(Compiler.STRESS_IF_CONVERSION_INNER_LOOPS, 25))
            {
                if (_startBlock.getBBWeight(_compiler) > BB_UNITY_WEIGHT * 1.05)
                {
                    JITDUMP("Skipping if-conversion inside loop (via weight)\n");
                    return false;
                }

                var reachability = _compiler.optReachableWithBudget(
                    _finalBlock, _startBlock, null, reachabilityBudget);
                if (reachability is Compiler.ReachabilityResult.Reachable)
                {
                    JITDUMP("Skipping if-conversion inside loop (via reachability)\n");
                    return false;
                }
                if (reachability is Compiler.ReachabilityResult.BudgetExceeded)
                {
                    JITDUMP("Skipping if-conversion since we ran out of reachability budget\n");
                    return false;
                }
            }

            GenTree selectTrueInput;
            GenTree selectFalseInput;
            if (_mainOper is GT_STORE_LCL_VAR)
            {
                var thenStore = _thenOperation.Node!.AsLclVar();
                selectFalseInput = thenStore.Data;
                selectTrueInput = _elseOperation.Block is null
                    ? _compiler.gtNewLclvNode(thenStore.Type, thenStore.LclNum)
                    : _elseOperation.Node!.AsLclVar().Data;
            }
            else
            {
                assert(_mainOper is GT_RETURN && _elseOperation.Block is not null);
                assert(_thenOperation.Node!.Type == _elseOperation.Node!.Type);
                selectTrueInput = _elseOperation.Node.AsUnOp().Op1;
                selectFalseInput = _thenOperation.Node.AsUnOp().Op1;
            }

            var type = _thenOperation.Node!.Type.ActualType;
            GenTree select = new GenTreeConditional(GT_SELECT, type, _cond, selectTrueInput, selectFalseInput);
            select.AddAllEffectsFlags(_cond);
            select.AddAllEffectsFlags(selectTrueInput);
            select.AddAllEffectsFlags(selectFalseInput);

#if DEBUG
            JITDUMP("\nSELECT created:\n");
            if (_compiler.verbose)
            {
                _compiler.gtDispTree(select);
            }
#endif

            var optimized = TryOptimizeSelect(select.AsConditional());
            if (optimized is not null)
            {
                select = optimized;
#if DEBUG
                JITDUMP("\nSELECT after optimizations:\n");
                if (_compiler.verbose)
                {
                    _compiler.gtDispTree(select);
                }
#endif
            }

            if (select.Oper is GT_SELECT)
            {
                if (varTypeIsFloating(select.Type))
                {
                    JITDUMP("Abort: SELECT of type float\n");
                    return true;
                }
#if TARGET_RISCV64
                // RISC-V can lower integer SELECT only when Zicond is available; otherwise it must keep
                // the branchy form.
                var canCodegenSelect = _compiler.compOpportunisticallyDependsOn(InstructionSet_Zicond) &&
                    varTypeIsIntegralOrI(select.Type);
                if (!canCodegenSelect)
                {
                    JITDUMP("Skipping if-conversion that could not be optimized to ordinary operations\n");
                    return true;
                }

                var selectNode = select.AsConditional();
                var selectTrue = selectNode.Op1;
                var selectFalse = selectNode.Op2;
                var selectTrueIsLocalNoOp = selectTrue.Oper is GT_LCL_VAR;
                var selectFalseIsLocalNoOp = selectFalse.Oper is GT_LCL_VAR;
                var selectTrueIsSmallImm = selectTrue.Oper.IsIntegralConst &&
                    Emitter.isValidSimm12(unchecked((nint)selectTrue.AsIntConCommon().IntegralValue));
                var selectFalseIsSmallImm = selectFalse.Oper.IsIntegralConst &&
                    Emitter.isValidSimm12(unchecked((nint)selectFalse.AsIntConCommon().IntegralValue));

                if ((selectTrueIsLocalNoOp && selectFalseIsSmallImm) ||
                    (selectTrueIsSmallImm && selectFalseIsLocalNoOp))
                {
                    JITDUMP("Skipping if-conversion: branchy form fits in ~2 insns; Zicond would need 5+\n");
                    return true;
                }
#elif !TARGET_64BIT
            if (select.Type is TYP_LONG)
            {
                JITDUMP("Abort: SELECT of type Long on 32-bit system\n");
                return true;
            }
#endif
            }

            var thenNode = _thenOperation.Node!;
            thenNode.AddAllEffectsFlags(select);
            if (_mainOper is GT_STORE_LCL_VAR)
            {
                thenNode.AsLclVar().DataRef = select;
            }
            else
            {
                thenNode.AsUnOp().Op1 = select;
            }

            _ = _compiler.gtSetEvalOrder(thenNode);
            _compiler.fgSetStmtSeq(_thenOperation.Stmt!);

            _compiler.fgInsertStmtBefore(_startBlock, _startBlock.LastStmt!, _thenOperation.Stmt!);
            _compiler.fgRemoveStmt(_startBlock, _startBlock.LastStmt!);
            _thenOperation.Block!.FirstStmt = null;

            var falseBlock = _startBlock.FalseTarget;
            var trueBlock = _startBlock.TrueTarget;
            var hasElseBlock = HasElseBlock;
            if (_mainOper is GT_RETURN)
            {
                _startBlock.SetKindAndTargetEdge(BBJ_RETURN, null);
            }
            else
            {
                var newEdge = hasElseBlock
                    ? _compiler.fgAddRefPred(_finalBlock!, _startBlock)
                    : _startBlock.TrueEdge;
                _startBlock.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);
            }
            assert(_startBlock.UniqueSucc == _finalBlock);

            RemoveBlock(falseBlock);
            if (_elseOperation.Block is not null)
            {
                if (hasElseBlock)
                {
                    RemoveBlock(trueBlock);
                }
                else
                {
                    _compiler.fgRemoveStmt(_startBlock, _elseOperation.Stmt!);
                }
            }

#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP("\nAfter if conversion\n");
                IfConvertDump();
            }
#endif
            return true;
        }

        private void RemoveBlock(BasicBlock block)
        {
            block.bbWeight = BB_ZERO_WEIGHT;
            _ = _compiler.fgRemoveAllRefPreds(block, _startBlock);
            _ = _compiler.fgRemoveBlock(block, unreachable: true);
        }

        private GenTree? TryOptimizeSelect(GenTreeConditional select)
        {
            var optimized = _compiler.gtFoldExprConditional(select);
            if (optimized.Oper is not GT_SELECT)
            {
                return optimized;
            }

            return TrySelectToCnsOpCond(select)
                ?? TrySelectToLclOpCond(select)
                ?? TrySelectToCondOpLcl(select);
        }

        private GenTree? TrySelectToCnsOpCond(GenTreeConditional select)
        {
            var cond = select.Cond;
            var trueInput = select.Op1;
            var falseInput = select.Op2;
            if (!cond.Oper.IsCompare || !trueInput.Oper.IsIntegralConst ||
                !falseInput.Oper.IsIntegralConst)
            {
                return null;
            }

            var trueValue = trueInput.AsIntConCommon().IntegralValue;
            var falseValue = falseInput.AsIntConCommon().IntegralValue;
            if (((trueValue == 1) && (falseValue == 0)) ||
                ((trueValue == 0) && (falseValue == 1)))
            {
                var result = trueValue == 1 ? cond : _compiler.gtReverseCond(cond);
                if (result.Type != select.Type)
                {
                    result = _compiler.gtNewCastNode(select.Type, result, true, select.Type);
                }
                return result;
            }

            if (trueValue == falseValue)
            {
                return _compiler.gtWrapWithSideEffects(trueInput, cond);
            }

#if TARGET_RISCV64
            var isCondReversed = false;
            var selectOper = MatchIntConstSelectValues(trueValue, falseValue);
            if (!selectOper.IsMatched)
            {
                isCondReversed = true;
                selectOper = MatchIntConstSelectValues(falseValue, trueValue);
            }

            if (selectOper.IsMatched)
            {
                var left = isCondReversed ? trueInput : falseInput;
                var right = isCondReversed ? _compiler.gtReverseCond(cond) : cond;
                if (selectOper.BitIndex > 0)
                {
                    assert(selectOper.Oper is GT_LSH);
                    left.AsIntConCommon().SetValueTruncating(selectOper.BitIndex);
                    var temp = left;
                    left = right;
                    right = temp;
                }

                return _compiler.gtNewBinaryNode(selectOper.Oper, selectOper.Type, left, right);
            }
#endif

            return null;
        }

        private GenTreeOp? TrySelectToLclOpCond(GenTreeConditional select)
        {
#if TARGET_RISCV64
            var cond = select.Cond;
            var oper = select.Op1;
            var lcl = select.Op2;
            if (!cond.Oper.IsCompare)
            {
                return null;
            }

            var isCondReversed = !lcl.Oper.IsAnyLocal;
            if (isCondReversed)
            {
                (oper, lcl) = (lcl, oper);
            }

            if (lcl.Oper.IsAnyLocal &&
                ((oper.Oper is GT_ADD or GT_OR or GT_XOR) || oper.Oper.IsShift))
            {
                var lcl2 = oper.AsOp().Op1;
                var one = oper.AsOp().Op2;
                if (oper.Oper.IsCommutative && !one.Oper.IsIntegralConst)
                {
                    (lcl2, one) = (one, lcl2);
                }

                var isDecrement = oper.Oper is GT_ADD && one.IsIntegralConst(-1);
                if (one.IsIntegralConst(1) || isDecrement)
                {
                    var localNumber = lcl.AsLclVarCommon().LclNum;
                    if ((lcl2.Oper is GT_LCL_VAR) && (lcl2.AsLclVar().LclNum == localNumber))
                    {
                        var result = oper.AsOp();
                        result.Op1 = lcl2;
                        result.Op2 = isCondReversed ? _compiler.gtReverseCond(cond) : cond;
                        if (isDecrement)
                        {
                            result.SetOper(GT_SUB);
                        }

                        result.Flags |= cond.Flags & GTF_ALL_EFFECT;
                        return result;
                    }
                }
            }
#endif
            return null;
        }

        private GenTreeOp? TrySelectToCondOpLcl(GenTreeConditional select)
        {
#if TARGET_RISCV64
            var cond = select.Cond;
            var oper = select.Op1;
            var zero = select.Op2;
            if (!cond.Oper.IsCompare)
            {
                return null;
            }

            var isCondReversed = !zero.Oper.IsIntegralConst;
            if (isCondReversed)
            {
                (oper, zero) = (zero, oper);
            }

            if (zero.IsIntegralConst(0) && oper.OperIs(GT_AND, GT_LSH))
            {
                var one = oper.AsOp().Op1;
                var expr = oper.AsOp().Op2;
                if (oper.Oper.IsCommutative && !one.Oper.IsIntegralConst)
                {
                    (one, expr) = (expr, one);
                }

                if (one.IsIntegralConst(1))
                {
                    var result = oper.AsOp();
                    result.Op1 = isCondReversed ? _compiler.gtReverseCond(cond) : cond;
                    result.Op2 = expr;
                    result.Flags |= cond.Flags & GTF_ALL_EFFECT;
                    return result;
                }
            }
#endif
            return null;
        }
    }
}
