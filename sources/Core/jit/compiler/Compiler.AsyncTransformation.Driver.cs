// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        public PhaseStatus Run()
        {
            var normalBlocks = new List<BasicBlock>();
            var tailBlocks = new List<BasicBlock>();
            var memberOffsets = new List<GenTree>();
            var awaits = FindAwaits(normalBlocks, tailBlocks, memberOffsets);

            if ((awaits.Normal + awaits.Tail) > 1)
            {
                CreateSharedReturnBB();
            }

            var result = PhaseStatus.MODIFIED_NOTHING;
            if (awaits.Tail > 0)
            {
                JITDUMP($"Found {awaits.Tail} tail awaits in {tailBlocks.Count} blocks\n");
                TransformTailAwaits(tailBlocks);
                _compiler.fgInvalidateDfsTree();
                if (awaits.Normal > 0)
                {
                    normalBlocks.Clear();
                    tailBlocks.Clear();
                    memberOffsets.Clear();
                    awaits = FindAwaits(normalBlocks, tailBlocks, memberOffsets);
                }

                result = PhaseStatus.MODIFIED_EVERYTHING;
            }

            JITDUMP($"Found {awaits.Normal} awaits in {normalBlocks.Count} blocks\n");
            if (awaits.Normal == 0)
            {
                assert(memberOffsets.Count == 0);
                if ((awaits.Tail > 0) && _compiler.MethodHasPatchpoint)
                {
                    CreateResumptionSwitch(null);
                    _compiler.fgInvalidateDfsTree();
                    result = PhaseStatus.MODIFIED_EVERYTHING;
                }

                return result;
            }

            _compiler.compSuspensionPoints = [];
            _compiler.compAsyncVars = [];
            _ = _compiler.eeGetAsyncInfo();
            _compiler._dfsTree ??= _compiler.fgComputeDfs();
            _compiler.lvaComputePreciseRefCounts(isRecompute: true, setSlotNumbers: false);
            new Liveness<AsyncLivenessPolicy>(_compiler).RunLIR();
#if DEBUG
            _compiler.mostRecentlyActivePhase = PHASE_ASYNC;
#endif
            VarSetOps.AssignNoCopy(_compiler, ref _compiler.compCurLife,
                VarSetOps.MakeEmpty(_compiler));
            _compiler._blockToEHPreds = null;

            var defaultValues = new AsyncDefaultValueAnalysis(_compiler);
            defaultValues.Run();
            var preservedValues = new AsyncPreservedValueAnalysis(_compiler);
            preservedValues.Run(normalBlocks);
            var analyses = new AsyncAnalysis(_compiler, defaultValues, preservedValues);
            var defs = new List<GenTree>();

            foreach (var initialBlock in normalBlocks)
            {
                assert(defs.Count == 0);
                var block = initialBlock;
                analyses.StartBlock(block);
                bool transformed;
                do
                {
                    transformed = false;
                    foreach (var tree in block)
                    {
                        _ = tree.VisitOperands(operand => {
                            if (operand.IsValue)
                            {
                                for (var i = defs.Count - 1; i >= 0; i--)
                                {
                                    if (ReferenceEquals(defs[i], operand))
                                    {
                                        defs[i] = defs[^1];
                                        defs.RemoveAt(defs.Count - 1);
                                        break;
                                    }
                                }
                            }

                            return GenTree.VisitResult.Continue;
                        });

                        if ((tree is GenTreeCall call) && call.IsAsync && !call.IsTailCall &&
                            !call.GetAsyncInfo().IsTailAwait)
                        {
                            block = Transform(block, call, defs, analyses);
                            defs.Clear();
                            transformed = true;
                            break;
                        }

                        analyses.Update(tree);
                        if (tree.IsValue && !tree.IsUnusedValue)
                        {
                            defs.Add(tree);
                        }
                    }
                }
                while (transformed);
            }

            if (_compiler.opts.IsOSR)
            {
                _reuseContinuationVar = _compiler.lvaGrabTemp(
                    false, "OSR reusable continuation");
                _compiler.lvaGetDesc(_reuseContinuationVar).Type = TYP_REF;
            }
            else
            {
                _reuseContinuationVar = _compiler.lvaAsyncContinuationArg;
            }

            var commonDef = FindAndRemoveCommonAsyncResumedDef();
            var layout = CreateResumptionsAndSuspensions(memberOffsets);
            CreateResumptionSwitch(commonDef);

            ResolveContinuationMemberOffsets(layout, memberOffsets.Count);

            _compiler.fgInvalidateDfsTree();
            if (_compiler.opts.OptimizationDisabled)
            {
                var trackedToVar = _compiler.lvaTrackedToVarNum ??
                    throw new InvalidOperationException("Async liveness has no tracked-local mapping");
                for (var i = 0; i < _compiler.lvaTrackedCount; i++)
                {
                    _compiler.lvaGetDesc(trackedToVar[i]).lvTracked = false;
                }

                _compiler.lvaCurEpoch++;
                _compiler.lvaTrackedCount = 0;
                _compiler.lvaTrackedCountInSizeTUnits = 0;
                foreach (var block in _compiler.Blocks)
                {
                    block.bbLiveIn = VarSetOps.UninitVal();
                    block.bbLiveOut = VarSetOps.UninitVal();
                    block.bbVarUse = VarSetOps.UninitVal();
                    block.bbVarDef = VarSetOps.UninitVal();
                }

                _compiler.fgBBVarSetsInited = false;
            }

            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        private void ResolveContinuationMemberOffsets(AsyncContinuationLayout layout, int expectedCount)
        {
            var replacedOffsets = 0;
            foreach (var block in _compiler.Blocks)
            {
                for (var member = block.FirstLIRNode; member is not null;)
                {
                    var next = member.Next;
                    if (member.Oper is GT_CONTINUATION_MEMBER_OFFSET)
                    {
                        var index = checked((int)member.AsVal().Val1);
                        if ((index < 0) || (index >= layout.ContinuationMemberOffsets.Count) ||
                            (layout.ContinuationMemberOffsets[index] < 0))
                        {
                            throw new InvalidOperationException("Unallocated continuation member offset");
                        }

                        var offset = OFFSETOF__CORINFO_Continuation__data -
                            SIZEOF__CORINFO_Object + layout.ContinuationMemberOffsets[index];
                        var constant = new GenTreeIntCon(member.Type, offset, null, member, NodeThreading.LIR);
                        block.ReplaceNode(member, constant);
                        replacedOffsets++;
                    }

                    member = next;
                }
            }

            if (replacedOffsets != expectedCount)
            {
                throw new InvalidOperationException("Continuation member offset missing from final LIR");
            }
        }

        private BasicBlock Transform(BasicBlock block, GenTreeCall call,
            List<GenTree> defs, AsyncAnalysis analyses)
        {
#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"Processing call [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}\n");
                JITDUMP($"  {defs.Count} live LIR edges\n");
                if (defs.Count != 0)
                {
                    var separator = "    ";
                    foreach (var def in defs)
                    {
                        JITDUMP($"{separator}[{def.TreeId:D6}] ({def.Type.Name})");
                        separator = ", ";
                    }

                    JITDUMP("\n");
                }
            }
#endif
            var resumeReachable = analyses.ResumeReachable;
            var mutated = VarSetOps.MakeCopy(_compiler, analyses.MutatedSinceResumption);
            JITDUMP($"  This suspension point is{(resumeReachable ? "" : " NOT")} resume-reachable\n");
#if DEBUG
            if (resumeReachable && _compiler.verbose)
            {
                JITDUMP($"  Locals mutated since previous resumption: {AsyncAnalysis.PrintVarSet(_compiler, mutated)}\n");
            }
#endif
            var builder = new AsyncContinuationLayoutBuilder(_compiler);
            CreateLiveSetForSuspension(block, call, defs, analyses, builder);
            BuildContinuation(block, call, ContinuationNeedsKeepAlive(analyses), builder);
            var definition = CanonicalizeCallDefinition(block, call, analyses);
            var reused = FindReusableSuspension(
                block, call, definition, builder, resumeReachable, mutated);
            if (reused is not null)
            {
                JITDUMP($"  Reused state {reused.Number}\n");
                var remainder = CreateCheckAndSuspendAfterCall(
                    block, call, definition, reused.SuspensionBB);
                HandleReusedSuspension(block, call);
                _compiler.Metrics.SuspensionPointsMerged++;
                JITDUMP("\n");
                return remainder;
            }

            var stateNumber = _states.Count;
            JITDUMP($"  Assigned state {stateNumber}\n");
            var suspension = CreateSuspensionBlock(block, stateNumber);
            var next = CreateCheckAndSuspendAfterCall(block, call, definition, suspension);
            var resumption = CreateResumptionBlock(next, stateNumber);
            _states.Add(new AsyncState(stateNumber, builder, block, call, definition,
                suspension, resumption, resumeReachable, mutated));
            JITDUMP("\n");
            return next;
        }
    }
}
