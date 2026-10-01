// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
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
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenSharedScopePublicationTests
{
    [TestCase(0u, 0)]
    [TestCase(1u, 0)]
    [TestCase(3u, 2)]
    public static void CapacityAllocationAndActualPublicationCountHaveSeparateOwnership(uint capacity, int count)
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.eeVarsCount = 19;
            compiler.eeAllocateLVs(capacity);
            Assert.That(compiler.eeVarsCount, Is.Zero);
            Assert.That(context->AllocatedBytes, Is.EqualTo(unchecked((nint)(capacity * sizeof(ICorDebugInfo.NativeVarInfo)))));

            var location = StackLocation();
            for (var index = 0; index < count; index++)
            {
                compiler.eeSetLVinfo(unchecked((uint)index), 2, 9, 17, index, in location);
            }
            Assert.That(compiler.eeVarsCount, Is.Zero);
            compiler.eeVarsCount = count;
            compiler.eeSetLVdone();

            Assert.That(context->Count, Is.EqualTo(count));
            Assert.That(context->Calls, Is.EqualTo(capacity == 0 ? 3 : count == 0 ? 123 : 13));
            Assert.That(context->OwnedDuringPublication, Is.True);
            Assert.That(context->MethodMatched, Is.True);
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
            if (count == 0)
            {
                Assert.That((nint)context->Published, Is.EqualTo((nint)0));
            }
            else
            {
                Assert.That(context->First.startOffset, Is.EqualTo(2u));
                Assert.That(context->Last.varNumber, Is.EqualTo(unchecked((uint)(count - 1))));
            }
        });
    }

    [TestCase(VLT_REG_FP)]
    [TestCase(VLT_REG_REG)]
    [TestCase(VLT_REG_STK)]
    [TestCase(VLT_STK_BYREF)]
    [TestCase(VLT_STK2)]
    [TestCase(VLT_FIXED_VA)]
    public static void EERecordsCopyTheWholeLocationAndPreserveUnsignedMetadata(ICorDebugInfo.VarLocType type)
    {
        WithPublication((compiler, _, context) =>
        {
            CodeGen.siVarLoc location = default;
            new Span<byte>(&location, sizeof(CodeGen.siVarLoc)).Fill(0xA5);
            location.vlType = type;
            var expected = new ReadOnlySpan<byte>(&location, sizeof(CodeGen.siVarLoc)).ToArray();
            compiler.eeAllocateLVs(1);
            compiler.eeSetLVinfo(0, uint.MaxValue, 0, uint.MaxValue, ICorDebugInfo.CALL_RETURN_ILNUM, in location);
            compiler.eeVarsCount = 1;
            compiler.eeSetLVdone();

            Assert.That(context->First.startOffset, Is.EqualTo(uint.MaxValue));
            Assert.That(context->First.endOffset, Is.Zero);
            Assert.That(context->First.callReturnValueILOffset, Is.EqualTo(uint.MaxValue));
            Assert.That(context->First.varNumber, Is.EqualTo(unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM)));
            Assert.That(new ReadOnlySpan<byte>(&context->First.loc, sizeof(ICorDebugInfo.VarLoc)).ToArray(),
                Is.EqualTo(expected));
            Assert.That(new ReadOnlySpan<byte>(&location, sizeof(CodeGen.siVarLoc)).ToArray(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void NullEEAllocationDoesNotDereferenceOptionalRecordStorage()
    {
        WithPublication((compiler, _, context) =>
        {
            context->ReturnNullAllocation = true;
            compiler.eeAllocateLVs(2);
            var location = StackLocation();
            compiler.eeSetLVinfo(1, 4, 7, 0, 0, in location);
            compiler.eeVarsCount = 1;
            compiler.eeSetLVdone();

            Assert.That(context->Calls, Is.EqualTo(13));
            Assert.That(context->Count, Is.EqualTo(1));
            Assert.That((nint)context->Published, Is.EqualTo((nint)0));
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
        });
    }

#if DEBUG
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void AllocationDiagnosticsRetainNativeSignedFormattingWithoutAllocatingHugeTables(uint capacity)
    {
        WithPublication((compiler, _, context) =>
        {
            context->ReturnNullAllocation = true;
            compiler.verbose = true;
            var output = Capture(() => compiler.eeAllocateLVs(capacity));
            compiler.verbose = false;
            compiler.eeSetLVdone();

            Assert.That(output, Is.EqualTo($"Allocating {unchecked((int)capacity)} VarLocInfo{Environment.NewLine}"));
            Assert.That(context->AllocatedBytes,
                Is.EqualTo(unchecked((nint)((nuint)capacity * (nuint)sizeof(ICorDebugInfo.NativeVarInfo)))));
            Assert.That((nint)context->Allocated, Is.EqualTo((nint)0));
            Assert.That(context->Calls, Is.EqualTo(13));
        });
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public static void EECanReleaseTransferredStorageDuringThePublicationCallback(bool release)
    {
        WithPublication((compiler, _, context) =>
        {
            context->ReleaseDuringPublication = release;
            compiler.eeAllocateLVs(1);
            var location = StackLocation();
            compiler.eeSetLVinfo(0, 3, 8, 0, ICorDebugInfo.RETBUF_ILNUM, in location);
            compiler.eeVarsCount = 1;
            compiler.eeSetLVdone();

            Assert.That(context->OwnedDuringPublication, Is.True);
            Assert.That(context->Calls, Is.EqualTo(13));
            Assert.That(context->First.loc.vlStk.vlsOffset, Is.EqualTo(-64));
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
            Assert.That(context->Allocated == null, Is.EqualTo(release));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CompactedHomesPrecedeCallReturnsAndRetainNativeEndWraparound(bool hiddenArgument)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            if (hiddenArgument)
            {
                compiler.info.compRetBuffArg = 0;
            }
            _ = AddRange(codeGen, 0, 0, 3, StackLocation(), prolog: true);
            _ = AddRange(codeGen, 0, 3, 9, StackLocation());
            _ = AddRange(codeGen, 1, 0, 7, new() { vlType = VLT_INVALID });
            AddCallReturn(codeGen, -1, 11, StackLocation());
            AddCallReturn(codeGen, 17, 12, new() { vlType = VLT_INVALID });
            AddCallReturn(codeGen, 19, uint.MaxValue, StackLocation());

            codeGen.genSetScopeInfo();

            Assert.That(context->AllocatedBytes, Is.EqualTo((nint)(6 * sizeof(ICorDebugInfo.NativeVarInfo))));
            Assert.That(context->Count, Is.EqualTo(3));
            Assert.That(context->Calls, Is.EqualTo(13));
            Assert.That(context->First.startOffset, Is.Zero);
            Assert.That(context->First.endOffset, Is.EqualTo(9u));
            Assert.That(context->First.varNumber,
                Is.EqualTo(hiddenArgument ? unchecked((uint)ICorDebugInfo.RETBUF_ILNUM) : 0u));
            Assert.That(context->Published[1].varNumber, Is.EqualTo(unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM)));
            Assert.That(context->Published[1].callReturnValueILOffset, Is.EqualTo(uint.MaxValue));
            Assert.That(context->Last.startOffset, Is.EqualTo(uint.MaxValue));
            Assert.That(context->Last.endOffset, Is.Zero);
            Assert.That(context->Last.callReturnValueILOffset, Is.EqualTo(19u));
            Assert.That((nint)compiler.eeVars, Is.EqualTo((nint)0));
        });
    }

#if TARGET_X86
    [TestCase(8, 16, 32)]
    [TestCase(-16, -8, 32)]
    [TestCase(0, 16, unchecked((int)0x80000020u))]
    public static void VarargsStackHomesAreCookieRelativeAndMutateTheOriginalRange(int cookie, int argument, int size)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            SetVarargs(compiler, cookie, argument, size);
            var range = AddRange(codeGen, 1, 2, 8, StackLocation());
            codeGen.genSetScopeInfo();

            var expected = unchecked((int)((uint)size - ((uint)argument - (uint)cookie)));
            Assert.That(context->Count, Is.EqualTo(1));
            Assert.That(context->First.loc.vlType, Is.EqualTo(VLT_FIXED_VA));
            Assert.That(context->First.loc.vlFixedVarArg.vlfvOffset, Is.EqualTo(expected));
            Assert.That(range.m_VarLocation.vlType, Is.EqualTo(VLT_FIXED_VA));
            Assert.That(range.m_VarLocation.vlFixedVarArg.vlfvOffset, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void VarargsCookieAndRegisterArgumentsKeepTheirOriginalHomes()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            SetVarargs(compiler, 8, 16, 32);
            compiler.lvaTable[1].lvIsRegArg = true;
            var cookie = AddRange(codeGen, 0, 0, 3, StackLocation());
            var argument = AddRange(codeGen, 1, 0, 3, StackLocation());
            codeGen.genSetScopeInfo();

            Assert.That(context->Count, Is.EqualTo(2));
            Assert.That(context->First.varNumber, Is.EqualTo(unchecked((uint)ICorDebugInfo.VARARGS_HND_ILNUM)));
            Assert.That(cookie.m_VarLocation.vlType, Is.EqualTo(VLT_STK));
            Assert.That(argument.m_VarLocation.vlType, Is.EqualTo(VLT_STK));
        });
    }

    [Test]
    public static void VarargsWithAnUnhomedCookieReturnBeforeRecordingOrTranslating()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            SetVarargs(compiler, 8, 16, 32);
            compiler.lvaTable[0].lvOnFrame = false;
            compiler.eeAllocateLVs(1);
            var location = StackLocation();
            codeGen.genSetScopeInfo(0, 2, 6, 1, 1, true, ref location);

            Assert.That(location.vlType, Is.EqualTo(VLT_STK));
            Assert.That(new ReadOnlySpan<byte>(compiler.eeVars, sizeof(ICorDebugInfo.NativeVarInfo)).ToArray(),
                Is.All.EqualTo((byte)0xA5));
            Assert.That(compiler.eeVarsCount, Is.Zero);
            compiler.eeSetLVdone();
            Assert.That(context->Calls, Is.EqualTo(123));
            Assert.That((nint)context->Published, Is.EqualTo((nint)0));
        });
    }

    private static void SetVarargs(Compiler compiler, int cookie, int argument, int size)
    {
        compiler.info.compIsVarArgs = true;
        compiler.lvaVarargsHandleArg = 0;
        compiler.lvaParameterStackSize = size;
        compiler.lvaTable[0].lvOnFrame = true;
        compiler.lvaTable[0].StackOffset = cookie;
        compiler.lvaTable[1].StackOffset = argument;
    }
