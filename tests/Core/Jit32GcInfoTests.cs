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

    [Test]
    public static void HeaderEncoderUsesExactShortcutWhenAvailable()
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 0,
            epilogSize = 1,
            epilogCount = 1,
            revPInvokeOffset = uint.MaxValue,
        };
        var cached = -1;
        var more = -1;

        var encoding = GCInfo.EncodeHeaderFirst(in header, out var state, ref more, ref cached);

        Assert.That(encoding, Is.EqualTo(0));
        Assert.That(more, Is.Zero);
        Assert.That(cached, Is.Zero);
        Assert.That(state.IsHeaderMatch(in header), Is.True);
    }

    [Test]
    public static void HeaderEncoderAdjustsWideValuesAndOptionalFields()
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 51,
            epilogSize = 17,
            epilogCount = 2,
            epilogAtEnd = 1,
            ediSaved = 1,
            esiSaved = 1,
            ebxSaved = 1,
            ebpSaved = 1,
            ebpFrame = 1,
            interruptible = 1,
            doubleAlign = 1,
            security = 1,
            handlers = 1,
            localloc = 1,
            editNcontinue = 1,
            varargs = 1,
            profCallbacks = 1,
            genericsContext = 1,
            genericsContextIsMethodDesc = 1,
            returnKind = 3,
            isAsync = 1,
            argCount = 0x123,
            frameSize = 0x12345,
            untrackedCnt = 5,
            varPtrTableSize = 2,
            gsCookieOffset = 8,
            syncStartOffset = 4,
            syncEndOffset = 8,
            revPInvokeOffset = 12,
            noGCRegionCnt = 8,
        };
        var cached = -1;
        var more = -1;
        var encoding = GCInfo.EncodeHeaderFirst(in header, out var state, ref more, ref cached);
        var usedSecondCodeSet = false;
        var steps = 0;

        while ((encoding & 0x80) != 0)
        {
            Assert.That(steps++, Is.LessThan(64));
            encoding = GCInfo.EncodeHeaderNext(in header, ref state, out var codeSet);
            usedSecondCodeSet |= codeSet == 2;
        }

        Assert.That(state.IsHeaderMatch(in header), Is.True);
        Assert.That(usedSecondCodeSet, Is.True);
    }

    [Test]
    public static void HeaderSaveMeasurementMatchesSerializedSize()
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
        var returnTypeDesc = new ReturnTypeDesc();
        returnTypeDesc.InitializeReturnType(compiler, TYP_REF, null, default);
        compiler.compRetTypeDesc = returnTypeDesc;
        compiler.lvaCount = 0;
        compiler.lvaTable = [];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerUsed = false;
        JitTls.Compiler = compiler;
        codeGen.RegSet.rsClearRegsModified();

        try
        {
#pragma warning disable IDE0007
            byte* destination = stackalloc byte[64];
#pragma warning restore IDE0007
            var header = default(GCInfo.InfoHdr);
            var cached = -1;
            var measuredSize = codeGen.GCInfo.gcInfoBlockHdrSave(destination, 0, 0x123, 0, 0,
                ref header, ref cached);
            var serializedSize = codeGen.GCInfo.gcInfoBlockHdrSave(destination, -1, 0x123, 0, 0,
                ref header, ref cached);

            Assert.That(serializedSize, Is.EqualTo(measuredSize));
            Assert.That(header.returnKind, Is.EqualTo(1));
#if VERIFY_GC_TABLES
            Assert.That(*(ushort*)destination, Is.EqualTo(0xFEEF));
            Assert.That(destination[2], Is.EqualTo(0x82));
            Assert.That(destination[3], Is.EqualTo(0x23));
#else
            Assert.That(destination[0], Is.EqualTo(0x82));
            Assert.That(destination[1], Is.EqualTo(0x23));
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }
}
#endif
