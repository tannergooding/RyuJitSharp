// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineRecursionTests
{
    private static int s_typeDepth;
    private static int s_definitionQueries;
    private static int s_typeQueries;
    private static bool s_sameDefinition;
    private static CORINFO_SIG_INFO s_signature;
    private static nuint s_signatureMethod;

    [TestCase(1, 0)]
    [TestCase(3, 0)]
    [TestCase(1002, 0)]
    [TestCase(4, 3)]
    [TestCase(1002, 1001)]
    public static void DepthTraversalBoundsWorkAndPrioritizesRecursion(int depth, int recursiveAt)
    {
        WithCompiler(compiler => {
            var strategy = compiler._inlineStrategy ?? throw new InvalidOperationException("Missing inline strategy.");
            InlineContext? context = null;

            for (var level = depth; level > 0; level--)
            {
                context = new InlineContext(strategy) {
                    _parent = context,
                    _callee = (CORINFO_METHOD_STRUCT_*)(level == recursiveAt ? 0x100 : 0x200),
                    _runtimeContext = (CORINFO_CONTEXT_STRUCT_*)0x1000,
                };
            }

            var call = new GenTreeCall(var_types.TYP_VOID) { _callType = gtCallTypes.CT_USER_FUNC };
            var result = new InlineResult(compiler, call, null, "recursion probe", doNotReport: true);
            var info = new InlineInfo {
                fncHandle = (CORINFO_METHOD_STRUCT_*)0x100,
                inlineResult = result,
                inlineCandidateInfo = new InlineCandidateInfo {
                    inlinersContext = context,
                    exactContextHandle = (CORINFO_CONTEXT_STRUCT_*)0x1000,
                },
            };

            var expectedDepth = Math.Min(recursiveAt > 0 ? recursiveAt : depth, InlineStrategy.IMPLEMENTATION_MAX_INLINE_DEPTH + 1);
            Assert.That(compiler.fgCheckInlineDepthAndRecursion(info), Is.EqualTo(expectedDepth));
            Assert.That(s_definitionQueries, Is.EqualTo(expectedDepth - (recursiveAt > 0 ? 1 : 0)));
            var depthField = typeof(DefaultPolicy).GetField("_callsiteDepth", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing policy depth.");
            Assert.That(depthField.GetValue(result.Policy), Is.EqualTo(recursiveAt > 0 ? 0 : expectedDepth));

            if (recursiveAt > 0)
            {
                Assert.That(result.Observation, Is.EqualTo(InlineObservation.CALLSITE_IS_RECURSIVE));
            }
            else if (depth > 3)
            {
                Assert.That(result.Observation, Is.EqualTo(InlineObservation.CALLSITE_IS_TOO_DEEP));
            }
            else
            {
                Assert.That(result.IsFailure, Is.False);
            }
        });
    }

    [TestCase(false, 65, false)]
    [TestCase(true, 64, false)]
    [TestCase(true, 65, true)]
    public static void PolymorphicRecursionLimitsGenericLoading(bool sameDefinition, int complexity, bool expected)
    {
        WithCompiler(compiler => {
            s_sameDefinition = sameDefinition;
            s_typeDepth = complexity;
            var strategy = compiler._inlineStrategy ?? throw new InvalidOperationException("Missing inline strategy.");
            var ancestor = new InlineContext(strategy) { _callee = (CORINFO_METHOD_STRUCT_*)0x200 };
            var info = new InlineInfo {
                fncHandle = (CORINFO_METHOD_STRUCT_*)0x100,
                inlineCandidateInfo = new InlineCandidateInfo {
                    exactContextHandle = (CORINFO_CONTEXT_STRUCT_*)((nuint)0x1000 | (nuint)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_CLASS),
                },
            };

            Assert.That(compiler.IsDisallowedRecursiveInline(ancestor, info), Is.EqualTo(expected));
            Assert.That(s_definitionQueries, Is.EqualTo(1));
            Assert.That(s_typeQueries > 0, Is.EqualTo(sameDefinition));
        });
    }

    [TestCase(32, 32, 0, false)]
    [TestCase(32, 33, 0, true)]
    [TestCase(1, 1, 62, false)]
    [TestCase(1, 1, 63, true)]
    public static void MethodContextsCountBothInstantiationsAndNestedTypes(int classCount, int methodCount, int nestedDepth, bool expected)
    {
        WithCompiler(compiler => {
            var arguments = stackalloc CORINFO_CLASS_STRUCT_*[65];

            for (var i = 0; i < 65; i++)
            {
                arguments[i] = (CORINFO_CLASS_STRUCT_*)0x2000;
            }

            arguments[0] = (CORINFO_CLASS_STRUCT_*)0x1000;
            s_typeDepth = nestedDepth;
            s_signature.sigInst.classInstCount = classCount;
            s_signature.sigInst.classInst = arguments;
            s_signature.sigInst.methInstCount = methodCount;
            s_signature.sigInst.methInst = arguments + classCount;
            var context = (CORINFO_CONTEXT_STRUCT_*)((nuint)0x8000 | (nuint)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_METHOD);

            Assert.That(compiler.ContextComplexityExceeds(context, 64), Is.EqualTo(expected));
            Assert.That(s_signatureMethod, Is.EqualTo((nuint)0x8000));

            if (classCount + methodCount > 64)
            {
                Assert.That(s_typeQueries, Is.Zero);
            }
        });
    }

    [Test]
    public static void AbsentContextDoesNotQueryTheRuntime()
    {
        WithCompiler(compiler => {
            Assert.That(compiler.ContextComplexityExceeds(null, 0), Is.False);
            Assert.That(s_typeQueries, Is.Zero);
            Assert.That(s_signatureMethod, Is.EqualTo((nuint)0));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        var depthField = typeof(InlineStrategy).GetField("_maxInlineDepth", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing strategy depth.");
        depthField.SetValue(compiler._inlineStrategy, 3);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.haveSameMethodDefinition =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, byte>)&HaveSameMethodDefinition;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeInstantiationArgument;
        vtable.Base.Base.getMethodSig = &GetMethodSig;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        s_typeDepth = 0;
        s_definitionQueries = 0;
        s_typeQueries = 0;
        s_sameDefinition = false;
        s_signature = default;
        s_signatureMethod = 0;
        JitTls.Compiler = compiler;
        Globals.JitConfig = new JitConfigValues();

        try
        {
            action(compiler);
        }
        finally
        {
            s_signature = default;
            Globals.JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte HaveSameMethodDefinition(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* first, CORINFO_METHOD_STRUCT_* second)
    {
        s_definitionQueries++;

        return s_sameDefinition ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeInstantiationArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
    {
        s_typeQueries++;

        return (index == 0) && ((nuint)type >= 0x1000) && ((nuint)type < (nuint)(0x1000 + (8 * s_typeDepth)))
            ? (CORINFO_CLASS_STRUCT_*)((nuint)type + 8) : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodSig(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* signature, CORINFO_CLASS_STRUCT_* parent)
    {
        s_signatureMethod = (nuint)method;
        *signature = s_signature;
    }
}
