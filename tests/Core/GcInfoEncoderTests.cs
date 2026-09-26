// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GcSlotFlags;
using static RyuJitSharp.GcSlotState;
using static RyuJitSharp.GcStackSlotBase;
using static RyuJitSharp.GENERIC_CONTEXTPARAM_TYPE;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GcInfoEncoderTests
{
    [Test]
    public static void EmptySlimHeaderHasNativeByteLayoutAndPublishesOnce()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(16);
            encoder.FinalizeSlotIds();
            encoder.Build();
            Assert.That(encoder.GetEncodedGCInfoSize(), Is.EqualTo((nuint)0));
#if DEBUG
            Assert.That(encoder.m_CurrentMethodSize.TotalSize, Is.EqualTo((nuint)16));
            Assert.That(encoder.m_CurrentMethodSize.GetFieldSize("FlagsSize"), Is.EqualTo((nuint)4));
            Assert.That(encoder.m_CurrentMethodSize.GetFieldSize("CodeLengthSize"), Is.EqualTo((nuint)9));
            Assert.That(encoder.m_CurrentMethodSize.NumMethods, Is.EqualTo((nuint)1));
#endif

            var result = encoder.Emit();
            Assert.That((nuint)result, Is.EqualTo((nuint)context->Buffer));
            Assert.That(encoder.GetEncodedGCInfoSize(), Is.EqualTo((nuint)2));
            Assert.That(context->Size, Is.EqualTo((nint)2));
            Assert.That(context->Calls, Is.EqualTo(1));
            Assert.That(new ReadOnlySpan<byte>(result, 2).ToArray(), Is.EqualTo(new byte[] { 0x40, 0x00 }));
            Assert.Throws<InvalidOperationException>(() => encoder.Emit());
        });
    }

    [Test]
    public static void EmptyFatHeaderEncodesVarargsAndFixedOutgoingArea()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(16);
            encoder.SetIsVarArg();
            encoder.SetSizeOfStackOutgoingAndScratchArea(0);
            encoder.Build();
            var result = encoder.Emit();

            Assert.That(new ReadOnlySpan<byte>(result, (int)context->Size).ToArray(),
                        Is.EqualTo(new byte[] { 0x03, 0x80, 0x00, 0x00 }));
        });
    }

    [Test]
    public static void FatHeaderPreservesCookieContextFrameLeafEditAndContinueAndReversePInvoke()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(64);
            encoder.SetIsVarArg();
            encoder.SetGSCookieStackSlot(-16, 8, 60);
            encoder.SetGenericsInstContextStackSlot(-24, GENERIC_CONTEXTPARAM_THIS);
            encoder.SetStackBaseRegister(5);
            encoder.SetWantsReportOnlyLeaf();
            encoder.SetSizeOfEditAndContinuePreservedArea(16);
            encoder.SetReversePInvokeFrameSlot(-32);
            encoder.SetSizeOfStackOutgoingAndScratchArea(32);
            encoder.Build();
#if DEBUG
            Assert.That(encoder.m_CurrentMethodSize.TotalSize, Is.EqualTo((nuint)76));
            Assert.That(encoder.m_CurrentMethodSize.GetFieldSize("GsCookieSize"), Is.EqualTo((nuint)7));
            Assert.That(encoder.m_CurrentMethodSize.GetFieldSize("EncInfoSize"), Is.EqualTo((nuint)10));
