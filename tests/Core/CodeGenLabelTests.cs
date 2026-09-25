// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenLabelTests
{
    [TestCase(BBJ_ALWAYS, false, false)]
    [TestCase(BBJ_ALWAYS, true, true)]
    [TestCase(BBJ_EHCATCHRET, false, true)]
    public static void FallthroughLabelsRespectJumpKindAndHotColdBoundary(
        BBKinds kind, bool cold, bool targetLabeled)
    {
        WithCompiler((compiler, codeGen) => {
            var blocks = CreateBlocks(compiler, kind, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            compiler.fgFirstColdBlock = cold ? blocks[1] : null;

            MarkLabels(codeGen);

            Assert.That(blocks.Select(b => b.HasFlag(BBF_HAS_LABEL)),
                Is.EqualTo((bool[])[true, targetLabeled]));
            Assert.That(blocks[0].IsLastHotBlock(compiler), Is.EqualTo(cold));
            Assert.That(blocks[1].IsLastHotBlock(compiler), Is.EqualTo(!cold));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void ConditionalTargetsOnlyElideAnAdjacentHotFalseEdge(bool cold, bool nonAdjacent)
    {
        WithCompiler((compiler, codeGen) => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            blocks[0].TrueEdge = new FlowEdge(blocks[0], blocks[2], null);
            blocks[0].FalseEdge = new FlowEdge(blocks[0], blocks[nonAdjacent ? 3 : 1], null);
            compiler.fgFirstColdBlock = cold ? blocks[1] : null;

            MarkLabels(codeGen);

            Assert.That(blocks.Select(b => b.HasFlag(BBF_HAS_LABEL)),
                Is.EqualTo((bool[])[true, cold && !nonAdjacent, true, nonAdjacent]));
        });
    }

    [Test]
    public static void SwitchAndUsedThrowHelperTargetsRequireLabels()
    {
        WithCompiler((compiler, codeGen) => {
            var blocks = CreateBlocks(compiler, BBJ_SWITCH, BBJ_RETURN, BBJ_RETURN, BBJ_THROW, BBJ_THROW);
            var first = new FlowEdge(blocks[0], blocks[1], null);
            var second = new FlowEdge(blocks[0], blocks[2], null);
            blocks[0].SwitchTargets = new BBswtDesc([first, second, first], [0, 1, 2], hasDefault: true);
            compiler.fgHasSwitch = true;
            var used = new Compiler.AddCodeDsc {
                acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL, acdDstBlk = blocks[3], acdUsed = true,
            };
            var unused = new Compiler.AddCodeDsc {
                acdKind = SpecialCodeKind.SCK_OVERFLOW, acdDstBlk = blocks[4], acdUsed = false,
            };
            var map = compiler.fgGetAddCodeDscMap();
            map.Add(new Compiler.AddCodeDscKey(used), used);
            map.Add(new Compiler.AddCodeDscKey(unused), unused);

            MarkLabels(codeGen);

            Assert.That(blocks.Select(b => b.HasFlag(BBF_HAS_LABEL)),
                Is.EqualTo((bool[])[true, true, true, true, false]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExceptionRegionsMarkBeginningsAndExclusiveEnds(bool handlerEndsMethod)
    {
        WithCompiler((compiler, codeGen) => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN,
                BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            compiler.compHndBBtab = [
                new() {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_FILTER,
                    ebdTryBeg = blocks[1], ebdTryLast = blocks[2],
                    ebdFilter = blocks[3], ebdHndBeg = blocks[4],
                    ebdHndLast = blocks[handlerEndsMethod ? 6 : 5],
                },
            ];
            compiler.compHndBBtabCount = 1;

            MarkLabels(codeGen);

            Assert.That(blocks.Select(b => b.HasFlag(BBF_HAS_LABEL)),
                Is.EqualTo((bool[])[true, true, false, true, true, false, !handlerEndsMethod]));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void FinallyThunkEndSkipsOnlyAnActualContinuationPair(bool retless, bool atEnd)
    {
        WithCompiler((compiler, codeGen) => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_CALLFINALLY,
                retless ? BBJ_RETURN : BBJ_CALLFINALLYRET, BBJ_RETURN);
            blocks[1].TargetEdge = new FlowEdge(blocks[1], blocks[0], null);
            if (retless)
            {
                blocks[1].SetFlags(BBF_RETLESS_CALL);
            }
            else
            {
                blocks[2].TargetEdge = new FlowEdge(blocks[2], blocks[0], null);
            }
            if (atEnd)
            {
                var last = blocks[retless ? 1 : 2];
                last.Next = null;
                compiler.fgLastBB = last;
            }

            MarkLabels(codeGen);

            Assert.That(blocks.Select(b => b.HasFlag(BBF_HAS_LABEL)),
                Is.EqualTo((bool[])[true, false, retless && !atEnd, !retless && !atEnd]));
        });
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        var blocks = new BasicBlock[kinds.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, kinds[index]);
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = false;
#endif

        return blocks;
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.compHndBBtab = [];
#if DEBUG
        JitTls.Compiler = compiler;
#endif
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
#if DEBUG
            JitTls.Compiler = previous;
#endif
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genMarkLabelsForCodegen")]
    private static extern void MarkLabels(CodeGen codeGen);
}
