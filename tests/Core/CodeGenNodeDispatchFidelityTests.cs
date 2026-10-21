// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenNodeDispatchFidelityTests
{
    private static class DebugData
    {
        internal static readonly List<string?> Assertions = [];
        internal static readonly string[] NoAssertions = [];
        internal static readonly string[] FieldListAssertion =
            ["!\"LIST, FIELD_LIST nodes should always be marked contained.\""];
        internal static readonly string[] NyiAssertion = ["NYI: Unimplemented node type GT_PHI\n"];
        internal static readonly string[] UnknownNodeAssertion = ["!\"Unknown node in codegen\""];
        internal static readonly string[] NyiAndUnknownAssertions =
        [
            "NYI: Unimplemented node type GT_PHI\n",
            "!\"Unknown node in codegen\"",
        ];
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FieldListsPreserveTheNativeAssertionPolicy(bool contained, bool skipOnAssert)
    {
        WithCodeGen(codeGen =>
        {
            if (TestConfiguration.IsDebug)
            {
                SkipOnAssert(ref JitConfig) = skipOnAssert ? 1 : 0;
            }

            var tree = new GenTreeFieldList { IsContained = contained };

            if (TestConfiguration.IsDebug && !contained && skipOnAssert)
            {
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
            else
            {
                codeGen.genCodeForTreeNode(tree);
            }

            if (TestConfiguration.IsDebug)
            {
                Assert.That(DebugData.Assertions, Is.EqualTo(contained
                    ? DebugData.NoAssertions
                    : DebugData.FieldListAssertion));
            }

            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        }, altJit: true);
    }

    [TestCase(GT_NOP, false)]
    [TestCase(GT_CNS_INT, true)]
    [TestCase(GT_FIELD_LIST, true)]
    public static void MarkersAndContainedNodesDoNotRequireAnInitializedEmitter(genTreeOps oper, bool contained)
    {
        WithCodeGen(codeGen =>
        {
            var tree = oper == GT_FIELD_LIST
                ? new GenTreeFieldList()
                : oper == GT_CNS_INT
                    ? new GenTreeIntCon(TYP_INT, 0)
                    : new GenTree(oper, TYP_VOID);
            if (contained)
            {
                tree.IsContained = true;
            }

            codeGen.genCodeForTreeNode(tree);

            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(DebugData.Assertions, Is.Empty);
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void UnknownNodesPreserveNyiThenAssertOrdering(int nyiPolicy)
    {
        WithCodeGen(codeGen =>
        {
            NyiPolicy(ref JitConfig) = nyiPolicy;
            var tree = new GenTreePhi(TYP_INT);

            if (TestConfiguration.IsDebug && (nyiPolicy & 2) == 0)
            {
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
            else
            {
                codeGen.genCodeForTreeNode(tree);
            }

            if (TestConfiguration.IsDebug)
            {
                var expected = nyiPolicy switch
                {
                    0 => DebugData.NoAssertions,
                    1 => DebugData.NyiAssertion,
                    2 => DebugData.UnknownNodeAssertion,
                    _ => DebugData.NyiAndUnknownAssertions,
                };
                Assert.That(DebugData.Assertions, Is.EqualTo(expected));
            }

            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnknownAltJitNodesHonorNowayContinuation(bool minopts)
    {
        WithCodeGen(codeGen =>
        {
            var tree = new GenTreePhi(TYP_INT);
            if (minopts)
            {
                codeGen.genCodeForTreeNode(tree);
            }
            else
            {
                if (TestConfiguration.IsDebug)
                {
                    var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                    Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                        .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
                }
                else
                {
                    codeGen.genCodeForTreeNode(tree);
                }
            }

            if (TestConfiguration.IsDebug)
            {
                Assert.That(DebugData.Assertions, Is.EqualTo(minopts
                    ? DebugData.UnknownNodeAssertion
                    : DebugData.NoAssertions));
            }

            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        }, altJit: true, minopts: minopts);
    }

    [Test]
    public static void UnsupportedSwiftErrorValuesFailExplicitly()
    {
        if (Configuration.SWIFT_SUPPORT)
        {
            Assert.Ignore("Swift error values are supported on this target.");
        }

        WithCodeGen(codeGen =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genCodeForTreeNode(new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL)));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(failure?.ErrorCode, Is.EqualTo(FatalJitErrorCode.DirectFailureMessage090));
            if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
            {
                Assert.That(failure?.Message, Is.EqualTo("Swift error values are unavailable for this target."));
            }
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        });
    }

    private static void WithCodeGen(Action<CodeGen> action, bool altJit = false, bool minopts = true)
    {
        Assume.That(Configuration.TARGET_XARCH, "This test requires an xarch target.");

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = TestConfiguration.IsDebug ? TestConfiguration.CreateJitTls(&ee) : null;
        if (TestConfiguration.IsDebug)
        {
            DebugData.Assertions.Clear();
        }

        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        var previousFunctionLog = Compiler.compJitFuncInfoFile;
        var previousNyiCount = s_fatalNyiCount;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        if (altJit)
        {
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        }
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);

        try
        {
            JitConfig = default;
            JitTls.Compiler = compiler;
            Compiler.compJitFuncInfoFile = null;
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
            Compiler.compJitFuncInfoFile = previousFunctionLog;
            s_fatalNyiCount = previousNyiCount;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitAssertOnNYI")]
    private static extern ref int NyiPolicy(ref JitConfigValues config);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        DebugData.Assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int SkipOnAssert(ref JitConfigValues config);
}
