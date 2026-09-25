// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.IPmappingDscKind;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenIPMappingPublicationTests
{
    private static readonly int[] s_expectedNativeOffsets = [0, 1, 1, 1, 2];
    private static readonly int[] s_expectedILOffsets = [3, -2, 0, -3, -1];

    [Test]
    public static void DisabledDebugInfoDoesNotCallTheEeOrResolveLocations()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.opts.compDbgInfo = false;
            _ = compiler.genIPmappings.AddLast(default(IPmappingDsc));
            codeGen.genIPmappingGen();

            Assert.That(context->Allocations, Is.Zero);
            Assert.That(context->Publications, Is.Zero);
            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void EmptyMappingsPublishZeroCountWithNullBuffer()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            codeGen.genIPmappingGen();

            Assert.That(context->Allocations, Is.Zero);
            Assert.That(context->Publications, Is.EqualTo(1));
            Assert.That(context->Count, Is.Zero);
            Assert.That((nint)context->Boundaries, Is.EqualTo((nint)0));
            Assert.That((nint)compiler.eeBoundaries, Is.EqualTo((nint)0));
        });
    }

    [TestCase(NoMapping, Normal, false, false, 11)]
    [TestCase(Normal, NoMapping, false, false, 10)]
    [TestCase(Normal, Normal, true, false, 10)]
    [TestCase(Normal, Normal, false, true, 11)]
    [TestCase(Normal, Normal, false, false, 11)]
    public static void CoincidentOffsetsUseTheNativeNoMappingAndLabelTiebreakers(
        IPmappingDscKind firstKind, IPmappingDscKind secondKind, bool firstLabel, bool secondLabel, int survivingIL)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            Add(compiler, codeGen, firstKind, 10, firstLabel);
            Add(compiler, codeGen, secondKind, 11, secondLabel);

            Publish(codeGen);

            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(1));
            Assert.That(context->Count, Is.EqualTo(1));
            Assert.That(context->Boundaries[0].nativeOffset, Is.Zero);
            Assert.That(context->Boundaries[0].ilOffset, Is.EqualTo(survivingIL));
            Assert.That(context->Boundaries[0].source, Is.EqualTo(ICorDebugInfo.SOURCE_TYPE_INVALID));
            Assert.That(context->Allocations, Is.EqualTo(1));
            Assert.That(context->Publications, Is.EqualTo(1));
            Assert.That((nint)compiler.eeBoundaries, Is.EqualTo((nint)0));
        });
    }

    [TestCase(Prolog, Normal, 0, -2, 0)]
    [TestCase(Normal, Epilog, 10, 10, -3)]
    [TestCase(Normal, Normal, 10, 10, 11, true)]
    public static void SpecialCoincidentBoundariesAreRetained(
        IPmappingDscKind firstKind, IPmappingDscKind secondKind, int firstIL,
        int expectedFirst, int expectedSecond, bool callOnFirst = false)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            var firstSource = callOnFirst ? ICorDebugInfo.CALL_INSTRUCTION : 0;
            Add(compiler, codeGen, firstKind, firstIL, false, firstSource);
            Add(compiler, codeGen, secondKind, firstKind == Prolog ? 0 : 11, false);
            Publish(codeGen);

            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(2));
            Assert.That(context->Count, Is.EqualTo(2));
            Assert.That(context->Boundaries[0].ilOffset, Is.EqualTo(expectedFirst));
            Assert.That(context->Boundaries[1].ilOffset, Is.EqualTo(expectedSecond));
            Assert.That(context->Boundaries[0].nativeOffset, Is.Zero);
            Assert.That(context->Boundaries[1].nativeOffset, Is.Zero);
        });
    }

    [Test]
    public static void CallInstructionOnTheLaterMappingAlsoRetainsBoth()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            Add(compiler, codeGen, Normal, 10, false);
            Add(compiler, codeGen, Normal, 11, false, ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC);
            Publish(codeGen);

            Assert.That(context->Count, Is.EqualTo(2));
            Assert.That(context->Boundaries[1].source,
                Is.EqualTo(ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC));
        });
    }

    [Test]
    public static void MultipleGroupsOfMappingsResolveActualEmitterOffsetsAndPreserveOrder()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            Add(compiler, codeGen, NoMapping, 0, false);
            Add(compiler, codeGen, Normal, 2, false, ICorDebugInfo.STACK_EMPTY);
            Add(compiler, codeGen, Normal, 3, false);
            codeGen.Emitter.emitIns(INS_nop);
            Add(compiler, codeGen, Prolog, 0, true);
            Add(compiler, codeGen, Normal, 0, false, ICorDebugInfo.ASYNC);
            Add(compiler, codeGen, Epilog, 0, false);
            codeGen.Emitter.emitIns(INS_nop);
            Add(compiler, codeGen, NoMapping, 0, false);

            Publish(codeGen);

            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(5));
            Assert.That(context->Count, Is.EqualTo(5));
            int[] nativeOffsets =
            [
                context->Boundaries[0].nativeOffset, context->Boundaries[1].nativeOffset,
                context->Boundaries[2].nativeOffset, context->Boundaries[3].nativeOffset,
                context->Boundaries[4].nativeOffset,
            ];
            int[] ilOffsets =
            [
                context->Boundaries[0].ilOffset, context->Boundaries[1].ilOffset,
                context->Boundaries[2].ilOffset, context->Boundaries[3].ilOffset,
                context->Boundaries[4].ilOffset,
            ];
            Assert.That(nativeOffsets, Is.EqualTo(s_expectedNativeOffsets));
            Assert.That(ilOffsets, Is.EqualTo(s_expectedILOffsets));
            Assert.That(context->Boundaries[2].source, Is.EqualTo(ICorDebugInfo.ASYNC));
            Assert.That(context->Boundaries[3].source, Is.EqualTo(ICorDebugInfo.STACK_EMPTY));
            Assert.That(context->Boundaries[4].source, Is.EqualTo(ICorDebugInfo.STACK_EMPTY));
        });
    }

