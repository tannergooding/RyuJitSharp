// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
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
#if DEBUG
    private static readonly List<string?> s_assertions = [];
    private static readonly string[] s_noAssertions = [];
    private static readonly string[] s_fieldListAssertion = ["!\"LIST, FIELD_LIST nodes should always be marked contained.\""];
    private static readonly string[] s_nyiAssertion = ["NYI: Unimplemented node type GT_PHI\n"];
    private static readonly string[] s_unknownNodeAssertion = ["!\"Unknown node in codegen\""];
    private static readonly string[] s_nyiAndUnknownAssertions =
    [
        "NYI: Unimplemented node type GT_PHI\n",
        "!\"Unknown node in codegen\"",
    ];
    private static readonly string[] s_swiftNyiAndUnknownAssertions =
    [
        "NYI: Unimplemented node type GT_SWIFT_ERROR\n",
        "!\"Unknown node in codegen\"",
    ];
#endif

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FieldListsPreserveTheNativeAssertionPolicy(bool contained, bool skipOnAssert)
    {
        WithCodeGen(codeGen =>
        {
#if DEBUG
            SkipOnAssert(ref JitConfig) = skipOnAssert ? 1 : 0;
#endif
            var tree = new GenTreeFieldList { IsContained = contained };

#if DEBUG
            if (!contained && skipOnAssert)
            {
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
            else
#endif
            {
                codeGen.genCodeForTreeNode(tree);
            }

#if DEBUG
            Assert.That(s_assertions, Is.EqualTo(contained
                ? s_noAssertions
                : s_fieldListAssertion));
#endif
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
#if DEBUG
            Assert.That(s_assertions, Is.Empty);
#endif
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

#if DEBUG
            if ((nyiPolicy & 2) == 0)
            {
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
            else
#endif
            {
                codeGen.genCodeForTreeNode(tree);
            }

#if DEBUG
            var expected = nyiPolicy switch
            {
                0 => s_noAssertions,
                1 => s_nyiAssertion,
                2 => s_unknownNodeAssertion,
                _ => s_nyiAndUnknownAssertions,
            };
            Assert.That(s_assertions, Is.EqualTo(expected));
#endif
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
#if DEBUG
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
#else
                codeGen.genCodeForTreeNode(tree);
#endif
            }

#if DEBUG
            Assert.That(s_assertions, Is.EqualTo(minopts
                ? s_unknownNodeAssertion
                : s_noAssertions));
#endif
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        }, altJit: true, minopts: minopts);
    }

#if !SWIFT_SUPPORT
    [Test]
    public static void DisabledSwiftOperatorsUseTheUnknownNodeDiagnostic()
    {
        WithCodeGen(codeGen =>
        {
            NyiPolicy(ref JitConfig) = 3;

            codeGen.genCodeForTreeNode(new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL));

#if DEBUG
            Assert.That(s_assertions, Is.EqualTo(s_swiftNyiAndUnknownAssertions));
#endif
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
        });
    }
#endif

    private static void WithCodeGen(Action<CodeGen> action, bool altJit = false, bool minopts = true)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
#if FUNC_INFO_LOGGING
        var previousFunctionLog = Compiler.compJitFuncInfoFile;
#endif
#if MEASURE_FATAL
        var previousNyiCount = s_fatalNyiCount;
#endif
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
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = null;
#endif
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = previousFunctionLog;
#endif
#if MEASURE_FATAL
            s_fatalNyiCount = previousNyiCount;
#endif
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitAssertOnNYI")]
    private static extern ref int NyiPolicy(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int SkipOnAssert(ref JitConfigValues config);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }
#endif
}
#endif
