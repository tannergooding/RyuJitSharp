// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ScopeReportingTests
{
    [Test]
    public static void DisabledScopesDoNotRequireInitializedStateOrCallTheEE()
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.opts.compScopeInfo = false;
            typeof(CodeGen).GetField("varLiveKeeper", PrivateFields)!.SetValue(codeGen, null);
            typeof(CodeGen).GetField("emittedCallReturnInfo", PrivateFields)!.SetValue(codeGen, null);

            codeGen.genSetScopeInfo();

            Assert.That(context->Calls, Is.Zero);
        });
    }

    [Test]
    public static void NoRangesPublishesNullWithoutAllocating()
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            codeGen.genSetScopeInfo();

            Assert.That(context->Calls, Is.EqualTo(3));
            Assert.That(context->Count, Is.Zero);
            Assert.That((nint)context->Published, Is.EqualTo((nint)0));
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroLengthRangesAreKeptOnlyForArguments(bool parameter)
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.lvaTable[0].lvIsParam = parameter;
            AddRange(codeGen, 0, 0, 0, Register(REG_RCX), prolog: parameter);

            codeGen.genSetScopeInfo();

            Assert.That(context->AllocatedBytes, Is.EqualTo((nint)sizeof(ICorDebugInfo.NativeVarInfo)));
            Assert.That(context->Count, Is.EqualTo(parameter ? 1 : 0));
            Assert.That(context->Calls, Is.EqualTo(parameter ? 13 : 123));
            if (parameter)
            {
                AssertRange(context->Published[0], 0, 1, 0, REG_RCX);
            }
            else
            {
                Assert.That((nint)context->Published, Is.EqualTo((nint)0));
            }
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InvalidLocationsAreFilteredBeforeZeroLengthArgumentExpansion(bool parameter)
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.lvaTable[0].lvIsParam = parameter;
            AddRange(codeGen, 0, 0, parameter ? 0u : 10u, new() { vlType = VLT_INVALID });

            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.Zero);
            Assert.That(context->Calls, Is.EqualTo(123));
            Assert.That((nint)context->Published, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EqualAdjacentHomesCoalesceAcrossPrologAndBody(bool zeroLengthProlog)
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.lvaTable[0].lvIsParam = true;
            var boundary = zeroLengthProlog ? 0u : 3u;
            AddRange(codeGen, 0, 0, boundary, Register(REG_RCX), prolog: true);
            AddRange(codeGen, 0, boundary, 9, Register(REG_RCX));
            AddRange(codeGen, 0, 9, 13, Register(REG_RCX));

            codeGen.genSetScopeInfo();

            Assert.That(context->AllocatedBytes, Is.EqualTo((nint)(3 * sizeof(ICorDebugInfo.NativeVarInfo))));
            Assert.That(context->Count, Is.EqualTo(1));
            AssertRange(context->Published[0], 0, 13, 0, REG_RCX);
        });
    }

    [TestCase(3u, REG_RDX)]
    [TestCase(4u, REG_RCX)]
    public static void ChangedHomesAndGapsRemainSeparate(uint bodyStart, regNumber bodyRegister)
    {
        WithCompiler((_, codeGen, context) =>
        {
            AddRange(codeGen, 0, 0, 3, Register(REG_RCX), prolog: true);
            AddRange(codeGen, 0, bodyStart, 8, Register(bodyRegister));

            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.EqualTo(2));
            AssertRange(context->Published[0], 0, 3, 0, REG_RCX);
            AssertRange(context->Published[1], bodyStart, 8, 0, bodyRegister);
        });
    }

    [Test]
    public static void FilteredHomesStillSeparateOtherRanges()
    {
        WithCompiler((_, codeGen, context) =>
        {
            AddRange(codeGen, 0, 0, 3, Register(REG_RCX));
            AddRange(codeGen, 0, 3, 3, new() { vlType = VLT_INVALID });
            AddRange(codeGen, 0, 3, 8, Register(REG_RCX));

            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.EqualTo(2));
            AssertRange(context->Published[0], 0, 3, 0, REG_RCX);
            AssertRange(context->Published[1], 3, 8, 0, REG_RCX);
        });
    }

    [Test]
    public static void VariablesAreReportedByLocalNumberAndUnknownILLocalsAreExcluded()
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.lvaOutgoingArgSpaceVar = 1;
            AddRange(codeGen, 2, 1, 3, Register(REG_R8));
            AddRange(codeGen, 1, 0, 9, Register(REG_RDX));
            AddRange(codeGen, 0, 4, 7, Register(REG_RCX));

            codeGen.genSetScopeInfo();

            Assert.That(context->AllocatedBytes, Is.EqualTo((nint)(2 * sizeof(ICorDebugInfo.NativeVarInfo))));
            Assert.That(context->Count, Is.EqualTo(2));
            AssertRange(context->Published[0], 4, 7, 0, REG_RCX);
            AssertRange(context->Published[1], 1, 3, 2, REG_R8);
        });
    }

    [Test]
    public static void HiddenArgumentILNumbersRemainBitExact()
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.info.compRetBuffArg = 0;
            AddRange(codeGen, 0, 0, 3, Register(REG_RCX));
            AddRange(codeGen, 1, 0, 3, Register(REG_RDX));

            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.EqualTo(2));
            AssertRange(context->Published[0], 0, 3, unchecked((uint)ICorDebugInfo.RETBUF_ILNUM), REG_RCX);
            AssertRange(context->Published[1], 0, 3, 0, REG_RDX);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallReturnsFollowCompactedLocalsAndRetainOneByteRanges(bool includeLocal)
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            if (includeLocal)
            {
                AddRange(codeGen, 0, 0, 3, Register(REG_RCX));
                AddRange(codeGen, 0, 3, 8, Register(REG_RCX));
            }
            else
            {
                compiler.info.compVarScopesCount = 0;
            }
            AddCallReturn(codeGen, 12, 8, Register(REG_RAX));
            AddCallReturn(codeGen, 13, 9, new() { vlType = VLT_INVALID });
            AddCallReturn(codeGen, 14, 11, Register(REG_RDX));

            codeGen.genSetScopeInfo();

            var index = includeLocal ? 1 : 0;
            Assert.That(context->Count, Is.EqualTo(index + 2));
            AssertRange(context->Published[index], 8, 9,
                unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM), REG_RAX);
            Assert.That(context->Published[index].callReturnValueILOffset, Is.EqualTo(12));
            AssertRange(context->Published[index + 1], 11, 12,
                unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM), REG_RDX);
            Assert.That(context->Published[index + 1].callReturnValueILOffset, Is.EqualTo(14));
        });
    }

    [Test]
    public static void OnlyInvalidCallsReleaseTheAllocatedBufferAndPublishNull()
    {
        WithCompiler((_, codeGen, context) =>
        {
            AddCallReturn(codeGen, 3, 4, new() { vlType = VLT_INVALID });

            codeGen.genSetScopeInfo();

            Assert.That(context->Calls, Is.EqualTo(123));
            Assert.That(context->Count, Is.Zero);
            Assert.That((nint)context->Published, Is.EqualTo((nint)0));
        });
    }

    [Test]
    public static void FinalizedEmitterOffsetsRatherThanEstimatesDetermineReportedRanges()
    {
        WithCompiler((_, codeGen, context) =>
        {
            var descriptor = (Emitter.instrDesc)Activator.CreateInstance(
                typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)!)!;
            descriptor.idCodeSize(7);
            var group = new insGroup
            {
                igOffs = 20,
                igInsCnt = 2,
                igSize = 9,
                igFlags = InsGroupFlags.UpdatedInstructionSize,
                igData = [descriptor],
            };
#if DEBUG
            group.igSelf = group;
#endif
            var ranges = (List<CodeGen.VariableLiveKeeper.VariableLiveRange>)
                codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0);
            ranges.Add(new(Register(REG_RCX), new(group, Emitter.emitSpecifiedOffset(1, 4)),
                new(group, Emitter.emitSpecifiedOffset(2, 6))));

            codeGen.genSetScopeInfo();

            AssertRange(context->Published[0], 27, 29, 0, REG_RCX);
        });
    }

    [Test]
    public static void UnsignedRangeEndExpansionRetainsNativeWraparound()
    {
        WithCompiler((compiler, codeGen, context) =>
        {
            compiler.lvaTable[0].lvIsParam = true;
            AddRange(codeGen, 0, uint.MaxValue, uint.MaxValue, Register(REG_RCX));
            AddCallReturn(codeGen, 3, uint.MaxValue, Register(REG_RAX));

            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.EqualTo(1));
            AssertRange(context->Published[0], uint.MaxValue, 0,
                unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM), REG_RAX);
        });
    }

    [Test]
    public static void EERecordingPreservesTheWholeLocationAndDoesNotIncrementTheCount()
    {
        WithCompiler((compiler, _, context) =>
        {
            CodeGen.siVarLoc location = default;
            location.storeVariableOnStack(REG_FPBASE, -64);
            compiler.eeAllocateLVs(1);
            compiler.eeSetLVinfo(0, 2, 11, 17, ICorDebugInfo.TYPECTXT_ILNUM, in location);
            Assert.That(compiler.eeVarsCount, Is.Zero);
            compiler.eeVarsCount = 1;

            compiler.eeSetLVdone();

            Assert.That(context->Published[0].startOffset, Is.EqualTo(2));
            Assert.That(context->Published[0].endOffset, Is.EqualTo(11));
            Assert.That(context->Published[0].callReturnValueILOffset, Is.EqualTo(17));
            Assert.That(context->Published[0].varNumber, Is.EqualTo(unchecked((uint)ICorDebugInfo.TYPECTXT_ILNUM)));
            Assert.That(context->Published[0].loc.vlType, Is.EqualTo(VLT_STK));
            Assert.That(context->Published[0].loc.vlStk.vlsBaseReg, Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_RBP));
            Assert.That(context->Published[0].loc.vlStk.vlsOffset, Is.EqualTo(-64));
            Assert.That(context->Calls, Is.EqualTo(13));
        });
    }

