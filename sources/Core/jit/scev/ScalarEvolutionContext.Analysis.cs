// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class ScalarEvolutionContext
{
    private const int AnalysisMaxDepth = 64;

    private readonly Compiler _compiler;
    private FlowGraphNaturalLoop? _loop;
    private readonly Dictionary<GenTree, Scev?> _cache = [with(ReferenceEqualityComparer.Instance)];
    private readonly Dictionary<GenTree, Scev?> _ephemeralCache = [with(ReferenceEqualityComparer.Instance)];
    private bool _usingEphemeralCache;

    public ScalarEvolutionContext(Compiler compiler)
    {
        _compiler = compiler;
    }

    public void ResetForLoop(FlowGraphNaturalLoop loop)
    {
        _loop = loop;
        _cache.Clear();
    }

    public ScevConstant NewConstant(var_types type, long value) => new(type, value);

    public ScevLocal NewLocal(int lclNum, int ssaNum)
        => new(_compiler.lvaGetDesc(lclNum).Type.ActualType, lclNum, ssaNum);

    public ScevUnop NewExtension(ScevOper oper, var_types targetType, Scev op)
    {
        assert(oper is ScevOper.ZeroExtend or ScevOper.SignExtend);
        return new ScevUnop(oper, targetType, op);
    }

    public ScevBinop NewBinop(ScevOper oper, Scev op1, Scev op2)
    {
        var resultType = op1.Type;
        if (oper is ScevOper.Add)
        {
            if (varTypeIsGC(op1.Type))
            {
                assert(op2.Type is TYP_I_IMPL);
                resultType = TYP_BYREF;
            }
            else if (varTypeIsGC(op2.Type))
            {
                assert(op1.Type is TYP_I_IMPL);
                resultType = TYP_BYREF;
            }
            else
            {
                assert(op1.Type == op2.Type);
            }
        }

        return new ScevBinop(oper, resultType, op1, op2);
    }

    public ScevAddRec NewAddRec(Scev start, Scev step)
    {
        var loop = _loop ?? throw new InvalidOperationException("Scalar evolution requires a loop.");
        return new ScevAddRec(start.Type, start, step, loop);
    }

    private Scev? CreateSimpleInvariantScev(GenTree tree)
    {
        var loop = _loop ?? throw new InvalidOperationException("Scalar evolution requires a loop.");
        if (tree.Oper is GT_CNS_INT or GT_CNS_LNG)
        {
            return CreateScevForConstant(tree.AsIntConCommon());
        }

        if (tree.Oper is GT_LCL_VAR && tree.AsLclVarCommon().HasSsaName)
        {
            var local = tree.AsLclVarCommon();
            ref readonly var ssaDsc = ref _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
            if ((ssaDsc.Block is null) || !loop.ContainsBlock(ssaDsc.Block))
            {
                return NewLocal(local.LclNum, local.SsaNum);
            }
        }

        return null;
    }

    private ScevConstant? CreateScevForConstant(GenTreeIntConCommon tree)
    {
        if (tree.IsIconHandle() || tree.Type is not (TYP_INT or TYP_LONG))
        {
            return null;
        }

        return NewConstant(tree.Type, tree.IntegralValue);
    }

    private Scev? AnalyzeNew(BasicBlock block, GenTree tree, int depth)
    {
        var loop = _loop ?? throw new InvalidOperationException("Scalar evolution requires a loop.");
        if (!varTypeIsIntegralOrI(tree.Type))
        {
            return null;
        }

        switch (tree.Oper)
        {
            case GT_CNS_INT:
            case GT_CNS_LNG:
            {
                return CreateScevForConstant(tree.AsIntConCommon());
            }

            case GT_LCL_VAR:
            case GT_PHI_ARG:
            {
                var local = tree.AsLclVarCommon();
                if (!local.HasSsaName)
                {
                    return null;
                }

                assert(_compiler.lvaGetDesc(local.LclNum).lvInSsa);
                ref var dsc = ref _compiler.lvaGetDesc(local.LclNum);
                ref readonly var ssaDsc = ref dsc.GetPerSsaData(local.SsaNum);
                if ((tree.Type != dsc.Type) || varTypeIsSmall(tree.Type))
                {
                    return null;
                }

                if ((ssaDsc.Block is null) || !loop.ContainsBlock(ssaDsc.Block))
                {
                    return NewLocal(local.LclNum, local.SsaNum);
                }

                var defNode = ssaDsc.DefNode;
                if (defNode is null)
                {
                    return null;
                }

                if (defNode.LclNum != local.LclNum)
                {
                    assert(dsc.lvIsStructField && defNode.LclNum == dsc.lvParentLcl);
                    return null;
                }

                return Analyze(ssaDsc.Block, defNode.Data, depth + 1);
            }

            case GT_PHI:
            {
                if (block != loop.Header)
                {
                    return null;
                }

                var phi = tree.AsPhi();
                GenTreePhiArg? enterSsa = null;
                GenTreePhiArg? backedgeSsa = null;
                foreach (var use in phi.Uses)
                {
                    var phiArg = use.Node.AsPhiArg();
                    if (loop.ContainsBlock(phiArg.PredBB))
                    {
                        if ((backedgeSsa is not null) && (backedgeSsa.SsaNum != phiArg.SsaNum))
                        {
                            return null;
                        }

                        backedgeSsa = phiArg;
                    }
                    else
                    {
                        if ((enterSsa is not null) && (enterSsa.SsaNum != phiArg.SsaNum))
                        {
                            return null;
                        }

                        enterSsa = phiArg;
                    }
                }

                if ((enterSsa is null) || (backedgeSsa is null))
                {
                    return null;
                }

                var enterScev = NewLocal(enterSsa.LclNum, enterSsa.SsaNum);
                ref var dsc = ref _compiler.lvaGetDesc(enterSsa.LclNum);
                ref readonly var ssaDsc = ref dsc.GetPerSsaData(backedgeSsa.SsaNum);
                var defNode = ssaDsc.DefNode;
                if (defNode is null)
                {
                    return null;
                }

                if (defNode.LclNum != enterSsa.LclNum)
                {
                    assert(dsc.lvIsStructField && defNode.LclNum == dsc.lvParentLcl);
                    return null;
                }

                assert(ssaDsc.Block is not null);
                var simpleAddRec = CreateSimpleAddRec(phi, enterScev, ssaDsc.Block, defNode.Data);
                if (simpleAddRec is not null)
                {
                    return simpleAddRec;
                }

                var symbolicAddRec = NewConstant(phi.Type, 0xdeadbeef);
                _ephemeralCache.Add(phi, symbolicAddRec);
                Scev? result;
                if (_usingEphemeralCache)
                {
                    result = Analyze(ssaDsc.Block, defNode.Data, depth + 1);
                }
                else
                {
                    _usingEphemeralCache = true;
                    try
                    {
                        result = Analyze(ssaDsc.Block, defNode.Data, depth + 1);
                    }
                    finally
                    {
                        _usingEphemeralCache = false;
                        _ephemeralCache.Clear();
                    }
                }

                return result is null ? null : MakeAddRecFromRecursiveScev(enterScev, result, symbolicAddRec);
            }

            case GT_CAST:
            {
                var cast = tree.AsCast();
                if (cast.CastType is not (TYP_LONG or TYP_ULONG))
                {
                    return null;
                }

                var operand = Analyze(block, cast.CastOp, depth + 1);
                return operand is null ? null :
                    NewExtension(cast.IsUnsigned ? ScevOper.ZeroExtend : ScevOper.SignExtend, TYP_LONG, operand);
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            case GT_LSH:
            {
                var op1 = Analyze(block, tree.AsOp().Op1, depth + 1);
                if (op1 is null)
                {
                    return null;
                }

                var op2 = Analyze(block, tree.AsOp().Op2, depth + 1);
                if (op2 is null)
                {
                    return null;
                }

                ScevOper oper;
                switch (tree.Oper)
                {
                    case GT_ADD:
                    {
                        oper = ScevOper.Add;
                        break;
                    }

                    case GT_SUB:
                    {
                        if (varTypeIsGC(op2.Type))
                        {
                            return null;
                        }

                        oper = ScevOper.Add;
                        op2 = NewBinop(ScevOper.Mul, op2, NewConstant(op2.Type, -1));
                        break;
                    }

                    case GT_MUL:
                    {
                        oper = ScevOper.Mul;
                        break;
                    }

                    case GT_LSH:
                    {
                        oper = ScevOper.Lsh;
                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException($"Unexpected operator {tree.Oper}.");
                    }
                }

                return NewBinop(oper, op1, op2);
            }

            case GT_COMMA:
            {
                return Analyze(block, tree.AsOp().Op2, depth + 1);
            }

            case GT_ARR_ADDR:
            {
                return Analyze(block, tree.AsArrAddr().Addr, depth + 1);
            }

            default:
            {
                return null;
            }
        }
    }

    private ScevAddRec? CreateSimpleAddRec(GenTreePhi headerPhi, ScevLocal start, BasicBlock stepDefBlock, GenTree stepDefData)
    {
        if (stepDefData.Oper is not GT_ADD)
        {
            return null;
        }

        var op1 = stepDefData.AsOp().Op1;
        var op2 = stepDefData.AsOp().Op2;
        GenTree? GetUseValue(GenTree value)
        {
            if (value.Oper is not GT_LCL_VAR || !value.AsLclVarCommon().HasSsaName)
            {
                return null;
            }

            var local = value.AsLclVarCommon();
            ref readonly var ssaDsc = ref _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
            return ssaDsc.DefNode?.Data;
        }

        GenTree stepTree;
        if (GetUseValue(op1) == headerPhi)
        {
            stepTree = op2;
        }
        else if (GetUseValue(op2) == headerPhi)
        {
            stepTree = op1;
        }
        else
        {
            return null;
        }

        var stepScev = CreateSimpleInvariantScev(stepTree);
        return stepScev is null ? null : NewAddRec(start, stepScev);
    }

    private static void ExtractAddOperands(ScevBinop binop, List<Scev> operands)
    {
        assert(binop.Oper is ScevOper.Add);
        if (binop.Op1 is ScevBinop { Oper: ScevOper.Add } first)
        {
            ExtractAddOperands(first, operands);
        }
        else
        {
            operands.Add(binop.Op1);
        }

        if (binop.Op2 is ScevBinop { Oper: ScevOper.Add } second)
        {
            ExtractAddOperands(second, operands);
        }
        else
        {
            operands.Add(binop.Op2);
        }
    }

    private ScevAddRec? MakeAddRecFromRecursiveScev(Scev start, Scev scev, Scev recursiveScev)
    {
        if (scev is not ScevBinop { Oper: ScevOper.Add } addition)
        {
            return null;
        }

        var addOperands = new List<Scev>();
        ExtractAddOperands(addition, addOperands);
        var numAppearances = 0;
        foreach (var addOperand in addOperands)
        {
            if (ReferenceEquals(addOperand, recursiveScev))
            {
                numAppearances++;
            }
            else if (addOperand.Visit(node => ReferenceEquals(node, recursiveScev)
                         ? ScevVisit.Abort : ScevVisit.Continue) is ScevVisit.Abort)
            {
                return null;
            }
        }

        if (numAppearances != 1)
        {
            return null;
        }

        Scev? step = null;
        foreach (var addOperand in addOperands)
        {
            if (ReferenceEquals(addOperand, recursiveScev))
            {
                continue;
            }

            step = step is null ? addOperand : NewBinop(ScevOper.Add, step, addOperand);
        }

        assert(step is not null);
        return NewAddRec(start, step);
    }

    public Scev? Analyze(BasicBlock block, GenTree tree)
    {
        if (_loop is null)
        {
            throw new InvalidOperationException("Scalar evolution requires a loop.");
        }

        return Analyze(block, tree, 0);
    }

    private Scev? Analyze(BasicBlock block, GenTree tree, int depth)
    {
        if (_cache.TryGetValue(tree, out var result) ||
            (_usingEphemeralCache && _ephemeralCache.TryGetValue(tree, out result)))
        {
            return result;
        }

        if (depth >= AnalysisMaxDepth)
        {
            return null;
        }

        result = AnalyzeNew(block, tree, depth);
        if (_usingEphemeralCache)
        {
            _ephemeralCache[tree] = result;
        }
        else
        {
            _cache.Add(tree, result);
        }

        return result;
    }
}
