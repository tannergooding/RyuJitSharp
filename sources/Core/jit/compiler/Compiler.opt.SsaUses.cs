// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Record SSA uses in a rewritten tree's containing block; counts may overestimate.</summary>
    /// <remarks>Definitions are ignored, and optimizations do not introduce new PHIs.</remarks>
    public void optRecordSsaUses(GenTree tree, BasicBlock block)
    {
        var visitor = new SsaRecordingVisitor(this, block);
        _ = visitor.WalkTree(ref tree, null);
    }

    private struct SsaRecordingVisitor(Compiler compiler, BasicBlock block) : IGenTreeVisitor<SsaRecordingVisitor>
    {
        public static bool DoPreOrder => true;

        public static bool DoLclVarsOnly => true;

        private readonly Compiler _compiler = compiler;
        private readonly BasicBlock _block = block;
        private readonly GenTreeStack _ancestors = [];

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var local = use.AsLclVarCommon();
            if ((local.Flags & GTF_VAR_DEF) == 0)
            {
                if (local.HasSsaName)
                {
                    ref var ssa = ref _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
                    ssa.AddUse(_block);
                }
                else
                {
                    assert(!_compiler.lvaGetDesc(local.LclNum).lvInSsa);
                    assert(!local.HasCompositeSsaName);
                }
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<SsaRecordingVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
