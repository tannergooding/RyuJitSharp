// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenRichDebugInfoPublicationTests
{
    [Test]
    public static void DisabledRichDebugInfoDoesNotAllocateOrPublish()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 0;
            codeGen.genReportRichDebugInfo();

            Assert.That(context->AllocationCount, Is.Zero);
            Assert.That(context->PublicationCount, Is.Zero);
        });
    }

    [Test]
    public static void RootOnlyTreePublishesAnEmptyMappingArray()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 1;
            codeGen.genReportRichDebugInfo();

            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->TreeBytes, Is.EqualTo((nint)sizeof(ICorDebugInfo.InlineTreeNode)));
            Assert.That(context->MappingBytes, Is.EqualTo((nint)0));
            Assert.That(context->PublicationCount, Is.EqualTo(1));
            Assert.That(context->ContextCount, Is.EqualTo(1));
            Assert.That(context->MappingCount, Is.Zero);
            Assert.That((nint)context->InlineTree[0].Method, Is.EqualTo((nint)compiler.info.compMethodHnd));
            Assert.That(context->InlineTree[0].ILOffset, Is.Zero);
            Assert.That(context->InlineTree[0].Child, Is.Zero);
            Assert.That(context->InlineTree[0].Sibling, Is.Zero);
        });
    }

    [Test]
    public static void SuccessfulInlineTreeSkipsFailuresAndRetainsOrdinalsAndRecordedMappingOrder()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 1;
            var root = compiler.compInlineContext ?? throw new AssertionException("Missing root context.");
            var first = NewInline(compiler, 1, 0x200, 4);
            var failed = NewInline(compiler, 0, 0x300, 5);
            var second = NewInline(compiler, 2, 0x400, 6);
            var nested = NewInline(compiler, 3, 0x500, 7);
            failed._flags = InlineContext.Flags.None;
            root._child = first;
            first._sibling = failed;
            failed._sibling = second;
            second._child = nested;
            var strategy = compiler._inlineStrategy ?? throw new AssertionException("Missing inline strategy.");
            InlineCount(strategy) = 3;

            codeGen.genAddRichIPMappingHere(new DebugInfo(root, new ILLocation(2, ICorDebugInfo.STACK_EMPTY)));
            codeGen.Emitter.emitIns(INS_nop);
            codeGen.genAddRichIPMappingHere(new DebugInfo(second,
                new ILLocation(8, ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC)));
            codeGen.Emitter.emitIns(INS_nop);
            codeGen.genAddRichIPMappingHere(new DebugInfo(nested, new ILLocation(9, ICorDebugInfo.SEQUENCE_POINT)));
            _ = Save(codeGen.Emitter, false);

            codeGen.genReportRichDebugInfo();

            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->ContextCount, Is.EqualTo(4));
            Assert.That(context->MappingCount, Is.EqualTo(3));
            Assert.That(context->InlineTree[0].Child, Is.EqualTo(1));
            Assert.That((nint)context->InlineTree[1].Method, Is.EqualTo((nint)first.Callee));
            Assert.That(context->InlineTree[1].Sibling, Is.EqualTo(2));
            Assert.That(context->InlineTree[2].ILOffset, Is.EqualTo(6));
            Assert.That(context->InlineTree[2].Child, Is.EqualTo(3));
            Assert.That((nint)context->InlineTree[3].Method, Is.EqualTo((nint)nested.Callee));
            Assert.That(context->InlineTree[3].ILOffset, Is.EqualTo(7));
            Assert.That(context->InlineTree[3].Sibling, Is.Zero);

            Assert.That(context->Mappings[0].NativeOffset, Is.Zero);
            Assert.That(context->Mappings[0].Inlinee, Is.Zero);
            Assert.That(context->Mappings[0].ILOffset, Is.EqualTo(2));
            Assert.That(context->Mappings[0].Source, Is.EqualTo(ICorDebugInfo.STACK_EMPTY));
            Assert.That(context->Mappings[1].NativeOffset, Is.EqualTo(1));
            Assert.That(context->Mappings[1].Inlinee, Is.EqualTo(2));
            Assert.That(context->Mappings[1].ILOffset, Is.EqualTo(8));
            Assert.That(context->Mappings[1].Source,
                Is.EqualTo(ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC));
            Assert.That(context->Mappings[2].NativeOffset, Is.EqualTo(2));
            Assert.That(context->Mappings[2].Inlinee, Is.EqualTo(3));
            Assert.That(context->Mappings[2].ILOffset, Is.EqualTo(9));
            Assert.That(context->Mappings[2].Source, Is.EqualTo(ICorDebugInfo.SEQUENCE_POINT));
        });
    }

