// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void InsertProfTailCallHook(GenTreeCall call, GenTree? insertionPoint)
    {
        assert(call.IsTailCall);
        assert(CompilerInstance.compIsProfilerHookNeeded);
#if TARGET_X86
        insertionPoint ??= call;
#else
        insertionPoint ??= FindEarliestPutArg(call) ?? call;
#endif

#if DEBUG
        JITDUMP($"Inserting profiler tail call before [{insertionPoint.TreeId:D6}]\n");
#endif
        BlockRange().InsertBefore(insertionPoint, new GenTree(GT_PROF_HOOK, TYP_VOID));
    }

    private static GenTree? FindEarliestPutArg(GenTreeCall call)
    {
        var numMarkedNodes = MarkCallPutArgAndFieldListNodes(call);
        if (numMarkedNodes == 0)
        {
            return null;
        }

        var node = (GenTree)call;
        do
        {
            node = node.Prev ??
                throw new System.InvalidOperationException("Reached block start while searching for tail-call arguments.");
            if ((node._lirFlags & LIR.Flags.Mark) != 0)
            {
                node._lirFlags &= ~LIR.Flags.Mark;
                numMarkedNodes--;
            }
        } while (numMarkedNodes > 0);

        assert(node.Oper.IsPutArg);
        return node;
    }
}