#endif
            var result = encoder.Emit();

            Assert.That(context->Size, Is.EqualTo((nint)10));
            Assert.That(new ReadOnlySpan<byte>(result, 10).ToArray(),
                        Is.EqualTo(new byte[] { 0xEB, 0x07, 0x72, 0x90, 0xAF, 0x07, 0x30, 0xF0, 0x08, 0x00 }));
        });
    }

    [Test]
    public static void SlotDefinitionsKeepUnsignedIdentifiersAndOwnCallSiteInputs()
    {
        WithEncoder((encoder, context) =>
        {
            var live = encoder.GetRegisterSlotId(0, GC_SLOT_BASE);
            var neverLive = encoder.GetStackSlotId(-8, GC_SLOT_BASE, GC_CALLER_SP_REL);
            Assert.That(live, Is.EqualTo(0u));
            Assert.That(neverLive, Is.EqualTo(1u));

            var site = stackalloc uint[1] { 4 };
            var size = stackalloc byte[1] { 5 };
            encoder.DefineCallSites(site, size, 1);
            site[0] = 0;
            size[0] = 1;
            encoder.SetSlotState(0, live, GC_SLOT_LIVE);
            encoder.SetCodeLength(16);
            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();

            // The call-site endpoint is 9; the dead stack slot is omitted from the slot table.
            Assert.That(context->Size, Is.EqualTo((nint)4));
            Assert.That(new ReadOnlySpan<byte>(result, 4).ToArray(), Is.EqualTo(new byte[] { 0x40, 0x48, 0x0E, 0x40 }));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UntrackedSlotsSortByFlagsBeforeOffsetAndRetainStackBase(bool fatHeader)
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(32);
            if (fatHeader)
            {
                encoder.SetIsVarArg();
                encoder.SetSizeOfStackOutgoingAndScratchArea(0);
            }
            Assert.That(encoder.GetStackSlotId(-16, GC_SLOT_UNTRACKED, GC_CALLER_SP_REL), Is.EqualTo(0u));
            Assert.That(encoder.GetStackSlotId(8, GC_SLOT_UNTRACKED | GC_SLOT_INTERIOR, GC_SP_REL), Is.EqualTo(1u));
            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();

            var expected = Convert.FromHexString(fatHeader ? "030001405881F001" : "8080B002E103");
            Assert.That(context->Size, Is.EqualTo((nint)expected.Length));
            Assert.That(new ReadOnlySpan<byte>(result, expected.Length).ToArray(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void AdjacentRangesMergeAndTransitionsAtMethodEndAreDiscarded()
    {
        WithEncoder((encoder, context) =>
        {
            var slot = encoder.GetRegisterSlotId(2, GC_SLOT_INTERIOR);
            encoder.SetCodeLength(128);
            encoder.SetSizeOfStackOutgoingAndScratchArea(0);
            encoder.DefineInterruptibleRange(0, 32);
            encoder.DefineInterruptibleRange(32, 64);
            encoder.SetSlotState(0, slot, GC_SLOT_LIVE);
            encoder.SetSlotState(63, slot, GC_SLOT_DEAD);
            encoder.SetSlotState(64, slot, GC_SLOT_LIVE);
            encoder.SetSlotState(96, slot, GC_SLOT_DEAD);
            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();

            Assert.That(context->Calls, Is.EqualTo(1));
            Assert.That(encoder.GetEncodedGCInfoSize(), Is.EqualTo((nuint)context->Size));
            Assert.That(new ReadOnlySpan<byte>(result, (int)context->Size).ToArray(),
                        Is.EqualTo(new byte[]
                        {
                            0x01, 0x00, 0x04, 0x08, 0xF0, 0x0D, 0x0C, 0x89, 0x82, 0x01,
                            0xFA, 0x33,
                        }));
        });
    }

    [Test]
    public static void DenseSingleChunkTransitionsPreserveNativeSlotAndOffsetOrder()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(64);
            encoder.SetSizeOfStackOutgoingAndScratchArea(0);
            encoder.DefineInterruptibleRange(0, 64);
            var slots = new uint[]
            {
                encoder.GetRegisterSlotId(3, GC_SLOT_BASE),
                encoder.GetRegisterSlotId(1, GC_SLOT_BASE),
                encoder.GetRegisterSlotId(2, GC_SLOT_BASE),
            };

            for (uint offset = 0; offset < 36; offset++)
            {
                var state = (offset / 3 % 2 == 0) ? GC_SLOT_LIVE : GC_SLOT_DEAD;
                encoder.SetSlotState(offset, slots[offset % 3], state);
            }

            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();
            var expected = Convert.FromHexString("01000208F03B0110018E41E251D9844EAD59EE575458442E9D51EA55DB058F0E8D49E6535AC56EBD21");

            Assert.That(context->Size, Is.EqualTo((nint)expected.Length));
            Assert.That(new ReadOnlySpan<byte>(result, expected.Length).ToArray(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void CanceledTransitionsAndDenseCallSitesAreStable()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(256);
            var slots = new uint[24];

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = encoder.GetStackSlotId(-8 * (i + 1), GC_SLOT_BASE, GC_CALLER_SP_REL);
                encoder.SetSlotState(0, slots[i], GC_SLOT_LIVE);
            }

            encoder.SetSlotState(50, slots[0], GC_SLOT_DEAD);
            encoder.SetSlotState(50, slots[0], GC_SLOT_LIVE);
            var sites = new uint[32];
            var sizes = new byte[32];

            for (var i = 0; i < sites.Length; i++)
            {
                sites[i] = (uint)(i * 7);
                sizes[i] = 1;
            }

            encoder.DefineCallSites(sites, sizes);
            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();

            Assert.That(context->Calls, Is.EqualTo(1));
            Assert.That(context->Size, Is.EqualTo((nint)66));
            Assert.That(new ReadOnlySpan<byte>(result, (int)context->Size).ToArray(),
                        Is.EqualTo(Convert.FromHexString(
                            "000C402A00E1C1A28364452607E8C8A98A6B4C2D0EEFCFB09172533415F6D6B798795A5B3A808240201008040281402010080402814020100804420000000000E306")));
        });
    }

    [Test]
    public static void SeparatedRangesPreserveLiveStateAcrossGapsAndChunkBoundaries()
    {
        WithEncoder((encoder, context) =>
        {
            encoder.SetCodeLength(256);
            encoder.SetSizeOfStackOutgoingAndScratchArea(16);
            var slot = encoder.GetRegisterSlotId(1, GC_SLOT_BASE);
            encoder.DefineInterruptibleRange(8, 80);
            encoder.DefineInterruptibleRange(136, 80);
            encoder.SetSlotState(4, slot, GC_SLOT_LIVE);
            encoder.SetSlotState(100, slot, GC_SLOT_DEAD);
            encoder.SetSlotState(144, slot, GC_SLOT_LIVE);
            encoder.SetSlotState(200, slot, GC_SLOT_DEAD);
            encoder.FinalizeSlotIds();
            encoder.Build();
            var result = encoder.Emit();

            Assert.That(context->Size, Is.EqualTo((nint)19));
            Assert.That(new ReadOnlySpan<byte>(result, (int)context->Size).ToArray(),
                        Is.EqualTo(Convert.FromHexString("0100184060886700F60C8CA042B9E6508C4200")));
        });
    }

    private delegate void EncoderAction(GcInfoEncoder encoder, MockContext* context);

    private static void WithEncoder(EncoderAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.allocGCInfo = &AllocGCInfo;
#if DEBUG
        vtable.Base.Base.printMethodName = &PrintMethodName;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.getMethodClass = &GetMethodClass;
#endif
        var output = stackalloc byte[2048];
        var context = new MockContext
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            Buffer = output,
        };
        var method = default(CORINFO_METHOD_INFO);
        using var encoder = new GcInfoEncoder(&context.JitInfo, &method);
        action(encoder, &context);
    }

    private struct MockContext
    {
        public ICorJitInfo JitInfo;
        public byte* Buffer;
        public nint Size;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocGCInfo(ICorJitInfo* jitInfo, nint size)
    {
        var context = (MockContext*)jitInfo;
        context->Size = size;
        context->Calls++;
        return context->Buffer;
    }

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
        => default;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintMethodName(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
                                        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        buffer[0] = 0;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* cls,
                                       byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        buffer[0] = 0;
        return 0;
    }
#endif
}