#if DEBUG
    [Test]
    public static void FileRecordPreservesReverseSiblingOrderAndNativeJsonFields()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            var root = compiler.compInlineContext ?? throw new AssertionException("Missing root context.");
            var first = NewInline(compiler, 1, (nint)Compiler.eeFindHelper(CORINFO_HELP_THROW), 4);
            var failed = NewInline(compiler, 0, (nint)Compiler.eeFindHelper(CORINFO_HELP_THROW), 5);
            var second = NewInline(compiler, 2, (nint)Compiler.eeFindHelper(CORINFO_HELP_THROW), 6);
            failed._flags = InlineContext.Flags.None;
            root._callee = Compiler.eeFindHelper(CORINFO_HELP_THROW);
            root._child = first;
            first._sibling = failed;
            failed._sibling = second;
            compiler.info.compMethodHnd = root.Callee;
            codeGen.genAddRichIPMappingHere(new DebugInfo(second, new ILLocation(8, ICorDebugInfo.ASYNC)));
            _ = Save(codeGen.Emitter, false);

            using var writer = new StringWriter();
            codeGen.genWriteRichDebugInfo(writer);

            var output = writer.ToString();
            Assert.That(output, Does.StartWith($"{{\"MethodID\":{(nint)root.Callee},\"InlineTree\":"));
            Assert.That(output, Does.Contain("\"Inlinees\":[{\"Ordinal\":2,"));
            Assert.That(output, Does.Contain("},{\"Ordinal\":1,"));
            Assert.That(output.IndexOf("\"Ordinal\":0,", StringComparison.Ordinal),
                Is.EqualTo(output.LastIndexOf("\"Ordinal\":0,", StringComparison.Ordinal)));
            Assert.That(output, Does.Contain("\"LocationFlags\":0,\"ExactILOffset\":6,"));
            Assert.That(output, Does.Contain("\"MethodName\":\"CORINFO_HELP_THROW\""));
            Assert.That(output, Does.EndWith(
                $"\"Mappings\":[{{\"NativeOffset\":0,\"InlineContext\":2,\"ILOffset\":8}}]}}{Environment.NewLine}"));
            Assert.That(context->AllocationCount, Is.Zero);
        });
    }
#endif

    private static InlineContext NewInline(Compiler compiler, int ordinal, nint method, int offset)
    {
        var strategy = compiler._inlineStrategy ?? throw new AssertionException("Missing inline strategy.");
        var inline = new InlineContext(strategy)
        {
            _callee = (CORINFO_METHOD_STRUCT_*)method,
            _actualCallOffset = offset,
            _location = new ILLocation(offset, ICorDebugInfo.SOURCE_TYPE_INVALID),
        };
        Ordinal(inline) = ordinal;
        return inline;
    }

    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithPublication(PublicationAction action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var previousRichConfig = RichDebugConfig(ref JitConfig);
#if DEBUG
            var previousFileConfig = RichFileConfig(ref JitConfig);
#endif
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.allocateArray = &AllocateArray;
            vtable.Base.Base.reportRichMappings = &ReportRichMappings;
            vtable.Base.Base.runWithSPMIErrorTrap = &RunWithSPMIErrorTrap;
            CORINFO_METHOD_STRUCT_ method = default;
            var context = new PublicationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMethodHnd = &method;
            var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
            compiler._inlineStrategy = strategy;
            compiler.compInlineContext = new InlineContext(strategy)
            {
                _callee = &method,
                _actualCallOffset = 0,
            };
            compiler.genRichIPmappings = [];
#if DEBUG
            RichFileConfig(ref JitConfig) = null;
#endif

            try
            {
                action(compiler, codeGen, &context);
            }
            finally
            {
                RichDebugConfig(ref JitConfig) = previousRichConfig;
#if DEBUG
                RichFileConfig(ref JitConfig) = previousFileConfig;
#endif
                NativeMemory.Free(context.InlineTree);
                NativeMemory.Free(context.Mappings);
            }
        });
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public ICorDebugInfo.InlineTreeNode* InlineTree;
        public ICorDebugInfo.RichOffsetMapping* Mappings;
        public nint TreeBytes;
        public nint MappingBytes;
        public int AllocationCount;
        public int PublicationCount;
        public int ContextCount;
        public int MappingCount;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* info, nint size)
    {
        var context = (PublicationContext*)info;
        var pointer = NativeMemory.Alloc(unchecked((nuint)(size == 0 ? 1 : size)));
        NativeMemory.Fill(pointer, unchecked((nuint)(size == 0 ? 1 : size)), 0xA5);
        if (context->AllocationCount == 0)
        {
            context->InlineTree = (ICorDebugInfo.InlineTreeNode*)pointer;
            context->TreeBytes = size;
        }
        else
        {
            context->Mappings = (ICorDebugInfo.RichOffsetMapping*)pointer;
            context->MappingBytes = size;
        }

        context->AllocationCount++;
        return pointer;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ReportRichMappings(ICorJitInfo* info, ICorDebugInfo.InlineTreeNode* inlineTree,
        int contextCount, ICorDebugInfo.RichOffsetMapping* mappings, int mappingCount)
    {
        var context = (PublicationContext*)info;
        context->InlineTree = inlineTree;
        context->ContextCount = contextCount;
        context->Mappings = mappings;
        context->MappingCount = mappingCount;
        context->PublicationCount++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithSPMIErrorTrap(ICorJitInfo* info,
        delegate* unmanaged[Cdecl]<void*, void> function, void* parameter)
    {
        function(parameter);
        return 1;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_richDebugInfo")]
    private static extern ref int RichDebugConfig(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_writeRichDebugInfoFile")]
    private static extern ref byte* RichFileConfig(ref JitConfigValues config);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_ordinal")]
    private static extern ref int Ordinal(InlineContext context);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_inlineCount")]
    private static extern ref int InlineCount(InlineStrategy strategy);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool extend);
}
