// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterSharedGCDiagnosticClosureTests
{
    [SetUp]
    public static void RequireDebugConfiguration()
    {
        Assume.That(TestConfiguration.IsDebug, "This fixture exercises Debug-only diagnostics.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndentationUsesTheNativeTargetsAndLiteralLength(bool diffable)
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.opts.disDiffable = diffable;
        var expected = Configuration.TARGET_AMD64 ? diffable ? 7 : 28 :
            Configuration.TARGET_X86 ? diffable ? 7 : 20 :
            Configuration.TARGET_ARM ? diffable ? 12 : 23 :
            diffable ? 12 : 29;
        Assert.That(TestConfiguration.CaptureDebugOutput(() => View.DisplayIndent(emitter)),
            Is.EqualTo(new string(' ', expected)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TrackingStatePrintsRealFieldAddressesWithNativeDiffableMasking(bool diffable)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var emitter = CreateEmitter(out var compiler);
        JitTls.Compiler = compiler;
        compiler.opts.dspDiffable = diffable;
        View.PreviousGCref(emitter) = (regMask)1;
        View.PreviousByref(emitter) = (regMask)2;
        View.InitialGCref(emitter) = (regMask)3;
        View.InitialByref(emitter) = (regMask)4;
        View.CurrentGCref(emitter) = (regMask)5;
        View.CurrentByref(emitter) = (regMask)6;

        var output = TestConfiguration.CaptureDebugOutput(() => View.DisplayGCInfo(emitter));
        var addresses = Regex.Matches(output, @"\(0x([0-9A-F]+)\)=");
        Assert.That(addresses, Has.Count.EqualTo(6));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var mask = FMT_PTR((void*)(nuint)0xD1FFAB1E);
        foreach (Match match in addresses)
        {
            var address = match.Groups[1].Value;
            Assert.That(address.Length, Is.EqualTo(nuint.Size * 2));
            if (diffable)
            {
                Assert.That(address, Is.EqualTo(mask));
            }
            else
            {
                Assert.That(address, Is.Not.EqualTo(new string('0', address.Length)));
                Assert.That(address, Is.Not.EqualTo(mask));
                Assert.That(seen.Add(address), Is.True);
            }
        }

        var registers = new string[6];
        for (var index = 0; index < registers.Length; index++)
        {
            var maskValue = new regMaskTP((regMask)(index + 1));
            registers[index] = TestConfiguration.CaptureDebugOutput(() =>
            {
                printRegMaskInt(maskValue);
                emitter.emitDispRegSet(maskValue);
            });
        }
        var normalized = Regex.Replace(output, @"\(0x[0-9A-F]+\)=", "(address)=");
        var expected = "Emitter GC tracking info:\n" +
            "  emitPrevGCrefVars {}\n" +
            $"  emitPrevGCrefRegs(address)={registers[0]}\n" +
            $"  emitPrevByrefRegs(address)={registers[1]}\n" +
            "  emitInitGCrefVars {}\n" +
            $"  emitInitGCrefRegs(address)={registers[2]}\n" +
            $"  emitInitByrefRegs(address)={registers[3]}\n" +
            "  emitThisGCrefVars {}\n" +
            $"  emitThisGCrefRegs(address)={registers[4]}\n" +
            $"  emitThisByrefRegs(address)={registers[5]}\n\n";
        Assert.That(normalized, Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
    }

    [Test]
    public static void InvalidArgumentGCTypeAssertsThenPrintsTheNativeErrorTitle()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = CreateEmitter(out var compiler);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        compiler.opts.disDiffable = true;
        var descriptor = new GCInfo.regPtrDsc
        {
            rpdArg = true,
            rpdGCtype = (GCInfo.GCtype)3,
            rpdArgType = rpdARG_POP,
        };
        descriptor.rpdCallData.rpdPtrArg = ushort.MaxValue;
        RegPtrList(ref emitter.GCInfo) = descriptor;
        RegPtrLast(ref emitter.GCInfo) = descriptor;

        var output = TestConfiguration.CaptureDebugOutput(() => View.DisplayArgumentDelta(emitter));

        Assert.That(context.Assertions, Is.EqualTo(1));
        Assert.That(context.InvalidTypeAssertions, Is.EqualTo(1));
        var indent = TestConfiguration.CaptureDebugOutput(() => View.DisplayIndent(emitter));
        Assert.That(output, Is.EqualTo($"{indent}; err arg pop 65535{Environment.NewLine}"));
        Assert.That(TestConfiguration.CaptureDebugOutput(() => View.DisplayArgumentDelta(emitter)), Is.Empty);
    }

    [Test]
    public static void InvalidPlaceholderFlagsKeepTheTwoOrderedNativeAssertions()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = CreateEmitter(out var compiler);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        var group = new insGroup
        {
            igFlags = InsGroupFlags.Placeholder | InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs,
            igPhData = new insPlaceholderGroupData(),
        };

        var output = TestConfiguration.CaptureDebugOutput(() =>
            View.DisplayGroup(emitter, group, displayFunc: false,
                displayInstructions: false, displayLocation: false));

        Assert.That(output, Does.Contain("prolog placeholder, next placeholder=<END>, gcvars, byref"));
        Assert.That(output, Does.Contain(";   InitGCVars="));
        Assert.That(context.Assertions, Is.EqualTo(2));
        Assert.That(context.PlaceholderGCvarAssertion, Is.EqualTo(1));
        Assert.That(context.PlaceholderByrefAssertion, Is.EqualTo(2));
    }

    [Test]
    public static void MetadataOnlyGroupDisplayUsesTheSharedNativeBody()
    {
        Assume.That(!Configuration.TARGET_AMD64);

        var emitter = CreateEmitter(out var compiler);
        compiler.compMethodID = 17;
        var group = new insGroup { igWeight = 100 };
        group.InitializeNum(3);
        var registers = TestConfiguration.CaptureDebugOutput(() =>
        {
            printRegMaskInt(default);
            emitter.emitDispRegSet(default);
        });

        Assert.That(TestConfiguration.CaptureDebugOutput(
            () => View.DisplayGroup(emitter, group, displayFunc: false,
                displayInstructions: false, displayLocation: false)),
            Is.EqualTo($"G_M017_IG03:        ; bbWeight=1, gcrefRegs={registers}{Environment.NewLine}"));
    }

    private static View CreateEmitter(out Compiler compiler)
    {
        compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new View(compiler);
        emitter.Init();
        return emitter;
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public int InvalidTypeAssertions;
        public int PlaceholderGCvarAssertion;
        public int PlaceholderByrefAssertion;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (AssertionContext*)self;
        context->Assertions++;
        switch (Marshal.PtrToStringUTF8((nint)expression))
        {
            case "!\"Invalid GCtype\"":
            {
                context->InvalidTypeAssertions++;
                break;
            }

            case "(ig.igFlags & InsGroupFlags.GCVars) == 0":
            {
                context->PlaceholderGCvarAssertion = context->Assertions;
                break;
            }

            case "(ig.igFlags & InsGroupFlags.ByrefRegs) == 0":
            {
                context->PlaceholderByrefAssertion = context->Assertions;
                break;
            }
        }

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrLast")]
    private static extern ref GCInfo.regPtrDsc? RegPtrLast(ref GCInfo info);

    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispInsIndent")]
        public static extern void DisplayIndent(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispRegPtrListDelta")]
        public static extern void DisplayArgumentDelta(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispGCinfo")]
        public static extern void DisplayGCInfo(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispIG")]
        public static extern void DisplayGroup(Emitter emitter, insGroup group, bool displayFunc,
            bool displayInstructions, bool displayLocation);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevGCrefRegs")]
        public static extern ref regMask PreviousGCref(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevByrefRegs")]
        public static extern ref regMask PreviousByref(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefRegs")]
        public static extern ref regMask InitialGCref(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitByrefRegs")]
        public static extern ref regMask InitialByref(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
        public static extern ref regMask CurrentGCref(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
        public static extern ref regMask CurrentByref(Emitter emitter);
    }
}
