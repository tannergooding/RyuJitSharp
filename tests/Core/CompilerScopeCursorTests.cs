// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CompilerScopeCursorTests
{
    [Test]
    public static unsafe void DebugBasicBlocksSplitAtSortedScopeBoundaries()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, _, _) =>
        {
            compiler.opts.compDbgCode = true;
            compiler.compHndBBtab = [];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            compiler.info.compVarScopes = [
                new() { vsdLifeBeg = 3, vsdLifeEnd = 4 },
                new() { vsdLifeBeg = 0, vsdLifeEnd = 2 },
                new() { vsdLifeBeg = 1, vsdLifeEnd = 3 },
            ];
            compiler.info.compVarScopesCount = 3;
            compiler.compInitScopeLists();
            ReadOnlySpan<byte> il = [0, 0, 0, 0, 0x2A];
            compiler.info.compILCodeSize = il.Length;

            fixed (byte* code = il)
            {
                compiler.info.compCode = code;
                compiler.fgMakeBasicBlocks(code, il.Length, new BitArray(il.Length));
            }

            int[] expected = [0, 1, 2, 3, 4];
            Assert.That(compiler.Blocks.Select(block => block.bbCodeOffs), Is.EqualTo(expected));
            Assert.That(compiler.fgLastBB?.bbCodeOffsEnd, Is.EqualTo(il.Length));
        });
    }

    [Test]
    public static void SortedCursorsFollowIndicesAndRetainDescriptorIdentity()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, _, _) =>
        {
            compiler.info.compVarScopes = [
                new() { vsdLVnum = 0, vsdLifeBeg = 10, vsdLifeEnd = 30 },
                new() { vsdLVnum = 1, vsdLifeBeg = 0, vsdLifeEnd = 20 },
                new() { vsdLVnum = 2, vsdLifeBeg = 5, vsdLifeEnd = 10 },
            ];
            compiler.info.compVarScopesCount = 3;
            compiler.compInitScopeLists();

            Assert.That(compiler.compEnterScopeList(0).vsdLVnum, Is.EqualTo(1));
            Assert.That(compiler.compEnterScopeList(1).vsdLVnum, Is.EqualTo(2));
            Assert.That(compiler.compEnterScopeList(2).vsdLVnum, Is.Zero);
            Assert.That(compiler.compExitScopeList(0).vsdLVnum, Is.EqualTo(2));
            Assert.That(compiler.compExitScopeList(1).vsdLVnum, Is.EqualTo(1));
            Assert.That(compiler.compExitScopeList(2).vsdLVnum, Is.Zero);

            ref var enter = ref compiler.compGetNextEnterScope(0);
            Assert.That(Unsafe.IsNullRef(in enter), Is.False);
            enter.vsdLVnum = 42;
            Assert.That(compiler.info.compVarScopes[1].vsdLVnum, Is.EqualTo(42));
            ref var exit = ref compiler.compGetNextExitScope(10);
            Assert.That(Unsafe.IsNullRef(in exit), Is.False);
            exit.vsdLVnum = 43;
            Assert.That(compiler.info.compVarScopes[2].vsdLVnum, Is.EqualTo(43));
        });
    }
}