#if DEBUG
    [Test]
    public static void DiagnosticOutputIncludesSpecialOffsetsAndAsyncFlags()
    {
        var output = CodeGenLifeTransitionTests.Capture(() =>
        {
            var mapping = new ICorDebugInfo.OffsetMapping
            {
                ilOffset = 4,
                nativeOffset = 0x15,
                source = ICorDebugInfo.STACK_EMPTY | ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC,
            };
            Compiler.eeDispLineInfo(&mapping);
            mapping.ilOffset = (int)ICorDebugInfo.MappingTypes.PROLOG;
            Compiler.eeDispLineInfo(&mapping);
        });

        Assert.That(output, Is.EqualTo(
            $"IL offs 0x0004 : 0x00000015 ( STACK_EMPTY CALL_INSTRUCTION ASYNC ){Environment.NewLine}" +
            $"IL offs PROLOG : 0x00000015 ( STACK_EMPTY CALL_INSTRUCTION ASYNC ){Environment.NewLine}"));
    }
#endif

    private static void Add(Compiler compiler, CodeGen codeGen, IPmappingDscKind kind, int ilOffset,
        bool isLabel, ICorDebugInfo.SourceTypes source = 0)
    {
        _ = compiler.genIPmappings.AddLast(new IPmappingDsc
        {
            ipmdNativeLoc = new emitLocation(codeGen.Emitter),
            ipmdKind = kind,
            ipmdLoc = kind == Normal ? new ILLocation(ilOffset, source) : default,
            ipmdIsLabel = isLabel,
        });
    }

    private static void Publish(CodeGen codeGen)
    {
        _ = Save(codeGen.Emitter, false);
        codeGen.genIPmappingGen();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool extend);

    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithPublication(PublicationAction action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.allocateArray = &AllocateArray;
            vtable.Base.Base.setBoundaries = &SetBoundaries;
            CORINFO_METHOD_STRUCT_ method = default;
            var context = new PublicationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Method = &method,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMethodHnd = &method;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];

            try
            {
                action(compiler, codeGen, &context);
                if (context.Publications != 0)
                {
                    Assert.That((nint)context.ObservedMethod, Is.EqualTo((nint)context.Method));
                }
            }
            finally
            {
                NativeMemory.Free(context.Boundaries);
            }
        });
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_METHOD_STRUCT_* Method;
        public CORINFO_METHOD_STRUCT_* ObservedMethod;
        public ICorDebugInfo.OffsetMapping* Boundaries;
        public int Count;
        public int Allocations;
        public int Publications;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* info, nint size)
    {
        var context = (PublicationContext*)info;
        context->Allocations++;
        return NativeMemory.Alloc(unchecked((nuint)size));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetBoundaries(ICorJitInfo* info, CORINFO_METHOD_STRUCT_* method,
        int count, ICorDebugInfo.OffsetMapping* boundaries)
    {
        var context = (PublicationContext*)info;
        context->ObservedMethod = method;
        context->Boundaries = boundaries;
        context->Count = count;
        context->Publications++;
    }
}
