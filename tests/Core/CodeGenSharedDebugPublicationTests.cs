// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.IPmappingDscKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenSharedDebugPublicationTests
{
    [Test]
    public static void DisabledLineMappingsDoNotResolveInvalidLocationsOrCallTheEe()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.opts.compDbgInfo = false;
            _ = compiler.genIPmappings.AddLast(default(IPmappingDsc));

            codeGen.genIPmappingGen();

            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(1));
            Assert.That(context->AllocationCount, Is.Zero);
            Assert.That(context->BoundaryPublications, Is.Zero);
        });
    }

    [Test]
    public static void EmptyLineMappingsPublishNullWithoutAllocation()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            PublishLines(codeGen);

            Assert.That(context->BoundaryPublications, Is.EqualTo(1));
            Assert.That(context->BoundaryCount, Is.Zero);
            Assert.That((nint)context->Boundaries, Is.EqualTo((nint)0));
            Assert.That(context->AllocationCount, Is.Zero);
            Assert.That(context->PublicationAllocationCount, Is.Zero);
            Assert.That(compiler.eeBoundariesCount, Is.Zero);
            Assert.That((nint)compiler.eeBoundaries, Is.EqualTo((nint)0));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public static void CoincidentMappingChainsRetainNativeIteratorAdvancement(int scenario)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            int[] expected;
            switch (scenario)
            {
                case 0:
                {
                    AddLine(compiler, NoMapping, 0);
                    AddLine(compiler, Normal, 10, isLabel: true);
                    AddLine(compiler, Normal, 11);
                    expected = [10];
                    break;
                }
                case 1:
                {
                    AddLine(compiler, Normal, 10, isLabel: true);
                    AddLine(compiler, NoMapping, 0);
                    AddLine(compiler, Normal, 11);
                    expected = [10];
                    break;
                }
                case 2:
                {
                    AddLine(compiler, Prolog, 0);
                    AddLine(compiler, Normal, 0);
                    AddLine(compiler, Normal, 1);
                    expected = [(int)ICorDebugInfo.MappingTypes.PROLOG, 1];
                    break;
                }
                case 3:
                {
                    AddLine(compiler, Normal, 10, source: ICorDebugInfo.CALL_INSTRUCTION);
                    AddLine(compiler, Normal, 11);
                    AddLine(compiler, Normal, 12, isLabel: true);
                    expected = [10, 12];
                    break;
                }
                case 4:
                {
                    AddLine(compiler, Normal, 10, isLabel: true);
                    AddLine(compiler, Normal, 11, source: ICorDebugInfo.CALL_INSTRUCTION);
                    AddLine(compiler, Normal, 12);
                    expected = [10, 11, 12];
                    break;
                }
                case 5:
                {
                    AddLine(compiler, Normal, 10);
                    AddLine(compiler, Epilog, 0);
                    AddLine(compiler, Normal, 11);
                    expected = [10, 11];
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(scenario));
                }
            }

            PublishLines(codeGen);

            Assert.That(compiler.genIPmappings.Select(mapping => mapping.ipmdKind == Normal
                ? mapping.ipmdLoc.Offset : (int)ICorDebugInfo.MappingTypes.PROLOG), Is.EqualTo(expected));
            Assert.That(context->AllocationCount, Is.EqualTo(1));
            Assert.That(context->AllocationBytes0,
                Is.EqualTo((nint)(expected.Length * sizeof(ICorDebugInfo.OffsetMapping))));
            Assert.That(context->PublicationAllocationCount, Is.EqualTo(1));
            Assert.That((nint)context->Boundaries, Is.EqualTo((nint)context->Allocation0));
            Assert.That(context->BoundaryCount, Is.EqualTo(expected.Length));
            Assert.That(context->BoundaryPublications, Is.EqualTo(1));
            Assert.That(compiler.eeBoundariesCount, Is.EqualTo(expected.Length));
            var mappings = compiler.genIPmappings.ToArray();
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(context->Boundaries[index].ilOffset, Is.EqualTo(expected[index]));
                Assert.That(context->Boundaries[index].nativeOffset, Is.Zero);
                Assert.That(context->Boundaries[index].source, Is.EqualTo(mappings[index].ipmdKind == Normal
                    ? mappings[index].ipmdLoc.SourceTypes : ICorDebugInfo.STACK_EMPTY));
            }
            Assert.That((nint)compiler.eeBoundaries, Is.EqualTo((nint)0));
        });
    }

    [TestCase(0x80000000u)]
    [TestCase(0xFFFFFFFFu)]
    public static void LineRecordingAcceptsNonzeroUnsignedCountBits(uint count)
    {
        WithPublication((compiler, _, context) =>
        {
            var mapping = (ICorDebugInfo.OffsetMapping*)NativeMemory.Alloc(
                (nuint)sizeof(ICorDebugInfo.OffsetMapping));
            try
            {
                NativeMemory.Fill(mapping, (nuint)sizeof(ICorDebugInfo.OffsetMapping), 0xA5);
                // Only index zero is accessed; the unsigned extent is metadata, not the allocation size.
                compiler.eeBoundariesCount = unchecked((int)count);
                compiler.eeBoundaries = mapping;

                compiler.eeSetLIinfo(0, 0, Normal, new ILLocation(7, ICorDebugInfo.CALL_INSTRUCTION));

                Assert.That(unchecked((uint)compiler.eeBoundariesCount), Is.EqualTo(count));
                Assert.That(mapping->nativeOffset, Is.Zero);
                Assert.That(mapping->ilOffset, Is.EqualTo(7));
                Assert.That(mapping->source, Is.EqualTo(ICorDebugInfo.CALL_INSTRUCTION));
                Assert.That((nint)compiler.eeBoundaries, Is.EqualTo((nint)mapping));
                Assert.That(context->AllocationCount, Is.Zero);
                Assert.That(context->BoundaryPublications, Is.Zero);
            }
            finally
            {
                compiler.eeBoundaries = null;
                NativeMemory.Free(mapping);
            }
        });
    }

    [Test]
    public static void DisabledRichMappingsDoNotTouchInvalidInlineStateOrLocations()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 0;
            _ = compiler.genRichIPmappings.AddLast(default(RichIPMapping));

            codeGen.genReportRichDebugInfo();

            Assert.That(context->AllocationCount, Is.Zero);
            Assert.That(context->RichPublications, Is.Zero);
        }, seedInlineTree: false);
    }

    [Test]
    public static void RootOnlyRichMappingsAllocateInOrderAndTransferBothArrays()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 1;

            codeGen.genReportRichDebugInfo();

            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->AllocationBytes0, Is.EqualTo((nint)sizeof(ICorDebugInfo.InlineTreeNode)));
            Assert.That(context->AllocationBytes1, Is.EqualTo((nint)0));
            Assert.That(context->PublicationAllocationCount, Is.EqualTo(2));
            Assert.That(context->RichPublications, Is.EqualTo(1));
            Assert.That((nint)context->InlineTree, Is.EqualTo((nint)context->Allocation0));
            Assert.That((nint)context->RichMappings, Is.EqualTo((nint)context->Allocation1));
            Assert.That(context->InlineCount, Is.EqualTo(1));
            Assert.That(context->RichCount, Is.Zero);
            Assert.That((nint)context->InlineTree[0].Method, Is.EqualTo((nint)compiler.info.compMethodHnd));
            Assert.That(context->InlineTree[0].ILOffset, Is.Zero);
            Assert.That(context->InlineTree[0].Child, Is.Zero);
            Assert.That(context->InlineTree[0].Sibling, Is.Zero);
            var fieldEnd = (int)Marshal.OffsetOf<ICorDebugInfo.InlineTreeNode>(
                nameof(ICorDebugInfo.InlineTreeNode.Sibling)) + sizeof(int);
            for (var index = fieldEnd; index < sizeof(ICorDebugInfo.InlineTreeNode); index++)
            {
                Assert.That(((byte*)context->InlineTree)[index], Is.Zero);
            }
        });
    }

    [Test]
    public static void RichMappingsKeepCoincidentOffsetsAndUnsignedOffsetBitsInRecordedOrder()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 1;
            var root = compiler.compInlineContext ?? throw new AssertionException("Missing inline root.");
            var strategy = compiler._inlineStrategy ?? throw new AssertionException("Missing inline strategy.");
            var child = new InlineContext(strategy)
            {
                _callee = (CORINFO_METHOD_STRUCT_*)0x200,
                _actualCallOffset = 7,
            };
            Ordinal(child) = 1;
            InlineCount(strategy) = 1;
            root._child = child;
            var location = Location(0x80000000);
            _ = compiler.genRichIPmappings.AddLast(new RichIPMapping
            {
                nativeLoc = location,
                debugInfo = new DebugInfo(child, new ILLocation(8, ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC)),
            });
            _ = compiler.genRichIPmappings.AddLast(new RichIPMapping
            {
                nativeLoc = location,
                debugInfo = new DebugInfo(root, new ILLocation(2, ICorDebugInfo.STACK_EMPTY)),
            });

            codeGen.genReportRichDebugInfo();

            Assert.That(context->InlineCount, Is.EqualTo(2));
            Assert.That(context->RichCount, Is.EqualTo(2));
            Assert.That(context->AllocationBytes1, Is.EqualTo((nint)(2 * sizeof(ICorDebugInfo.RichOffsetMapping))));
            Assert.That(context->InlineTree[0].Child, Is.EqualTo(1));
            Assert.That(context->InlineTree[1].ILOffset, Is.EqualTo(7));
            Assert.That(context->RichMappings[0].NativeOffset, Is.EqualTo(int.MinValue));
            Assert.That(context->RichMappings[1].NativeOffset, Is.EqualTo(int.MinValue));
            Assert.That(context->RichMappings[0].Inlinee, Is.EqualTo(1));
            Assert.That(context->RichMappings[1].Inlinee, Is.Zero);
            Assert.That(context->RichMappings[0].ILOffset, Is.EqualTo(8));
            Assert.That(context->RichMappings[1].ILOffset, Is.EqualTo(2));
            Assert.That(context->RichMappings[0].Source,
                Is.EqualTo(ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC));
            Assert.That(context->RichMappings[1].Source, Is.EqualTo(ICorDebugInfo.STACK_EMPTY));
        });
    }

    [Test]
    public static void RichPublicationReusesTheSuccessfulInlineSiblingDependency()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            RichDebugConfig(ref JitConfig) = 1;
            var root = compiler.compInlineContext ?? throw new AssertionException("Missing inline root.");
            var strategy = compiler._inlineStrategy ?? throw new AssertionException("Missing inline strategy.");
            var failed = new InlineContext(strategy) { _flags = InlineContext.Flags.None };
            var child = new InlineContext(strategy) { _callee = (CORINFO_METHOD_STRUCT_*)0x200 };
            Ordinal(child) = 1;
            InlineCount(strategy) = 1;
            root._child = failed;
            failed._sibling = child;

            codeGen.genReportRichDebugInfo();

            Assert.That(context->InlineCount, Is.EqualTo(2));
            Assert.That(context->InlineTree[0].Child, Is.EqualTo(1));
            Assert.That((nint)context->InlineTree[1].Method, Is.EqualTo((nint)child.Callee));
            Assert.That(context->InlineTree[1].Sibling, Is.Zero);
        });
    }

    private static void PublishLines(CodeGen codeGen)
    {
        codeGen.genIPmappingGen();
    }

    private static void AddLine(Compiler compiler, IPmappingDscKind kind, int offset,
        bool isLabel = false, ICorDebugInfo.SourceTypes source = 0)
    {
        _ = compiler.genIPmappings.AddLast(new IPmappingDsc
        {
            ipmdNativeLoc = Location(0),
            ipmdKind = kind,
            ipmdLoc = kind == Normal ? new ILLocation(offset, source) : default,
            ipmdIsLabel = isLabel,
        });
    }

    private static emitLocation Location(uint offset)
    {
        var group = new insGroup { igOffs = offset };
#if DEBUG
        group.igSelf = group;
#endif
        return new emitLocation(group);
    }

    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithPublication(PublicationAction action, bool seedInlineTree = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousRichConfig = RichDebugConfig(ref JitConfig);
#if DEBUG
        var previousFileConfig = RichFileConfig(ref JitConfig);
        RichFileConfig(ref JitConfig) = null;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compDbgInfo = true;
        compiler.genIPmappings = [];
        compiler.genRichIPmappings = [];
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.allocateArray = &AllocateArray;
        vtable.Base.Base.setBoundaries = &SetBoundaries;
        vtable.Base.Base.reportRichMappings = &ReportRichMappings;
        CORINFO_METHOD_STRUCT_ method = default;
        var context = new PublicationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compMethodHnd = &method;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            if (seedInlineTree)
            {
                var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
                compiler._inlineStrategy = strategy;
                compiler.compInlineContext = new InlineContext(strategy) { _callee = &method, _actualCallOffset = 0 };
            }
            action(compiler, codeGen, &context);
        }
        finally
        {
            JitTls.Compiler = previous;
            RichDebugConfig(ref JitConfig) = previousRichConfig;
#if DEBUG
            RichFileConfig(ref JitConfig) = previousFileConfig;
#endif
            NativeMemory.Free(context.Allocation0);
            NativeMemory.Free(context.Allocation1);
        }
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public void* Allocation0;
        public void* Allocation1;
        public nint AllocationBytes0;
        public nint AllocationBytes1;
        public int AllocationCount;
        public ICorDebugInfo.OffsetMapping* Boundaries;
        public int BoundaryCount;
        public int BoundaryPublications;
        public ICorDebugInfo.InlineTreeNode* InlineTree;
        public int InlineCount;
        public ICorDebugInfo.RichOffsetMapping* RichMappings;
        public int RichCount;
        public int RichPublications;
        public int PublicationAllocationCount;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* info, nint size)
    {
        var context = (PublicationContext*)info;
        var bytes = unchecked((nuint)(size == 0 ? 1 : size));
        var pointer = NativeMemory.Alloc(bytes);
        NativeMemory.Fill(pointer, bytes, 0xA5);
        if (context->AllocationCount == 0)
        {
            context->Allocation0 = pointer;
            context->AllocationBytes0 = size;
        }
        else
        {
            context->Allocation1 = pointer;
            context->AllocationBytes1 = size;
        }
        context->AllocationCount++;
        return pointer;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetBoundaries(ICorJitInfo* info, CORINFO_METHOD_STRUCT_* method,
        int count, ICorDebugInfo.OffsetMapping* boundaries)
    {
        var context = (PublicationContext*)info;
        context->Boundaries = boundaries;
        context->BoundaryCount = count;
        context->BoundaryPublications++;
        context->PublicationAllocationCount = context->AllocationCount;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ReportRichMappings(ICorJitInfo* info, ICorDebugInfo.InlineTreeNode* inlineTree,
        int inlineCount, ICorDebugInfo.RichOffsetMapping* mappings, int mappingCount)
    {
        var context = (PublicationContext*)info;
        context->InlineTree = inlineTree;
        context->InlineCount = inlineCount;
        context->RichMappings = mappings;
        context->RichCount = mappingCount;
        context->RichPublications++;
        context->PublicationAllocationCount = context->AllocationCount;
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
}
