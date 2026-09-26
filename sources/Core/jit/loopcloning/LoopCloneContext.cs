// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed class LoopCloneContext
{
    public const double FastPathWeightScaleFactor = 0.99;
    public const double SlowPathWeightScaleFactor = 1.0 - FastPathWeightScaleFactor;

    private readonly List<LcOptInfo>?[] _optInfo;
    private readonly List<LC_Condition>?[] _conditions;
    private readonly List<LC_Array>?[] _arrayDerefs;
    private readonly List<LC_Ident>?[] _objDerefs;
    private readonly List<List<LC_Condition>>?[] _blockConditions;
    private readonly NaturalLoopIterInfo?[] _iterInfo;

    public LoopCloneContext(int loopCount)
    {
        _optInfo = new List<LcOptInfo>?[loopCount];
        _conditions = new List<LC_Condition>?[loopCount];
        _arrayDerefs = new List<LC_Array>?[loopCount];
        _objDerefs = new List<LC_Ident>?[loopCount];
        _blockConditions = new List<List<LC_Condition>>?[loopCount];
        _iterInfo = new NaturalLoopIterInfo?[loopCount];
    }

    public List<LcOptInfo> EnsureLoopOptInfo(int loopNum) => _optInfo[loopNum] ??= [];

    public List<LcOptInfo>? GetLoopOptInfo(int loopNum) => _optInfo[loopNum];

    public void CancelLoopOptInfo(int loopNum)
    {
        JITDUMP($"Cancelling loop cloning for loop L{loopNum:D2}\n");
        _optInfo[loopNum] = null;
        if (_conditions[loopNum] is not null)
        {
            _conditions[loopNum]!.Clear();
            _conditions[loopNum] = null;
        }
    }

    public List<LC_Condition> EnsureConditions(int loopNum) => _conditions[loopNum] ??= [];

    public List<LC_Condition>? GetConditions(int loopNum) => _conditions[loopNum];

    public List<LC_Array> EnsureArrayDerefs(int loopNum) => _arrayDerefs[loopNum] ??= [];

    public List<LC_Ident> EnsureObjDerefs(int loopNum) => _objDerefs[loopNum] ??= [];

    public bool HasBlockConditions(int loopNum)
    {
        var levels = _blockConditions[loopNum];
        if (levels is null)
        {
            return false;
        }
        foreach (var level in levels)
        {
            if (level.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

#if DEBUG
    public void PrintBlockConditions(int loopNum)
    {
        jitprintf("Block conditions:\n");
        var levels = _blockConditions[loopNum];
        if (levels is null || levels.Count == 0)
        {
            jitprintf("No block conditions\n");
            return;
        }
        for (var i = 0; i < levels.Count; i++)
        {
            PrintBlockLevelConditions(i, levels[i]);
        }
    }

    public void PrintBlockLevelConditions(int level, List<LC_Condition> conditions)
    {
        jitprintf($"{level} = ");
        for (var i = 0; i < conditions.Count; i++)
        {
            if (i != 0)
            {
                jitprintf(" && ");
            }
            jitprintf("(");
            conditions[i].Print();
            jitprintf(")");
        }
        jitprintf("\n");
    }

    public void PrintConditions(int loopNum)
    {
        var conditions = _conditions[loopNum];
        if (conditions is null)
        {
            jitprintf("NO conditions");
            return;
        }
        if (conditions.Count == 0)
        {
            jitprintf("Conditions were optimized away! Will always take cloned path.");
            return;
        }
        for (var i = 0; i < conditions.Count; i++)
        {
            if (i != 0)
            {
                jitprintf(" && ");
            }
            jitprintf("(");
            conditions[i].Print();
            jitprintf(")");
        }
    }
#endif

    public List<List<LC_Condition>> GetBlockConditions(int loopNum)
    {
        assert(HasBlockConditions(loopNum));
        return _blockConditions[loopNum]!;
    }

    public List<List<LC_Condition>> EnsureBlockConditions(int loopNum, int condBlocks)
    {
        var levels = _blockConditions[loopNum] ??= [];
        while (levels.Count < condBlocks)
        {
            levels.Add([]);
        }
        return levels;
    }

    public NaturalLoopIterInfo? GetLoopIterInfo(int loopNum) => _iterInfo[loopNum];

    public void SetLoopIterInfo(int loopNum, NaturalLoopIterInfo info) => _iterInfo[loopNum] = info;

    public void EvaluateConditions(int loopNum, out bool allTrue, out bool anyFalse, bool verbose = false)
    {
        allTrue = true;
        anyFalse = false;
        var conditions = _conditions[loopNum]!;
        assert(conditions.Count > 0);
        JITDUMP($"Evaluating {conditions.Count} loop cloning conditions for loop L{loopNum:D2}\n");
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
#if DEBUG
            if (verbose)
            {
                jitprintf($"Considering condition {i}: (");
                condition.Print();
            }
#endif
            if (condition.Evaluates(out var result))
            {
                JITDUMP($") evaluates to {dspBool(result)}\n");
                if (!result)
                {
                    anyFalse = true;
                    break;
                }
            }
            else
            {
                JITDUMP("), could not be evaluated\n");
                allTrue = false;
            }
        }
        JITDUMP($"Evaluation result allTrue = {dspBool(allTrue)}, anyFalse = {dspBool(anyFalse)}\n");
    }

    private void OptimizeConditions(List<LC_Condition> conditions)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i].Evaluates(out var result))
            {
                if (result)
                {
                    conditions.RemoveAt(i);
                    i--;
                    continue;
                }
                // Native uses the condition index here; main conditions are normally rejected by EvaluateConditions first.
                CancelLoopOptInfo(i);
                break;
            }

            for (var j = i + 1; j < conditions.Count; j++)
            {
                if (conditions[i].Combines(conditions[j], out var combined))
                {
                    conditions.RemoveAt(j);
                    conditions[i] = combined;
                    i = -1;
                    break;
                }
            }
        }
