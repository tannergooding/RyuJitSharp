// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM && DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class WasmControlFlowDiagnosticsTests
{
    [Test]
    public static void QuietDumpDoesNotRequireIntervals()
    {
        WithCompiler(compiler => {
            compiler.verbose = false;
            compiler.fgWasmIntervals = null;

            Assert.That(CaptureDump(compiler), Is.Empty);
        });
    }

    [Test]
    public static void LoopBackedgeBindsToStartAndClosesAtMethodEnd()
    {
        WithCompiler(compiler => {
            var block = NewBlocks(compiler, BBJ_ALWAYS)[0];
            block.TargetEdge = compiler.fgAddRefPred(block, block);
            compiler.fgWasmIntervals = [new WasmInterval(0, 1, WasmInterval.Kind.Loop)];

            Assert.That(CaptureDump(compiler), Is.EqualTo(
                "Before BB01 at 0 stack is:empty\n" +
                "Loop (1)\n" +
                "  BB01\n" +
                "BR 0 (0)be\n\n" +
                "END    (1)Loop\n"));
        });
    }

    [Test]
    public static void ContiguousTrueTargetInvertsTheDiagnosticBranch()
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].TrueEdge = compiler.fgAddRefPred(blocks[1], blocks[0]);
            blocks[0].FalseEdge = compiler.fgAddRefPred(blocks[2], blocks[0]);
            compiler.fgWasmIntervals = [new WasmInterval(0, 2, WasmInterval.Kind.Block)];

            Assert.That(CaptureDump(compiler), Is.EqualTo(
                "Before BB01 at 0 stack is:empty\n" +
                "Block (2)\n" +
                "  BB01\n" +
                "FALLTHROUGH-inv\n" +
                "BR_IF-inv 0 (2)\n\n" +
                "Before BB02 at 1 stack is: [0,2]\n" +
                "  BB02\n" +
                "RETURN\n\n" +
                "Before BB03 at 2 stack is: [0,2]\n" +
                "END    (2)Block\n" +
                "  BB03\n" +
                "RETURN\n\n"));
        });
    }

    [Test]
    public static void SwitchDumpPreservesDuplicateCaseOrderAndNestedDepth()
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, BBJ_SWITCH, BBJ_RETURN, BBJ_RETURN);
            var near = compiler.fgAddRefPred(blocks[1], blocks[0]);
            var far = compiler.fgAddRefPred(blocks[2], blocks[0]);
            var desc = new BBswtDesc([near, far], [0, 1, 2], hasDefault: true);
            desc.Cases[0] = far;
            desc.Cases[1] = near;
            desc.Cases[2] = far;
            blocks[0].SwitchTargets = desc;
            compiler.fgWasmIntervals = [
                new WasmInterval(0, 2, WasmInterval.Kind.Block),
                new WasmInterval(0, 1, WasmInterval.Kind.Block)
            ];

            Assert.That(CaptureDump(compiler), Is.EqualTo(
                "Before BB01 at 0 stack is:empty\n" +
                "Block (2)\n" +
                "Block (1)\n" +
                "  BB01\n" +
                "BR_TABLE 1 (2), 0 (1), 1 (2)\n\n" +
                "Before BB02 at 1 stack is: [0,1] [0,2]\n" +
                "END    (1)Block\n" +
                "  BB02\n" +
                "RETURN\n\n" +
                "Before BB03 at 2 stack is: [0,2]\n" +
                "END    (2)Block\n" +
                "  BB03\n" +
                "RETURN\n\n"));
        });
    }

    private static BasicBlock[] NewBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            var block = BasicBlock.New(compiler, kinds[i]);
            block.bbNum = i + 1;
            block.bbPreorderNum = i;
            block.bbRefs = 0;
            blocks[i] = block;

            if (i != 0)
            {
                blocks[i - 1].Next = block;
                block.Prev = blocks[i - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        return blocks;
    }

    private static string CaptureDump(Compiler compiler)
    {
        var previousWriter = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        s_jitstdout = writer;

        try
        {
            compiler.fgDumpWasmControlFlow();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previousWriter;
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.verbose = true;
        compiler.fgPredsComputed = true;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.fgWasmIntervals = [];
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
