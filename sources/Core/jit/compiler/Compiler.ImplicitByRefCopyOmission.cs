// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, morph.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    // Mark before morphing so earlier reads gain global effects before a later
    // call can receive the local's address. Otherwise argument reordering could
    // move a read past a call that mutates the reused storage.
    private PhaseStatus fgMarkImplicitByRefCopyOmissionCandidates()
    {
#if FEATURE_IMPLICIT_BYREFS && !UNIX_AMD64_ABI
        if (!fgDidEarlyLiveness)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var visitor = new ImplicitByRefCopyOmissionVisitor(this);
        foreach (var block in Blocks)
        {
            foreach (var statement in block.Statements)
            {
                if ((statement.RootNode.Flags & GTF_CALL) == 0)
                {
                    continue;
                }

                foreach (var local in statement.LocalsTreeList)
                {
                    if (!varTypeIsStruct(local.Type) || !local.Oper.IsLocalRead)
                    {
                        continue;
                    }

                    if ((local.Flags & GTF_VAR_DEATH) != 0)
                    {
                        _ = visitor.WalkTree(ref statement.RootNodeRef, null);
                        break;
                    }
                }
            }
        }
#endif

        return PhaseStatus.MODIFIED_NOTHING;
    }

#if FEATURE_IMPLICIT_BYREFS && !UNIX_AMD64_ABI
    private struct ImplicitByRefCopyOmissionVisitor : IGenTreeVisitor<ImplicitByRefCopyOmissionVisitor>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        public ImplicitByRefCopyOmissionVisitor(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public static bool UseExecutionOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if ((use.Flags & GTF_CALL) == 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (use.Oper is not GT_CALL)
            {
                return WALK_CONTINUE;
            }

            var call = use.AsCall();
            foreach (var arg in call.Args.Args)
            {
                if (!varTypeIsStruct(arg.SignatureType))
                {
                    continue;
                }

                var argNode = arg.Node.EffectiveVal;
                if (!argNode.Oper.IsLocalRead)
                {
                    continue;
                }

                var lclNum = argNode.AsLclVarCommon().LclNum;
                ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
                if (varDsc.lvIsLastUseCopyOmissionCandidate)
                {
                    continue;
                }

                // Implicit byrefs already acquire global effects through their indirections.
                if (varDsc.IsImplicitByRef)
                {
                    continue;
                }

                if (varDsc.lvPromoted || varDsc.lvIsStructField || ((argNode.Flags & GTF_VAR_DEATH) == 0))
                {
                    continue;
                }

                if (!call.Args.IsAbiInformationDetermined)
                {
                    call.Args.DetermineAbiInfo(_compiler, call);
                }

                if (!arg.AbiInfo.IsPassedByReference)
                {
                    continue;
                }

#if DEBUG
                JITDUMP($"Marking V{lclNum:D2} as a candidate for last-use copy omission [{argNode.TreeId:D6}]\n");
#endif
                varDsc.lvIsLastUseCopyOmissionCandidate = true;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ImplicitByRefCopyOmissionVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
#endif
}
