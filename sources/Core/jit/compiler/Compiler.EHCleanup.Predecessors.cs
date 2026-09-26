// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgflow.cpp.

using System;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private void fgRemoveBlockAsPred(BasicBlock block)
    {
        switch (block.Kind)
        {
            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_ALWAYS:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            {
                fgRemoveRefPred(block.TargetEdge);
                break;
            }

            case BBJ_COND:
            {
                fgRemoveRefPred(block.TrueEdge);
                fgRemoveRefPred(block.FalseEdge);
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                var targets = block.EhfTargets
                    ?? throw new InvalidOperationException("Finally return targets must be initialized.");
                foreach (var edge in targets.Succs)
                {
                    _ = fgRemoveAllRefPreds(edge.DestinationBlock, block);
                }
                break;
            }

            case BBJ_EHFAULTRET:
            case BBJ_THROW:
            case BBJ_RETURN:
            {
                break;
            }

            case BBJ_SWITCH:
            {
                foreach (var edge in block.SwitchTargets.Succs)
                {
                    _ = fgRemoveAllRefPreds(edge.DestinationBlock, block);
                }
                break;
            }

            default:
            {
                throw new InvalidOperationException($"Unexpected block kind {block.Kind}.");
            }
        }
    }
}
