// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanIntervalDiagnosticsTests
{
    [Test]
    public static void EmptyBlocksRetainNativeHeadersSeparatorsAndTrailingLines()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                firstHeader + $"====={newline}{newline}" +
                secondHeader + $"====={newline}{newline}{newline}{newline}"));
            Assert.That(CurrentBlockSequenceNumber(allocator), Is.EqualTo(2));
        });
    }

    [Test]
    public static void LocalNumberingLastUseAndOperandSeparatorsMatchNativeDump()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = 2;

            var candidate = new GenTreeLclVar(TYP_INT, 0) { _seqNum = 2 };
            candidate.Flags |= GTF_VAR_DEATH;
            var constant = compiler.gtNewIconNode(TYP_INT, 42);
            constant._seqNum = 4;
            var add = new GenTreeOp(GT_ADD, TYP_INT, candidate, constant) { _seqNum = 6 };
            var store = new GenTreeLclVar(TYP_INT, 1, add) { _seqNum = 8 };
            var memoryLocal = new GenTreeLclVar(TYP_INT, 1) { _seqNum = 10 };
            block.InsertAtEnd(candidate);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(add);
            block.InsertAtEnd(store);
            block.InsertAtEnd(memoryLocal);

            var header = Capture(() => block.dspBlockHeader());
            var constantNameAndValue = Capture(() => DisplayLeaf(compiler, constant));
            var addName = Capture(() => compiler.gtDispNodeName(add));
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("")}  V00(t{candidate.TreeId}*){newline}" +
                $"  N004. {Destination($"t{constant.TreeId}")}{constantNameAndValue}{newline}" +
                $"  N006. {Destination($"t{add.TreeId}")}{addName}; t{candidate.TreeId}*,t{constant.TreeId}{newline}" +
                $"  N008. {Destination("")}  V01 MEM; t{add.TreeId}{newline}" +
                $"  N010. {Destination($"t{memoryLocal.TreeId}")}  V01 MEM{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedOperandPrintsItsOwnIdOnlyWhenItProducesRegisters(bool childProducesRegister)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var child = compiler.gtNewIconNode(TYP_INT, 7);
            child._seqNum = 2;
            if (!childProducesRegister)
            {
                child.IsContained = true;
            }

            var wrapper = new GenTreeUnOp(GT_BITCAST, TYP_INT, child) { IsContained = true, _seqNum = 4 };
            var other = compiler.gtNewIconNode(TYP_INT, 8);
            other._seqNum = 6;
            var add = new GenTreeOp(GT_ADD, TYP_INT, wrapper, other) { _seqNum = 8 };
            block.InsertAtEnd(child);
            block.InsertAtEnd(wrapper);
            block.InsertAtEnd(other);
            block.InsertAtEnd(add);

            var header = Capture(() => block.dspBlockHeader());
            var childText = Capture(() => DisplayLeaf(compiler, child));
            var wrapperName = Capture(() => compiler.gtDispNodeName(wrapper));
            var otherText = Capture(() => DisplayLeaf(compiler, other));
            var addName = Capture(() => compiler.gtDispNodeName(add));
            var childDestination = childProducesRegister ? $"t{child.TreeId}" : "";
            var wrapperDestination = childProducesRegister ? $"t{wrapper.TreeId}" : "";
            var addOperands = childProducesRegister ? $"t{wrapper.TreeId},t{other.TreeId}" : $"t{other.TreeId}";
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination(childDestination)}{childText}{newline}" +
                $"  N004. {Destination(wrapperDestination)}{wrapperName}{(childProducesRegister ? $"; t{child.TreeId}" : "")}{newline}" +
                $"  N006. {Destination($"t{other.TreeId}")}{otherText}{newline}" +
                $"  N008. {Destination($"t{add.TreeId}")}{addName}; {addOperands}{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void SequenceNumbersUseTheNativeUnsignedDisplayWidth()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            constant._seqNum = -1;
            block.InsertAtEnd(constant);
            var header = Capture(() => block.dspBlockHeader());
            var leaf = Capture(() => DisplayLeaf(compiler, constant));
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N4294967295. {Destination($"t{constant.TreeId}")}{leaf}{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void ResolutionBlockPrintsItsOriginalEdge()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;
            allocator.getSplitBBNumToTargetBBNumMap()[(uint)blocks[1].bbNum] =
                new LinearScan.SplitEdgeInfo {
                    fromBBNum = (uint)blocks[0].bbNum,
                    toBBNum = (uint)blocks[0].bbNum,
                };
            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                firstHeader + $"====={newline}{newline}" +
                secondHeader + $"====={newline}" +
                $"New block introduced for resolution from {FMT_BB(blocks[0].bbNum)} to {FMT_BB(blocks[0].bbNum)}{newline}" +
                $"{newline}{newline}{newline}"));
            Assert.That(CurrentBlockSequenceNumber(allocator), Is.EqualTo(2));
        });
    }

    [Test]
    public static void ResolutionBlockWithoutSplitMappingFailsRatherThanDroppingItsDiagnostic()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;

            _ = Assert.Throws<FatalJitException>(() => _ = Capture(() => TupleStyleDumpPre(allocator)));
        });
    }

    private static string Destination(string operand)
        => operand.Length == 0 ? "                 " : $"{operand,-15} =";

    private static void DisplayLeaf(Compiler compiler, GenTree tree)
    {
        compiler.gtDispNodeName(tree);
        var indentStack = new IndentStack(compiler);
        compiler.gtDispLeaf(tree, ref indentStack);
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index != 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = count;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;
        return blocks;
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "tupleStyleDumpPre")]
    private static extern void TupleStyleDumpPre(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockSequenceNumber")]
    private static extern ref int CurrentBlockSequenceNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
