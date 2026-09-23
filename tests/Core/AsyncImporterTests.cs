// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncImporterTests
{
    [TestCase(new byte[] { 0x0A, 0x12, 0 }, 0)]
    [TestCase(new byte[] { 0x0B, 0xFE, 0x0D, 1, 0 }, 1)]
    [TestCase(new byte[] { 0x0C, 0x12, 2 }, 2)]
    [TestCase(new byte[] { 0x0D, 0xFE, 0x0D, 3, 0 }, 3)]
    [TestCase(new byte[] { 0x13, 255, 0x12, 255 }, 255)]
    [TestCase(new byte[] { 0x13, 255, 0xFE, 0x0D, 255, 0 }, 255)]
    [TestCase(new byte[] { 0xFE, 0x0E, 0, 1, 0xFE, 0x0D, 0, 1 }, 256)]
    [TestCase(new byte[] { 0xFE, 0x0E, 255, 255, 0xFE, 0x0D, 255, 255 }, 65535)]
    [TestCase(new byte[] { 0xFE, 0x0E, 3, 0, 0x12, 3 }, 3)]
    public static void LocalAddressPatternsCheckEveryTruncationAndPreserveTheCursor(byte[] pattern, int expectedLocal)
    {
        var bytes = new byte[pattern.Length + 1];
        pattern.CopyTo(bytes, 0);
        fixed (byte* start = bytes)
        {
            for (var length = 0; length <= bytes.Length; length++)
            {
                var cursor = start;
                var matched = Compiler.impMatchStlocLdloca(ref cursor, start + length, out var local);
                var expectedMatch = length >= pattern.Length;
                Assert.That(matched, Is.EqualTo(expectedMatch), $"Input length {length}");
                Assert.That(local, Is.EqualTo(expectedMatch ? expectedLocal : Globals.BAD_VAR_NUM));
                Assert.That(cursor - start, Is.EqualTo(expectedMatch ? (long)pattern.Length : 0L));
            }
        }

        const int configuredAwaitLength = 1 + (2 * (1 + sizeof(int)));
        var awaitBytes = new byte[sizeof(int) + pattern.Length + configuredAwaitLength];
        pattern.CopyTo(awaitBytes, sizeof(int));
        fixed (byte* start = awaitBytes)
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var result = compiler.impMatchTaskAwaitPattern(start, start + awaitBytes.Length, out var config, out var awaitOffset);
            Assert.That((nint)result, Is.EqualTo((nint)0));
            Assert.That(config, Is.EqualTo(-1));
            Assert.That(awaitOffset, Is.EqualTo(Globals.BAD_IL_OFFSET));
        }
    }

    [TestCase(new byte[] { 0x0A, 0x12, 1 })]
    [TestCase(new byte[] { 0xFE, 0x0E, 255, 255, 0x12, 255 })]
    [TestCase(new byte[] { 0x13, 255, 0xFE, 0x0D, 255, 1 })]
    [TestCase(new byte[] { 0xFE, 0x0D, 1, 0, 0x12, 1 })]
    [TestCase(new byte[] { 0x0B, 0xFE, 0x0C, 1, 0 })]
    [TestCase(new byte[] { 0x06, 0x12, 0 })]
    public static void LocalAddressPatternsRejectMismatchedLocalsAndOpcodes(byte[] bytes)
    {
        fixed (byte* start = bytes)
        {
            var cursor = start;
            var matched = Compiler.impMatchStlocLdloca(ref cursor, start + bytes.Length, out var local);
            Assert.That(matched, Is.False);
            Assert.That(local, Is.EqualTo(Globals.BAD_VAR_NUM));
            Assert.That(cursor - start, Is.EqualTo(0L));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void TailAwaitsInheritTheWholeContextChain(int outerFrames)
    {
        WithCompiler(compiler => {
            var inliningCall = new GenTreeCall(var_types.TYP_VOID);
            List<ContinuationContextHandling> handling = [];
            inliningCall.SetIsAsync(new AsyncCallInfo { InlineFrameContextHandling = handling });
            List<CallArg> inherited = [];

            AddArgument(WellKnownArg.AsyncResumedDef, compiler.gtNewLclAddrNode(var_types.TYP_BYREF, 0, 0));
            AddArgument(WellKnownArg.AsyncResumedUse, compiler.gtNewLclvNode(var_types.TYP_INT, 0));
            AddArgument(WellKnownArg.AsyncExecutionContext, compiler.gtNewLclvNode(var_types.TYP_REF, 1));
            AddArgument(WellKnownArg.AsyncSynchronizationContext, compiler.gtNewLclvNode(var_types.TYP_REF, 2));
            for (var frame = 1; frame <= outerFrames; frame++)
            {
                handling.Add(ContinuationContextHandling.ContinueOnThreadPool);
                AddArgument(WellKnownArg.AsyncResumedUse, compiler.gtNewLclvNode(var_types.TYP_INT, frame * 3));
                AddArgument(WellKnownArg.AsyncExecutionContext, compiler.gtNewLclvNode(var_types.TYP_REF, (frame * 3) + 1));
                AddArgument(WellKnownArg.AsyncSynchronizationContext, compiler.gtNewLclvNode(var_types.TYP_REF, (frame * 3) + 2));
            }
            inherited[0].Node.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            inherited[^1].Node.Flags |= GenTreeFlags.GTF_GLOB_REF;

            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.impInlineInfo = new InlineInfo {
                InlineRoot = compiler,
                InlinerCompiler = compiler,
                iciCall = inliningCall,
            };
            var call = new GenTreeCall(var_types.TYP_VOID);
            call.SetIsAsync(default);
            var userArgument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(var_types.TYP_INT, 42)));

            compiler.impInheritAsyncContextsFromInliner(call);

            List<CallArg> arguments = [];
            foreach (var argument in call.Args.Args)
            {
                arguments.Add(argument);
            }
            Assert.Multiple(() => {
                Assert.That(arguments.Count, Is.EqualTo(inherited.Count + 1));
                Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.True);
                Assert.That((call.Flags & GenTreeFlags.GTF_GLOB_REF) != 0, Is.True);
                Assert.That(call.GetAsyncInfo().InlineFrameContextHandling, Is.SameAs(handling));
            });
            Assert.That(arguments[4], Is.SameAs(userArgument));
            arguments.RemoveAt(4);
            for (var index = 0; index < inherited.Count; index++)
            {
                Assert.Multiple(() => {
                    Assert.That(arguments[index].WellKnownArg, Is.EqualTo(inherited[index].WellKnownArg));
                    Assert.That(arguments[index].Node, Is.Not.SameAs(inherited[index].Node));
                    Assert.That(arguments[index].Node.Oper, Is.EqualTo(inherited[index].Node.Oper));
                    Assert.That(arguments[index].Node.AsLclVarCommon().LclNum, Is.EqualTo(inherited[index].Node.AsLclVarCommon().LclNum));
                });
            }

            void AddArgument(WellKnownArg kind, GenTree node)
            {
                inherited.Add(inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(node).WithWellKnownArg(kind)));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallsWithoutInheritedContextsKeepTheirArguments(bool inlining)
    {
        WithCompiler(compiler => {
            if (inlining)
            {
                compiler.opts.compFlags = Globals.CLFLG_INLINING;
                var inliningCall = new GenTreeCall(var_types.TYP_VOID);
                inliningCall.SetIsAsync(default);
                compiler.impInlineInfo = new InlineInfo { iciCall = inliningCall };
            }
            var call = new GenTreeCall(var_types.TYP_VOID);
            call.SetIsAsync(default);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewNull()));

            compiler.impInheritAsyncContextsFromInliner(call);

            Assert.That(call.Args.Head, Is.SameAs(argument));
            Assert.That(call.GetAsyncInfo().InlineFrameContextHandling, Is.Null);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[9];
        compiler.lvaCount = compiler.lvaTable.Length;
        for (var index = 0; index < compiler.lvaCount; index++)
        {
            compiler.lvaTable[index].Type = index % 3 == 0 ? var_types.TYP_INT : var_types.TYP_REF;
        }
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