#if DEBUG
    [Test]
    public static void DebugTranslationsUseTheLastMatchingNameAndMappedILNumber()
    {
        WithCompiler((compiler, codeGen, _) =>
        {
            compiler.info.compRetBuffArg = 0;
            compiler.info.compVarScopes =
            [
                new() { vsdLVnum = 0, vsdName = "first" },
                new() { vsdLVnum = 0, vsdName = "last" },
            ];
            compiler.info.compVarScopesCount = 2;
            AddRange(codeGen, 0, 2, 9, Register(REG_RCX));
            codeGen.genSetScopeInfo();

            var translations = (Array)typeof(CodeGen).GetField("genTrnslLocalVarInfo", PrivateFields)!.GetValue(codeGen)!;
            var translation = translations.GetValue(0)!;
            Assert.That(Field(translation, "tlviName"), Is.EqualTo("last"));
            Assert.That(Field(translation, "tlviVarNum"), Is.EqualTo(ICorDebugInfo.RETBUF_ILNUM));
            Assert.That(Field(translation, "tlviLVnum"), Is.EqualTo(0));
            Assert.That(Field(translation, "tlviStartPC"), Is.EqualTo(2u));
            Assert.That(Field(translation, "tlviLength"), Is.EqualTo((nuint)7));
            Assert.That(Field(translation, "tlviAvailable"), Is.EqualTo(true));
        });
    }

    [Test]
    public static void DebugPublicationRetainsCallAndSpecialVariableDiagnostics()
    {
        WithCompiler((compiler, codeGen, _) =>
        {
            compiler.info.compFullName = "ScopeReporting:Method";
            compiler.info.compRetBuffArg = 0;
            compiler.opts.dspDebugInfo = true;
            AddRange(codeGen, 0, 0, 3, Register(REG_RCX));
            AddCallReturn(codeGen, 7, 3, Register(REG_XMM0));

            var dump = Capture(codeGen.genSetScopeInfo);

            Assert.That(dump, Does.Contain("; Variable debug info: 2 live ranges, 0 vars for method ScopeReporting:Method"));
            Assert.That(dump, Does.Contain("( retBuff) : From 00000000h to 00000003h, in rcx"));
            Assert.That(dump, Does.Contain("(call 007) : From 00000003h to 00000004h, in mm0"));
        });
    }

    [Test]
    public static void DebugLayoutCheckUsesTheEEStorage()
    {
        CodeGen.checkICodeDebugInfo();
        Assert.That(Unsafe.SizeOf<CodeGen.siVarLoc>(), Is.EqualTo(Unsafe.SizeOf<ICorDebugInfo.VarLoc>()));
    }

    private static object? Field(object value, string name) =>
        typeof(CodeGen).GetNestedType("TrnslLocalVarInfo", BindingFlags.NonPublic)!.GetField(name)!.GetValue(value);

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
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;

    private static CodeGen.siVarLoc Register(regNumber reg)
    {
        CodeGen.siVarLoc location = default;
        location.storeVariableInRegisters(reg, REG_NA);

        return location;
    }

    private static emitLocation Location(uint offset)
    {
        var group = new insGroup { igOffs = offset };
#if DEBUG
        group.igSelf = group;
#endif
        return new emitLocation(group, 0);
    }

    private static void AddRange(CodeGen codeGen, int varNum, uint start, uint end,
        CodeGen.siVarLoc location, bool prolog = false)
    {
        var keeper = codeGen.getVariableLiveKeeper();
        var ranges = (List<CodeGen.VariableLiveKeeper.VariableLiveRange>)(prolog
            ? keeper.getLiveRangesForVarForProlog(varNum)
            : keeper.getLiveRangesForVarForBody(varNum));
        ranges.Add(new(location, Location(start), Location(end)));
    }

    private static void AddCallReturn(CodeGen codeGen, int ilOffset, uint offset, CodeGen.siVarLoc location)
    {
        var type = typeof(CodeGen).GetNestedType("EmittedCallReturnInfo", BindingFlags.NonPublic)!;
        var value = Activator.CreateInstance(type)!;
        type.GetField("callILOffset")!.SetValue(value, ilOffset);
        type.GetField("returnLocation")!.SetValue(value, Location(offset));
        type.GetField("returnValueLoc")!.SetValue(value, location);
        var list = (IList)typeof(CodeGen).GetField("emittedCallReturnInfo", PrivateFields)!.GetValue(codeGen)!;
        _ = list.Add(value);
    }

    private static void AssertRange(ICorDebugInfo.NativeVarInfo range, uint start, uint end, uint variable, regNumber reg)
    {
        Assert.That(range.startOffset, Is.EqualTo(start));
        Assert.That(range.endOffset, Is.EqualTo(end));
        Assert.That(range.varNumber, Is.EqualTo(variable));
        Assert.That(range.loc.vlType, Is.EqualTo(VLT_REG));
        Assert.That(range.loc.vlReg.vlrReg, Is.EqualTo((ICorDebugInfo.RegNum)reg));
    }

    private delegate void TestAction(Compiler compiler, CodeGen codeGen, EEContext* context);

    private static void WithCompiler(TestAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.allocateArray = &AllocateArray;
        vtable.Base.Base.freeArray = &FreeArray;
        vtable.Base.Base.setVars = &SetVars;
        var context = new EEContext { JitInfo = new() { lpVtbl = &vtable } };
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compScopeInfo = true;
        compiler.opts.compDbgInfo = true;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compTypeCtxtArg = BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.info.compLocalsCount = 3;
        compiler.info.compArgsCount = 1;
        compiler.info.compVarScopesCount = 1;
        compiler.info.compVarScopes = [new VarScopeDsc()];
        compiler.lvaCount = 3;
        compiler.lvaTable = new LclVarDsc[3];
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.initializeVariableLiveKeeper();
            var calls = typeof(CodeGen).GetField("emittedCallReturnInfo", PrivateFields)!;
            var callType = typeof(CodeGen).GetNestedType("EmittedCallReturnInfo", BindingFlags.NonPublic)!;
            calls.SetValue(codeGen, Activator.CreateInstance(typeof(List<>).MakeGenericType(callType)));
            action(compiler, codeGen, &context);
        }
        finally
        {
            NativeMemory.Free(context.Allocated);
            JitTls.Compiler = previous;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EEContext
    {
        public ICorJitInfo JitInfo;
        public void* Allocated;
        public nint AllocatedBytes;
        public ICorDebugInfo.NativeVarInfo* Published;
        public int Count;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* self, nint bytes)
    {
        var context = (EEContext*)self;
        context->Calls = (context->Calls * 10) + 1;
        context->AllocatedBytes = bytes;
        context->Allocated = NativeMemory.Alloc((nuint)bytes);

        return context->Allocated;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void FreeArray(ICorJitInfo* self, void* memory)
    {
        var context = (EEContext*)self;
        context->Calls = (context->Calls * 10) + 2;
        NativeMemory.Free(memory);
        context->Allocated = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetVars(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, int count,
        ICorDebugInfo.NativeVarInfo* variables)
    {
        var context = (EEContext*)self;
        context->Calls = (context->Calls * 10) + 3;
        context->Published = variables;
        context->Count = count;
    }
}
