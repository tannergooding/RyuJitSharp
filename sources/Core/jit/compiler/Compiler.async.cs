// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Add uses of the saved contexts to async calls, modelling their restoration on suspension.</summary>
    public void AddContextArgsToAsyncCalls(BasicBlock block)
    {
        var visitor = new AddAsyncContextArgsVisitor(this);
        foreach (var statement in block.Statements)
        {
            _ = visitor.WalkTree(ref statement.RootNodeRef, null);
        }
    }

    private struct AddAsyncContextArgsVisitor : IGenTreeVisitor<AddAsyncContextArgsVisitor>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        public AddAsyncContextArgsVisitor(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            if ((tree.Flags & GTF_CALL) == 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (!tree.Oper.IsCall || !tree.AsCall().IsAsync)
            {
                return WALK_CONTINUE;
            }

            var call = tree.AsCall();
            _compiler.compAsyncBodyMaySuspend = true;

            if (call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse) is not null)
            {
                assert(_compiler.compIsForInlining);
                return WALK_CONTINUE;
            }

            if (_compiler.compIsForInlining && !generalAsyncInliningEnabled())
            {
                return WALK_CONTINUE;
            }

            var resumed = _compiler.gtNewLclVarNode(TYP_INT, _compiler.lvaResumedIndicator);
            var resumedAddr = _compiler.gtNewLclAddrNode(TYP_BYREF, _compiler.lvaResumedIndicator, 0);
            var execCtx = _compiler.gtNewLclVarNode(TYP_REF, _compiler.lvaAsyncExecutionContextVar);
            var syncCtx = _compiler.gtNewLclVarNode(TYP_REF, _compiler.lvaAsyncSynchronizationContextVar);
#if DEBUG
            JITDUMP($"Adding resumed use [{resumed.TreeId:D6}], resumed def [{resumedAddr.TreeId:D6}] exec context [{execCtx.TreeId:D6}], sync context [{syncCtx.TreeId:D6}] to async call [{call.TreeId:D6}]\n");
#endif

            var resumedDefArg = NewCallArg.CreateForPrimitive(resumedAddr).WithWellKnownArg(WellKnownArg.AsyncResumedDef);
            var resumedUseArg = NewCallArg.CreateForPrimitive(resumed).WithWellKnownArg(WellKnownArg.AsyncResumedUse);
            var execCtxArg = NewCallArg.CreateForPrimitive(execCtx).WithWellKnownArg(WellKnownArg.AsyncExecutionContext);
            var syncCtxArg = NewCallArg.CreateForPrimitive(syncCtx).WithWellKnownArg(WellKnownArg.AsyncSynchronizationContext);

            // Keep the single def outside the per-frame (resumed, exec, sync) triples.
            var insertAfter = call.Args.PushFront(resumedDefArg);
            insertAfter = call.Args.InsertAfter(insertAfter, resumedUseArg);
            insertAfter = call.Args.InsertAfter(insertAfter, execCtxArg);
            insertAfter = call.Args.InsertAfter(insertAfter, syncCtxArg);
            _compiler.lvaGetDesc(_compiler.lvaResumedIndicator).lvHasLdAddrOp = true;

            if (!_compiler.compIsForInlining)
            {
                return WALK_CONTINUE;
            }

            // The inlining call's values describe every enclosing frame, innermost first.
            // Suspension lowering later uses these pseudo-args to hand contexts back through
            // frames that have not resumed, as though their physical frames had returned.
            var inlCall = _compiler.impInlineInfo.iciCall;
            assert(inlCall is not null);
            var numCopied = 0;
            foreach (var arg in inlCall.Args.Args)
            {
                var kind = arg.WellKnownArg;
                if (kind is not (WellKnownArg.AsyncResumedUse or WellKnownArg.AsyncExecutionContext or WellKnownArg.AsyncSynchronizationContext))
                {
                    continue;
                }

                var newArg = NewCallArg.CreateForPrimitive(_compiler.gtCloneExpr(arg.Node)).WithWellKnownArg(kind);
                insertAfter = call.Args.InsertAfter(insertAfter, newArg);
                numCopied++;
            }
            assert((numCopied % 3) == 0);

            if (numCopied == 0)
            {
#if DEBUG
                JITDUMP($"Inlining call [{inlCall.TreeId:D6}] has no context args; inlinee has no enclosing async frame\n");
#endif
                return WALK_CONTINUE;
            }

            List<ContinuationContextHandling> handling = [inlCall.GetAsyncInfo().ContinuationContextHandling];
            if (inlCall.GetAsyncInfo().InlineFrameContextHandling is List<ContinuationContextHandling> outerHandling)
            {
                foreach (var outer in outerHandling)
                {
                    handling.Add(outer);
                }
            }
            call.GetAsyncInfo().InlineFrameContextHandling = handling;
            assert(handling.Count == (numCopied / 3));
#if DEBUG
            JITDUMP($"Extended async call [{call.TreeId:D6}] to {(numCopied / 3) + 1} frames in chain\n");
#endif
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<AddAsyncContextArgsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
