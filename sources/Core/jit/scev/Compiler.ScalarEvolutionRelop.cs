// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    internal RelopEvaluationResult EvaluateScalarEvolutionRelop(FlowGraphNaturalLoop loop, ValueNum vn)
    {
        var store = vnStore ?? throw new System.InvalidOperationException("Scalar evolution requires value numbering.");
        for (var idom = loop.Header.bbIDom; idom is not null; idom = idom.bbIDom)
        {
            if (idom.Kind is not BBJ_COND)
            {
                continue;
            }

            var jump = idom.LastStmt?.RootNode;
            assert(jump is not null && jump.Oper is GT_JTRUE);
            var comparison = jump.AsUnOp().Op1;
            if (!comparison.Oper.IsCompare)
            {
                continue;
            }

            var implication = new RelopImplicationInfo {
                TreeNormVN = vn,
                DomCmpNormVN = store.VNNormalValue(comparison._vnPair.Liberal),
            };
            optRelopImpliesRelop(ref implication);
            if (!implication.CanInfer)
            {
                continue;
            }

            var trueReaches = optReachable(idom.TrueTarget, loop.Header, idom);
            var falseReaches = optReachable(idom.FalseTarget, loop.Header, idom);
            if (trueReaches && !falseReaches && implication.CanInferFromTrue)
            {
                return implication.ReverseSense ? RelopEvaluationResult.False : RelopEvaluationResult.True;
            }

            if (falseReaches && !trueReaches && implication.CanInferFromFalse)
            {
                return implication.ReverseSense ? RelopEvaluationResult.True : RelopEvaluationResult.False;
            }
        }

        return RelopEvaluationResult.Unknown;
    }
}
