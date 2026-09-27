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
        private unsafe void CreateResumptionSwitch(GenTreeLclVarCommon? commonResumedDef)
        {
            _compiler.fgCreateNewInitBB();
            var entry = _compiler.fgFirstBB ??
                throw new InvalidOperationException("Async method has no entry block");
            var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
            var nullValue = _compiler.gtNewNull();
            var nonNull = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, continuation, nullValue);
            var branch = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, nonNull);
            entry.InsertAtEnd(continuation);
            entry.InsertAtEnd(nullValue);
            entry.InsertAtEnd(nonNull);
            entry.InsertAtEnd(branch);

            FlowEdge resumeEdge;
            if (_states.Count == 0)
            {
                assert(_compiler.MethodHasPatchpoint);
                continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var address = LoadFromOffset(continuation,
                    OFFSETOF__CORINFO_Continuation__data, TYP_I_IMPL);
                resumeEdge = _compiler.fgAddRefPred(CreateOSRJumpBB(address), entry);
            }
            else if (_states.Count == 1)
            {
                resumeEdge = _compiler.fgAddRefPred(_states[0].ResumptionBB, entry);
                JITDUMP($"  Redirecting entry {FMT_BB(entry.bbNum)} directly to " +
                    $"{FMT_BB(_states[0].ResumptionBB.bbNum)} as it is the only resumption block\n");
            }
            else if (_states.Count == 2)
            {
                var conditional = _compiler.fgNewBBbefore(BBJ_COND,
                    _states[0].ResumptionBB, true);
                conditional.inheritWeightPercentage(entry, 0);
                var toZero = _compiler.fgAddRefPred(_states[0].ResumptionBB, conditional);
                var toOne = _compiler.fgAddRefPred(_states[1].ResumptionBB, conditional);
                conditional.SetCond(toOne, toZero);
                toZero.Likelihood = 0.5;
                toOne.Likelihood = 0.5;
                resumeEdge = _compiler.fgAddRefPred(conditional, entry);
                JITDUMP($"  Redirecting entry {FMT_BB(entry.bbNum)} to BBJ_COND " +
                    $"{FMT_BB(conditional.bbNum)} for resumption with 2 states\n");

                var resumedContinuation = _compiler.gtNewLclvNode(
                    TYP_REF, _compiler.lvaAsyncContinuationArg);
                var stateOffset = _compiler.info.compCompHnd->getFieldOffset(
                    _compiler.eeGetAsyncInfo().continuationStateFldHnd);
                var state = LoadFromOffset(resumedContinuation, stateOffset, TYP_INT,
                    GTF_IND_NONFAULTING);
                var zero = _compiler.gtNewZeroConNode(TYP_INT);
                var notZero = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, state, zero);
                var stateBranch = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, notZero);
                conditional.InsertAtEnd(LIR.SeqTree(_compiler, stateBranch));
            }
            else
            {
                var switchBlock = _compiler.fgNewBBbefore(BBJ_SWITCH,
                    _states[0].ResumptionBB, true);
                switchBlock.inheritWeightPercentage(entry, 0);
                resumeEdge = _compiler.fgAddRefPred(switchBlock, entry);
                JITDUMP($"  Redirecting entry {FMT_BB(entry.bbNum)} to BBJ_SWITCH " +
                    $"{FMT_BB(switchBlock.bbNum)} for resumption with {_states.Count} states\n");

                continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var stateOffset = _compiler.info.compCompHnd->getFieldOffset(
                    _compiler.eeGetAsyncInfo().continuationStateFldHnd);
                var state = LoadFromOffset(continuation, stateOffset, TYP_INT,
                    GTF_IND_NONFAULTING);
                var switchNode = _compiler.gtNewUnaryNode(GT_SWITCH, TYP_VOID, state);
                switchBlock.InsertAtEnd(LIR.SeqTree(_compiler, switchNode));
                _compiler.fgHasSwitch = true;

                var caseCount = _states.Count + 1;
                var cases = new FlowEdge[caseCount];
                var unique = new List<FlowEdge>(caseCount);
                for (var i = 0; i < caseCount; i++)
                {
                    var target = _states[i % _states.Count].ResumptionBB;
                    var edge = _compiler.fgAddRefPred(target, switchBlock);
                    edge.Likelihood = 1.0 / caseCount;
                    cases[i] = edge;
                    if (edge.DupCount == 1)
                    {
                        unique.Add(edge);
                    }
                }

                var descriptor = new BBswtDesc([.. unique], new int[caseCount], hasDefault: true);
                cases.AsSpan().CopyTo(descriptor.Cases);
                switchBlock.SwitchTargets = descriptor;
            }

            entry.SetCond(resumeEdge, entry.TargetEdge);
            resumeEdge.Likelihood = 0;
            entry.FalseEdge.Likelihood = 1;
            if (commonResumedDef is not null)
            {
                StoreResumedDef(commonResumedDef, resumeEdge.DestinationBlock);
            }

            if (_compiler.MethodHasPatchpoint && (_states.Count > 0))
            {
                JITDUMP("  Method has patch points...\n");
                var addressLocal = _compiler.lvaGrabTemp(false, "OSR address for tier0 method");
                _compiler.lvaGetDesc(addressLocal).Type = TYP_I_IMPL;
                var jump = CreateOSRJumpBB(_compiler.gtNewLclvNode(TYP_I_IMPL, addressLocal));
                var onContinuation = entry.TrueTarget;
                var check = _compiler.fgNewBBbefore(BBJ_COND, onContinuation, true);
                JITDUMP($"    Created {FMT_BB(check.bbNum)} to check whether we should transition immediately to OSR\n");
                _compiler.fgRedirectEdge(ref entry.TrueEdgeRef, check);
                entry.TrueEdge.Likelihood = 0;
                check.inheritWeightPercentage(entry, 0);

                var toContinuation = _compiler.fgAddRefPred(onContinuation, check);
                var toJump = _compiler.fgAddRefPred(jump, check);
                check.SetCond(toJump, toContinuation);
                toJump.Likelihood = 0;
                toContinuation.Likelihood = 1;

                continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var address = LoadFromOffset(continuation,
                    OFFSETOF__CORINFO_Continuation__data, TYP_I_IMPL);
                var storeAddress = _compiler.gtNewStoreLclVarNode(addressLocal, address);
                check.InsertAtEnd(LIR.SeqTree(_compiler, storeAddress));
                var savedAddress = _compiler.gtNewLclvNode(TYP_I_IMPL, addressLocal);
                var zero = _compiler.gtNewIconNode(TYP_I_IMPL, 0);
                var nonZero = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, savedAddress, zero);
                check.InsertAtEnd(LIR.SeqTree(_compiler,
                    _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, nonZero)));
            }
            else if (_compiler.opts.IsOSR)
            {
                JITDUMP("  Method is an OSR function\n");
                var onContinuation = entry.TrueTarget;
                var onNoContinuation = entry.FalseTarget;
                var check = _compiler.fgNewBBbefore(BBJ_COND, onContinuation, true);
                JITDUMP($"    Created {FMT_BB(check.bbNum)} to check for Tier-0 continuations\n");
                _compiler.fgRedirectEdge(ref entry.TrueEdgeRef, check);
                entry.TrueEdge.Likelihood = 0;
                check.inheritWeightPercentage(entry, 0);

                var toContinuation = _compiler.fgAddRefPred(onContinuation, check);
                var toNoContinuation = _compiler.fgAddRefPred(onNoContinuation, check);
                check.SetCond(toNoContinuation, toContinuation);
                toContinuation.Likelihood = 0;
                toNoContinuation.Likelihood = 1;

                continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var address = LoadFromOffset(continuation,
                    OFFSETOF__CORINFO_Continuation__data, TYP_I_IMPL);
                var zero = _compiler.gtNewIconNode(TYP_I_IMPL, 0);
                var isTierZero = _compiler.gtNewBinaryNode(GT_EQ, TYP_INT, address, zero);
                check.InsertAtEnd(LIR.SeqTree(_compiler,
                    _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, isTierZero)));

                continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var storeReusable = _compiler.gtNewStoreLclVarNode(
                    _reuseContinuationVar, continuation);
                onContinuation.InsertAtBeginning(continuation);
                onContinuation.InsertAfter(continuation, storeReusable);
            }
        }
    }
}
