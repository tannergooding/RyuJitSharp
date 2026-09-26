// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System.Collections.Generic;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class CursorInfo(BasicBlock block, Statement stmt, GenTree tree, ScevAddRec? iv)
    {
        public BasicBlock Block = block;
        public Statement Stmt = stmt;
        public GenTree Tree = tree;
        public GenTree? AdvancedTree = tree;
        public ScevAddRec? IV = iv;
    }

    private sealed partial class StrengthReductionContext(
        Compiler compiler, ScalarEvolutionContext scevContext, FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        private readonly Compiler _compiler = compiler;
        private readonly ScalarEvolutionContext _scevContext = scevContext;
        private readonly FlowGraphNaturalLoop _loop = loop;
        private readonly PerLoopInfo _loopInfo = loopInfo;
        private readonly List<Scev> _backEdgeBounds = [];
        private SimplificationAssumptions _assumptions;
        private List<CursorInfo> _cursors1 = [];
        private List<CursorInfo> _cursors2 = [];
        private readonly List<CursorInfo> _intermediateIVStores = [];

        private bool StressProfitability()
            => _compiler.compStressCompile(STRESS_STRENGTH_REDUCTION_PROFITABILITY, 50);

        public bool TryStrengthReduce()
        {
            JITDUMP($"Considering L{_loop.Index:D2} for strength reduction...\n");
            if ((JitConfig.JitEnableStrengthReduction == 0) &&
                !_compiler.compStressCompile(STRESS_STRENGTH_REDUCTION, 50))
            {
                JITDUMP("  Disabled: no stress mode\n");
                return false;
            }

            InitializeSimplificationAssumptions();
            JITDUMP("  Considering primary IVs\n");
            var changed = false;
            for (var stmt = _loop.Header.FirstStmt;
                (stmt is not null) && stmt.IsPhiDefnStmt; stmt = stmt.NextStmt)
            {
                DISPSTMT(stmt);
                var primaryLocal = stmt.RootNode.AsLclVarCommon();
                var candidate = _scevContext.Analyze(_loop.Header, primaryLocal.Data);
                if (candidate is null)
                {
                    JITDUMP("  Could not analyze header PHI\n");
                    continue;
                }

                candidate = _scevContext.Simplify(candidate, _assumptions);
#if DEBUG
                JITDUMP("  => ");
                if (_compiler.verbose)
                {
                    candidate.Dump(_compiler);
                }
                JITDUMP("\n");
#endif
                if (candidate is not ScevAddRec primaryIV)
                {
                    JITDUMP("  Not an addrec\n");
                    continue;
                }
                if (_compiler.optLocalHasNonLoopUses(primaryLocal.LclNum, _loop, _loopInfo))
                {
                    JITDUMP("  Has non-loop uses\n");
                    continue;
                }
                if (!InitializeCursors(primaryLocal, primaryIV))
                {
                    continue;
                }

                var cursors = _cursors1;
                var nextCursors = _cursors2;
                var derivedLevel = 0;
                var currentIV = primaryIV;
                // Retain the last common derived IV while advancing every use toward its parent.
                while (true)
                {
                    JITDUMP($"  Advancing cursors to be {derivedLevel + 1}-derived\n");
                    AdvanceCursors(cursors, nextCursors);
                    if (!CheckAdvancedCursors(nextCursors, out var nextIV) || nextIV is null)
                    {
                        break;
                    }

#if DEBUG
                    JITDUMP("  Next IV is: ");
                    if (_compiler.verbose)
                    {
                        nextIV.Dump(_compiler);
                    }
                    JITDUMP("\n");
#endif
                    if (varTypeIsGC(nextIV.Type))
                    {
                        if (_loopInfo.HasSuspensionPoint(_loop))
                        {
                            JITDUMP("    Next IV computes a GC pointer in a loop with a suspension point. Bailing.\n");
                            break;
                        }
                        if (!StaysWithinManagedObject(nextCursors, nextIV))
                        {
                            JITDUMP("    Next IV computes a GC pointer that we cannot prove " +
                                "to be inside a managed object. Bailing.\n");
                            break;
                        }
                    }

                    ExpandStoredCursors(nextCursors, cursors);
                    derivedLevel++;
                    (cursors, nextCursors) = (nextCursors, cursors);
                    currentIV = nextIV;
                }
                if (derivedLevel <= 0)
                {
                    continue;
                }

#if DEBUG
                JITDUMP($"  All uses of primary IV V{primaryLocal.LclNum:D2} " +
                    $"are used to compute a {derivedLevel}-derived IV ");
                if (_compiler.verbose)
                {
                    currentIV.Dump(_compiler);
                }
                JITDUMP("\n");
#endif
                if (!StressProfitability())
                {
                    if (Scev.Equals(currentIV.Step, primaryIV.Step))
                    {
                        JITDUMP("    Skipping: Candidate has same step as primary IV\n");
                        continue;
                    }
                    if ((currentIV.Step.Type is TYP_LONG) && (primaryIV.Step.Type is TYP_INT) &&
                        currentIV.Step.GetConstantValue(_compiler, out var newStep) &&
                        primaryIV.Step.GetConstantValue(_compiler, out var primaryStep) &&
                        (int)newStep == (int)primaryStep)
                    {
                        JITDUMP("    Skipping: Candidate has same widened step as primary IV\n");
                        continue;
                    }
                }
                if (TryReplaceUsesWithNewPrimaryIV(cursors, currentIV))
                {
                    changed = true;
                    _loopInfo.Invalidate(_loop);
                }
            }
            return changed;
        }

        private void InitializeSimplificationAssumptions()
        {
            _compiler.optVisitBoundingExitingCondBlocks(_loop, exiting => {
                var count = _scevContext.ComputeExitNotTakenCount(exiting);
                if (count is not null)
                {
                    _backEdgeBounds.Add(count);
                }
            });
            _assumptions = new SimplificationAssumptions([.. _backEdgeBounds]);
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("  Bound on backedge taken count is ");
                if (_backEdgeBounds.Count == 0)
                {
                    jitprintf("<unknown>\n");
                }
                var prefix = _backEdgeBounds.Count > 1 ? "min(" : "";
                foreach (var bound in _backEdgeBounds)
                {
                    jitprintf(prefix);
                    bound.Dump(_compiler);
                }
                jitprintf(_backEdgeBounds.Count > 1 ? ")\n" : "\n");
            }
#endif
        }
    }
}