#if DEBUG
        for (var i = 0; i < conditions.Count; i++)
        {
            for (var j = 0; j < conditions.Count; j++)
            {
                assert((i == j) || !conditions[i].Combines(conditions[j], out _));
            }
        }
#endif
    }

    public void OptimizeConditions(int loopNum, bool verbose = false)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf("Before optimizing cloning conditions\n\t");
            PrintConditions(loopNum);
            jitprintf("\n");
        }
#endif
        OptimizeConditions(_conditions[loopNum]!);
#if DEBUG
        if (verbose)
        {
            jitprintf("After optimizing cloning conditions\n\t");
            PrintConditions(loopNum);
            jitprintf("\n");
        }
#endif
    }

    public void OptimizeBlockConditions(int loopNum, bool verbose = false)
    {
        if (!HasBlockConditions(loopNum))
        {
            return;
        }
        foreach (var level in _blockConditions[loopNum]!)
        {
            OptimizeConditions(level);
        }
#if DEBUG
        if (verbose)
        {
            jitprintf("After optimizing block-level cloning conditions\n\t");
            PrintConditions(loopNum);
            jitprintf("\n");
        }
#endif
    }

    public BasicBlock CondToStmtInBlock(Compiler compiler, List<LC_Condition> conditions,
        BasicBlock slowPreheader, BasicBlock insertAfter, int totalCondsInChain)
    {
        noway_assert(conditions.Count > 0);
        assert(totalCondsInChain >= conditions.Count);
        var fastLikelihoodPerBlock = Math.Exp(Math.Log(FastPathWeightScaleFactor) / totalCondsInChain);

        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var newBlock = compiler.fgNewBBafter(BBJ_COND, insertAfter, extendRegion: true);
            newBlock.inheritWeight(insertAfter);

            JITDUMP($"Adding {FMT_BB(newBlock.bbNum)} -> {FMT_BB(slowPreheader.bbNum)}\n");
            var trueEdge = compiler.fgAddRefPred(slowPreheader, newBlock);
            newBlock.TrueEdge = trueEdge;
            trueEdge.Likelihood = 1 - fastLikelihoodPerBlock;

            if (insertAfter.Kind is BBJ_COND)
            {
                JITDUMP($"Adding {FMT_BB(insertAfter.bbNum)} -> {FMT_BB(newBlock.bbNum)}\n");
                var falseEdge = compiler.fgAddRefPred(newBlock, insertAfter);
                insertAfter.FalseEdge = falseEdge;
                falseEdge.Likelihood = fastLikelihoodPerBlock;
            }

            JITDUMP($"Adding conditions {i} to {FMT_BB(newBlock.bbNum)}\n");
            var comparison = condition.ToGenTree(compiler, newBlock, invert: true);
            comparison.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
            var branch = compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, comparison);
            compiler.fgInsertStmtAtEnd(newBlock, compiler.fgNewStmtFromTree(branch));
            insertAfter = newBlock;
        }
        return insertAfter;
    }
}