#endif

    private static CodeGen.siVarLoc StackLocation()
    {
        CodeGen.siVarLoc location = default;
        location.storeVariableOnStack(REG_FPBASE, -64);

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

    private static CodeGen.VariableLiveKeeper.VariableLiveRange AddRange(CodeGen codeGen, int variable,
        uint start, uint end, CodeGen.siVarLoc location, bool prolog = false)
    {
        var keeper = codeGen.getVariableLiveKeeper();
        var ranges = (List<CodeGen.VariableLiveKeeper.VariableLiveRange>)(prolog
            ? keeper.getLiveRangesForVarForProlog(variable)
            : keeper.getLiveRangesForVarForBody(variable));
        var range = new CodeGen.VariableLiveKeeper.VariableLiveRange(location, Location(start), Location(end));
        ranges.Add(range);

        return range;
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

    private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithPublication(PublicationAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        CORINFO_METHOD_STRUCT_ method = default;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.allocateArray = &AllocateArray;
        vtable.Base.Base.freeArray = &FreeArray;
        vtable.Base.Base.setVars = &SetVars;
        var context = new PublicationContext
        {
            JitInfo = new() { lpVtbl = &vtable },
            ExpectedMethod = &method,
        };
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compMethodHnd = &method;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compScopeInfo = true;
        compiler.opts.compDbgInfo = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compTypeCtxtArg = BAD_VAR_NUM;
        compiler.lvaVarargsHandleArg = BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.info.compLocalsCount = 3;
        compiler.info.compArgsCount = 2;
        compiler.info.compVarScopesCount = 1;
        compiler.info.compVarScopes = [new VarScopeDsc()];
        compiler.lvaCount = 3;
        compiler.lvaTable = [new() { Type = TYP_INT }, new() { Type = TYP_INT }, new() { Type = TYP_INT }];
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.initializeVariableLiveKeeper();
            var callType = typeof(CodeGen).GetNestedType("EmittedCallReturnInfo", BindingFlags.NonPublic)!;
            typeof(CodeGen).GetField("emittedCallReturnInfo", PrivateFields)!.SetValue(codeGen,
                Activator.CreateInstance(typeof(List<>).MakeGenericType(callType)));
            action(compiler, codeGen, &context);
        }
        finally
        {
            NativeMemory.Free(context.Allocated);
            JitTls.Compiler = previous;
        }
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_METHOD_STRUCT_* ExpectedMethod;
        public void* Allocated;
        public nint AllocatedBytes;
        public ICorDebugInfo.NativeVarInfo* Published;
        public ICorDebugInfo.NativeVarInfo First;
        public ICorDebugInfo.NativeVarInfo Last;
        public int Count;
        public int Calls;
        public bool ReturnNullAllocation;
        public bool ReleaseDuringPublication;
        public bool OwnedDuringPublication;
        public bool MethodMatched;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* self, nint bytes)
    {
        var context = (PublicationContext*)self;
        context->Calls = (context->Calls * 10) + 1;
        context->AllocatedBytes = bytes;
        if (!context->ReturnNullAllocation)
        {
            context->Allocated = NativeMemory.Alloc(unchecked((nuint)bytes));
            NativeMemory.Fill(context->Allocated, unchecked((nuint)bytes), 0xA5);
        }

        return context->Allocated;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void FreeArray(ICorJitInfo* self, void* memory)
    {
        var context = (PublicationContext*)self;
        context->Calls = (context->Calls * 10) + 2;
        NativeMemory.Free(memory);
        context->Allocated = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetVars(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, int count,
        ICorDebugInfo.NativeVarInfo* variables)
    {
        var context = (PublicationContext*)self;
        context->Calls = (context->Calls * 10) + 3;
        context->Published = variables;
        context->Count = count;
        context->MethodMatched = method == context->ExpectedMethod;
        context->OwnedDuringPublication = JitTls.Compiler is Compiler compiler && compiler.eeVars == variables;
        if ((count > 0) && (variables != null))
        {
            context->First = variables[0];
            context->Last = variables[count - 1];
        }
        if (context->ReleaseDuringPublication)
        {
            NativeMemory.Free(variables);
            context->Allocated = null;
        }
    }
}
#endif
