// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerBlock(BasicBlock block)
    {
#if WINDOWS_AMD64_ABI
        var compiler = CompilerInstance;
        assert(block == compiler.compCurBB);
        assert(block.IsEmpty || block.IsLIR);
        _block = block;

        // Insertions before the current node must already be lowered. The
        // returned successor can also replace or skip nodes in the original LIR.
        var node = block.FirstNode;
        while (node is not null)
        {
            node = LowerNode(node);
        }

#if DEBUG
        assert(CheckBlock(compiler, block));
#endif
#else
        throw new System.NotImplementedException("Block lowering outside Windows AMD64 is not ported.");
#endif
    }

    private void LowerJmpMethod(GenTree jmp)
    {
#if WINDOWS_AMD64_ABI
        assert(jmp.Oper is GT_JMP);
        JITDUMP("lowering GT_JMP\n");
        DISPNODE(jmp);
        JITDUMP("============\n");

        var compiler = CompilerInstance;
        if (compiler.compMethodRequiresPInvokeFrame)
        {
            var block = compiler.compCurBB;
            assert(block is not null);
            InsertPInvokeMethodEpilog(block, jmp);
        }
#else
        throw new System.NotImplementedException("Method-jump lowering outside Windows AMD64 is not ported.");
#endif
    }
}
