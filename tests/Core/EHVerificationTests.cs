// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using NUnit.Framework;
#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.bbCatchType;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EHVerificationTests
{
    [Test]
    public static void HandlerTableCheckerExistsOnlyInDebug()
    {
        var method = typeof(Compiler).GetMethod("fgVerifyHandlerTab", BindingFlags.Instance | BindingFlags.Public);
#if DEBUG
        Assert.That(method, Is.Not.Null);
#else
        Assert.That(method, Is.Null);
#endif
    }

#if DEBUG
    [Test]
    public static void EmptyTableNeedsNoGraph()
    {
        WithCompiler(compiler => {
            compiler.fgVerifyHandlerTab();
            Assert.That(s_assertions, Is.Empty);
        });
    }

    [Test]
    public static void HandlerKindsPreserveBlockNumbersAndIndices(
        [Values(EH_HANDLER_CATCH, EH_HANDLER_FINALLY, EH_HANDLER_FAULT, EH_HANDLER_FAULT_WAS_FINALLY,
            EH_HANDLER_FILTER)] EHHandlerType kind,
        [Values(false, true)] bool funclets)
    {
        WithCompiler(compiler => {
            CreateSimpleTable(compiler, kind, funclets);
            var before = new List<(BasicBlock, int, ushort, ushort)>();
            foreach (var block in compiler.Blocks)
            {
                before.Add((block, block.bbNum, block.bbTryIndex, block.bbHndIndex));
            }

            compiler.fgVerifyHandlerTab();

            Assert.That(s_assertions, Is.Empty);
            var index = 0;
            foreach (var block in compiler.Blocks)
            {
                Assert.That((block, block.bbNum, block.bbTryIndex, block.bbHndIndex), Is.EqualTo(before[index++]));
            }
            Assert.That(index, Is.EqualTo(before.Count));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedTryStartsAreAllowedOnlyBeforeNormalization(bool normalized)
    {
        WithCompiler(compiler => {
            CreateNestedTable(compiler, false, false, true);
            compiler.fgNormalizeEHDone = normalized;

            if (normalized)
            {
                CheckFailure(compiler, "bbNumOuterTryBeg < bbNumTryBeg");
            }
            else
            {
                compiler.fgVerifyHandlerTab();
                Assert.That(s_assertions, Is.Empty);
            }
        });
    }

    [Test]
    public static void NestedRegionsRetainInnermostIndices(
        [Values(false, true)] bool insideHandler,
        [Values(false, true)] bool funclets,
        [Values(false, true)] bool sharedEnd)
    {
        WithCompiler(compiler => {
            CreateNestedTable(compiler, insideHandler, funclets, false);
            compiler.fgNormalizeEHDone = true;

            if (sharedEnd)
            {
                ref var inner = ref compiler.compHndBBtab[0];
                ref var outer = ref compiler.compHndBBtab[1];
                var last = funclets ? inner.ebdTryLast : inner.ebdHndLast;
                if (insideHandler)
                {
                    outer.ebdHndLast.clearHndIndex();
                    outer.ebdHndLast = last;
                }
                else
                {
                    outer.ebdTryLast.clearTryIndex();
                    outer.ebdTryLast = last;
                }
            }

            compiler.fgVerifyHandlerTab();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(EH_HANDLER_CATCH)]
    [TestCase(EH_HANDLER_FILTER)]
    public static void HandlersMayLexicallyPrecedeTheirTry(EHHandlerType kind)
    {
        WithCompiler(compiler => {
            CreateSimpleTable(compiler, kind, false);
            ref var clause = ref compiler.compHndBBtab[0];
            var exit = clause.ebdHndLast.Next;
            assert(exit is not null);
            if (clause.HasFilter)
            {
                Link(compiler, clause.ebdFilter, clause.ebdHndBeg, exit, clause.ebdTryBeg);
            }
            else
            {
                Link(compiler, clause.ebdHndBeg, exit, clause.ebdTryBeg);
            }
            clause.ebdTryBeg.Next = null;

            compiler.fgVerifyHandlerTab();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(EH_HANDLER_FINALLY)]
    [TestCase(EH_HANDLER_FAULT)]
    public static void CatchReturnRequiresACatchHandler(EHHandlerType kind)
    {
        WithCompiler(compiler => {
            CreateSimpleTable(compiler, kind, false, BBJ_EHCATCHRET);
            CheckFailure(compiler, "ehDsc.HasCatchHandler");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedHandlerTryStartIsAllowedOnlyBeforeNormalization(bool normalized)
    {
        WithCompiler(compiler => {
            CreateNestedTable(compiler, true, false, true);
            compiler.fgNormalizeEHDone = normalized;

            if (normalized)
            {
                CheckFailure(compiler, "bbNumOuterHndBeg < bbNumTryBeg");
            }
            else
            {
                compiler.fgVerifyHandlerTab();
                Assert.That(s_assertions, Is.Empty);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MutualProtectHandlersDoNotInheritSiblingTryIndex(bool funclets)
    {
        WithCompiler(compiler => {
            var tryBlock = NewBlock(compiler);
            var firstHandler = NewBlock(compiler, BBJ_EHCATCHRET);
            var secondHandler = NewBlock(compiler, BBJ_EHCATCHRET);
            Link(compiler, tryBlock, firstHandler, secondHandler);
            SetTable(compiler,
                Clause(tryBlock, tryBlock, firstHandler, firstHandler, EH_HANDLER_CATCH, 0, enclosingTry: 1),
                Clause(tryBlock, tryBlock, secondHandler, secondHandler, EH_HANDLER_CATCH, 1));
            tryBlock.TryIndex = 0;
            firstHandler.HndIndex = 0;
            secondHandler.HndIndex = 1;
            compiler.fgNormalizeEHDone = true;
            compiler.fgFuncletsCreated = funclets;
            compiler.fgFirstFuncletBB = funclets ? firstHandler : null;

            compiler.fgVerifyHandlerTab();

            Assert.That(s_assertions, Is.Empty);
            Assert.That(firstHandler.hasTryIndex, Is.False);
            Assert.That(secondHandler.hasTryIndex, Is.False);
        });
    }

    [TestCase("id-range", "HBtab.ebdID < impInlineRoot.compEHID")]
    [TestCase("try-retention", "HBtab.ebdTryBeg.HasFlag(BBF_DONT_REMOVE)")]
    [TestCase("handler-retention", "HBtab.ebdHndBeg.HasFlag(BBF_DONT_REMOVE)")]
    [TestCase("removed-boundary", "!HBtab.ebdHndLast.HasFlag(BBF_REMOVED)")]
    [TestCase("absent-boundary", "bbNumHndLast != 0")]
    [TestCase("duplicate-block-number", "blockNumMap[block.bbNum] == 0")]
    [TestCase("try-index", "block.bbTryIndex == blockTryIndex[block.bbNum]")]
    [TestCase("handler-index", "block.bbHndIndex == blockHndIndex[block.bbNum]")]
    [TestCase("handler-catch-type", "HBtab.ebdHndBeg.CatchType is not")]
    [TestCase("non-handler-catch-type", "block.CatchType == BBCT_NONE")]
    [TestCase("premature-funclet", "fgFirstFuncletBB is null")]
    [TestCase("finally-return", "ehDsc.HasFinallyHandler")]
    [TestCase("fault-return", "ehDsc.HasFaultHandler")]
    [TestCase("filter-return", "ehDsc.HasFilter")]
    public static void InvalidTablesReportTheNativeAssertion(string fault, string expression)
    {
        WithCompiler(compiler => {
            CreateSimpleTable(compiler, EH_HANDLER_CATCH, false, fault switch {
                "finally-return" => BBJ_EHFINALLYRET,
                "fault-return" => BBJ_EHFAULTRET,
                "filter-return" => BBJ_EHFILTERRET,
                _ => null,
            });
            ref var clause = ref compiler.compHndBBtab[0];

            switch (fault)
            {
                case "id-range":
                {
                    clause.ebdID = compiler.compEHID;
                    break;
                }

                case "try-retention":
                {
                    clause.ebdTryBeg.RemoveFlags(BBF_DONT_REMOVE);
                    break;
                }

                case "handler-retention":
                {
                    clause.ebdHndBeg.RemoveFlags(BBF_DONT_REMOVE);
                    break;
                }

                case "removed-boundary":
                {
                    clause.ebdHndLast = NewBlock(compiler);
                    clause.ebdHndLast.SetFlags(BBF_REMOVED);
                    break;
                }

                case "absent-boundary":
                {
                    clause.ebdHndLast = NewBlock(compiler);
                    break;
                }

                case "duplicate-block-number":
                {
                    clause.ebdHndBeg.bbNum = clause.ebdTryBeg.bbNum;
                    break;
                }

                case "try-index":
                {
                    clause.ebdTryBeg.clearTryIndex();
                    break;
                }

                case "handler-index":
                {
                    clause.ebdHndBeg.clearHndIndex();
                    break;
                }

                case "handler-catch-type":
                {
                    clause.ebdHndBeg.CatchType = BBCT_NONE;
                    break;
                }

                case "non-handler-catch-type":
                {
                    clause.ebdTryBeg.CatchType = BBCT_FINALLY;
                    break;
                }

                case "premature-funclet":
                {
                    compiler.fgFirstFuncletBB = clause.ebdHndBeg;
                    break;
                }

                case "finally-return":
                case "fault-return":
                case "filter-return":
                {
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown invalid table case.");
                }
            }

            CheckFailure(compiler, expression);
        });
    }

    [TestCase("duplicate-id", "BitVecOps.TryAddElemD")]
    [TestCase("enclosing-index", "HBtab.ebdEnclosingTryIndex > XTnum")]
    [TestCase("outer-range", "bbNumTryLast <= bbNumOuterTryLast")]
    public static void InvalidNestingReportsTheNativeAssertion(string fault, string expression)
    {
        WithCompiler(compiler => {
            CreateNestedTable(compiler, false, false, false);
            switch (fault)
            {
                case "duplicate-id":
                {
                    compiler.compHndBBtab[1].ebdID = 0;
                    break;
                }

                case "enclosing-index":
                {
                    compiler.compHndBBtab[0].ebdEnclosingTryIndex = 0;
                    break;
                }

                case "outer-range":
                {
                    compiler.compHndBBtab[1].ebdTryLast = compiler.compHndBBtab[1].ebdTryBeg;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown invalid nesting case.");
                }
            }

            CheckFailure(compiler, expression);
        });
    }

    [TestCase("filter-type", "HBtab.ebdFilter.CatchType == BBCT_FILTER")]
    [TestCase("handler-type", "HBtab.ebdHndBeg.CatchType == BBCT_FILTER_HANDLER")]
    [TestCase("return-placement", "blockNumMap[block.bbNum] < blockNumMap[ehDsc.ebdHndBeg.bbNum]")]
    [TestCase("first-funclet", "bbNumFirstFunclet <= bbNumFilter")]
    public static void InvalidFiltersReportTheNativeAssertion(string fault, string expression)
    {
        WithCompiler(compiler => {
            CreateSimpleTable(compiler, EH_HANDLER_FILTER, true, fault == "return-placement" ? BBJ_EHFILTERRET : null);
            ref var clause = ref compiler.compHndBBtab[0];
            assert(clause.HasFilter);
            switch (fault)
            {
                case "filter-type":
                {
                    clause.ebdFilter.CatchType = BBCT_NONE;
                    break;
                }

                case "handler-type":
                {
                    clause.ebdHndBeg.CatchType = BBCT_NONE;
                    break;
                }

                case "return-placement":
                {
                    break;
                }

                case "first-funclet":
                {
                    compiler.fgFirstFuncletBB = clause.ebdHndBeg;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown invalid filter case.");
                }
            }

            CheckFailure(compiler, expression);
        });
    }

    private static void CreateSimpleTable(Compiler compiler, EHHandlerType kind, bool funclets, BBKinds? handlerKind = null)
    {
        var tryBlock = NewBlock(compiler);
        var handler = NewBlock(compiler, handlerKind ?? kind switch {
            EH_HANDLER_FINALLY => BBJ_EHFINALLYRET,
            EH_HANDLER_FAULT or EH_HANDLER_FAULT_WAS_FINALLY => BBJ_EHFAULTRET,
            _ => BBJ_EHCATCHRET,
        });
        var exit = NewBlock(compiler);
        var filter = kind == EH_HANDLER_FILTER ? NewBlock(compiler, BBJ_EHFILTERRET) : null;
        var clause = Clause(tryBlock, tryBlock, handler, handler, kind, 0);
        clause.ebdFilter = filter;
        tryBlock.TryIndex = 0;
        handler.HndIndex = 0;

        // Deliberately leave holes and nonlexical numbering in the block map.
        tryBlock.bbNum = 7;
        handler.bbNum = 2;
        exit.bbNum = 5;
        compiler.fgBBNumMax = 7;

        if (filter is not null)
        {
            filter.bbNum = 6;
            filter.HndIndex = 0;
            filter.SetFlags(BBF_DONT_REMOVE);
            filter.CatchType = BBCT_FILTER;
            Link(compiler, funclets ? [tryBlock, exit, filter, handler] : [tryBlock, filter, handler, exit]);
        }
        else
        {
            Link(compiler, funclets ? [tryBlock, exit, handler] : [tryBlock, handler, exit]);
        }

        SetTable(compiler, clause);
        compiler.fgFuncletsCreated = funclets;
        compiler.fgFirstFuncletBB = funclets ? filter ?? handler : null;
    }

    private static void CreateNestedTable(Compiler compiler, bool insideHandler, bool funclets, bool sharedStart)
    {
        var outerTry = NewBlock(compiler);
        var outerHandler = NewBlock(compiler, BBJ_EHCATCHRET);
        var enclosingBegin = insideHandler ? outerHandler : outerTry;
        var innerTry = sharedStart ? enclosingBegin : NewBlock(compiler);
        var innerHandler = NewBlock(compiler, BBJ_EHCATCHRET);
        var enclosingLast = NewBlock(compiler);
        var outer = Clause(outerTry, insideHandler ? outerTry : enclosingLast,
            outerHandler, insideHandler ? enclosingLast : outerHandler, EH_HANDLER_CATCH, 1);
        var inner = Clause(innerTry, innerTry, innerHandler, innerHandler, EH_HANDLER_CATCH, 0,
            enclosingTry: insideHandler ? EHblkDsc.NO_ENCLOSING_INDEX : (ushort)1,
            enclosingHandler: insideHandler ? (ushort)1 : EHblkDsc.NO_ENCLOSING_INDEX);

        List<BasicBlock> blocks = [outerTry];
        if (insideHandler)
        {
            blocks.Add(outerHandler);
        }
        if (!sharedStart)
        {
            blocks.Add(innerTry);
        }
        if (!funclets)
        {
            blocks.Add(innerHandler);
        }
        blocks.Add(enclosingLast);
        if (!insideHandler)
        {
            blocks.Add(outerHandler);
        }
        if (funclets)
        {
            blocks.Add(innerHandler);
        }
        Link(compiler, [.. blocks]);
        SetTable(compiler, inner, outer);

        outerTry.TryIndex = 1;
        outerHandler.HndIndex = 1;
        innerTry.TryIndex = 0;
        innerHandler.HndIndex = 0;
        if (insideHandler)
        {
            innerTry.HndIndex = 1;
            enclosingLast.HndIndex = 1;
        }
        else
        {
            innerHandler.TryIndex = 1;
            enclosingLast.TryIndex = 1;
        }
        compiler.fgFuncletsCreated = funclets;
        compiler.fgFirstFuncletBB = funclets ? outerHandler : null;
    }

    private static EHblkDsc Clause(BasicBlock tryBegin, BasicBlock tryLast, BasicBlock handlerBegin,
        BasicBlock handlerLast, EHHandlerType kind, ushort id,
        ushort enclosingTry = EHblkDsc.NO_ENCLOSING_INDEX,
        ushort enclosingHandler = EHblkDsc.NO_ENCLOSING_INDEX)
    {
        tryBegin.SetFlags(BBF_DONT_REMOVE);
        handlerBegin.SetFlags(BBF_DONT_REMOVE);
        handlerBegin.CatchType = kind switch {
            EH_HANDLER_FILTER => BBCT_FILTER_HANDLER,
            EH_HANDLER_FINALLY => BBCT_FINALLY,
            EH_HANDLER_FAULT or EH_HANDLER_FAULT_WAS_FINALLY => BBCT_FAULT,
            _ => (bbCatchType)1,
        };

        return new EHblkDsc {
            ebdTryBeg = tryBegin,
            ebdTryLast = tryLast,
            ebdHndBeg = handlerBegin,
            ebdHndLast = handlerLast,
            ebdHandlerType = kind,
            ebdID = id,
            ebdEnclosingTryIndex = enclosingTry,
            ebdEnclosingHndIndex = enclosingHandler,
        };
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind = BBJ_RETURN)
    {
        return BasicBlock.New(compiler, kind);
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        blocks[0].Prev = null;
        blocks[^1].Next = null;
        for (var i = 0; i < blocks.Length - 1; i++)
        {
            blocks[i].Next = blocks[i + 1];
        }
    }

    private static void SetTable(Compiler compiler, params EHblkDsc[] clauses)
    {
        compiler.compHndBBtab = clauses;
        compiler.compHndBBtabCount = (ushort)clauses.Length;
        compiler.compEHID = (ushort)clauses.Length;
    }

    private static void CheckFailure(Compiler compiler, string expression)
    {
        var exception = Assert.Throws<FatalJitException>(compiler.fgVerifyHandlerTab);
        Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
        Assert.That(s_assertions, Has.Count.EqualTo(1));
        Assert.That(s_assertions[0], Does.Contain(expression));
    }

    private static readonly List<string> s_assertions = [];

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgImportDone = true;
        compiler.info.compFullName = nameof(EHVerificationTests);
        JitFlags flags = default;
        flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();
        AltJitSkipOnAssert(ref JitConfig) = 1;
        s_assertions.Clear();

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);
#endif
}
