// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86 && JIT32_GCENCODER
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Jit32GcInfoTests
{
    [TestCase(false, true, false, true, false, false)]
    [TestCase(false, true, false, false, false, true)]
    [TestCase(false, false, false, false, false, false)]
    [TestCase(true, true, false, true, false, true)]
    [TestCase(true, true, true, true, false, false)]
    [TestCase(true, true, true, false, false, true)]
    [TestCase(true, false, true, true, false, false)]
    [TestCase(true, false, true, true, true, true)]
    public static void UntrackedLocalClassificationMatchesJit32Rules(
        bool isParameter, bool onFrame, bool isRegisterArgument, bool tracked, bool usesJmp, bool expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_REF,
                lvIsParam = isParameter,
                lvOnFrame = onFrame,
                lvIsRegArg = isRegisterArgument,
                lvTracked = tracked,
            },
        ];
        compiler.compJmpOpUsed = usesJmp;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        JitTls.Compiler = compiler;

        try
        {
            Assert.That(codeGen.GCInfo.gcIsUntrackedLocalOrNonEnregisteredArg(0), Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void HeaderCountsOnlyUntrackedRootsAndNonemptyLifetimes()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaCount = 3;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_REF,
                lvOnFrame = true,
            },
            new LclVarDsc
            {
                Type = TYP_BYREF,
                lvIsParam = true,
                lvTracked = true,
            },
            new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
            },
        ];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        JitTls.Compiler = compiler;

        try
        {
            codeGen.GCInfo.gcVarPtrList = new GCInfo.varPtrDsc
            {
                vpdBegOfs = 4,
                vpdEndOfs = 4,
                vpdNext = new GCInfo.varPtrDsc
                {
                    vpdBegOfs = 8,
                    vpdEndOfs = 12,
                },
            };

            codeGen.GCInfo.gcCountForHeader(out var untrackedCount, out var varPtrTableSize,
                out var noGCRegionCount);

            Assert.That(untrackedCount, Is.EqualTo(1));
            Assert.That(varPtrTableSize, Is.EqualTo(1));
            Assert.That(noGCRegionCount, Is.EqualTo(0));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }
}
#endif
