// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitInterfaceLayoutTests
{
    [TestCase(typeof(ICorStaticInfo))]
    [TestCase(typeof(ICorDynamicInfo))]
    [TestCase(typeof(ICorJitInfo))]
    [TestCase(typeof(ICorStaticInfo.Interface))]
    public static void ThreadStaticBlockInfoHasOnlyItsOutputParameter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type interfaceType)
    {
        var method = interfaceType.GetMethod(nameof(ICorStaticInfo.getThreadLocalStaticBlocksInfo))
            ?? throw new AssertionException("Missing thread-static block information method.");
        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(1));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(CORINFO_THREAD_STATIC_BLOCKS_INFO).MakePointerType()));
    }

    [Test]
    public static unsafe void ThreadStaticBlockInfoVtableMatchesPinnedSignature()
    {
        var slot = typeof(ICorStaticInfo.Vtbl<ICorStaticInfo>)
            .GetField(nameof(ICorStaticInfo.Vtbl<>.getThreadLocalStaticBlocksInfo))
            ?? throw new AssertionException("Missing thread-static block information slot.");
        Type[] expectedParameters = [typeof(ICorStaticInfo*), typeof(CORINFO_THREAD_STATIC_BLOCKS_INFO*)];
        Assert.That(slot.FieldType.GetFunctionPointerParameterTypes(), Is.EqualTo(expectedParameters));
        Assert.That(slot.FieldType.GetFunctionPointerReturnType(), Is.EqualTo(typeof(void)));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static unsafe void ThreadStaticBlockInfoWrappersForwardSelfAndOutput(int interfaceLevel)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getThreadLocalStaticBlocksInfo = &GetThreadStaticBlockInfo;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        CORINFO_THREAD_STATIC_BLOCKS_INFO info = default;

        switch (interfaceLevel)
        {
            case 0:
            {
                ((ICorStaticInfo*)&jitInfo)->getThreadLocalStaticBlocksInfo(&info);
                break;
            }

            case 1:
            {
                ((ICorDynamicInfo*)&jitInfo)->getThreadLocalStaticBlocksInfo(&info);
                break;
            }

            default:
            {
                jitInfo.getThreadLocalStaticBlocksInfo(&info);
                break;
            }
        }

        Assert.That((nint)info.threadVarsSection, Is.EqualTo((nint)(&jitInfo)));
        Assert.That(info.offsetOfBaseOfThreadLocalData, Is.EqualTo(0x12345678));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe void GetThreadStaticBlockInfo(ICorJitInfo* self, CORINFO_THREAD_STATIC_BLOCKS_INFO* info)
    {
        info->threadVarsSection = self;
        info->offsetOfBaseOfThreadLocalData = 0x12345678;
    }

    [Test]
    public static unsafe void WindowsX64LayoutsMatchPinnedNativeHeaders()
    {
        if (!OperatingSystem.IsWindows() || (RuntimeInformation.ProcessArchitecture != Architecture.X64))
        {
            Assert.Ignore("These measurements are from the Windows-x64 native headers.");
        }

        // Measured with MSVC against corjit.h at 33baf8ee337b20dd0f184b69a6f09be92850bf9e.
        // Use raw managed sizes, not marshaller layouts: the JIT passes these through pointers.
        Assert.Multiple(() => {
            Assert.That(sizeof(ICorDebugInfo.VarLoc), Is.EqualTo(16));
            Assert.That(sizeof(ICorDebugInfo.NativeVarInfo), Is.EqualTo(32));
            Assert.That(sizeof(CORINFO_ASYNC_INFO), Is.EqualTo(128));
            Assert.That(sizeof(CORINFO_EE_INFO), Is.EqualTo(80));
            Assert.That(sizeof(CORINFO_CALL_INFO), Is.EqualTo(344));
            Assert.That(sizeof(CORINFO_WASM_WELLKNOWN_GLOBALS), Is.EqualTo(32));
            Assert.That(sizeof(CORINFO_THREAD_STATIC_BLOCKS_INFO), Is.EqualTo(56));
        });

        ICorDebugInfo.NativeVarInfo info = default;
        Assert.That((byte*)&info.callReturnValueILOffset - (byte*)&info, Is.EqualTo(8));
        Assert.That((byte*)&info.loc - (byte*)&info, Is.EqualTo(16));

        CORINFO_THREAD_STATIC_BLOCKS_INFO threadStatics = default;
        Assert.That((byte*)&threadStatics.tlsIndex - (byte*)&threadStatics, Is.Zero);
        Assert.That((byte*)&threadStatics.tlsGetAddrFtnPtr - (byte*)&threadStatics, Is.EqualTo(16));
        Assert.That((byte*)&threadStatics.tlsIndexObject - (byte*)&threadStatics, Is.EqualTo(24));
        Assert.That((byte*)&threadStatics.threadVarsSection - (byte*)&threadStatics, Is.EqualTo(32));
        Assert.That((byte*)&threadStatics.offsetOfThreadLocalStoragePointer - (byte*)&threadStatics, Is.EqualTo(40));
        Assert.That((byte*)&threadStatics.offsetOfMaxThreadStaticBlocks - (byte*)&threadStatics, Is.EqualTo(44));
        Assert.That((byte*)&threadStatics.offsetOfThreadStaticBlocks - (byte*)&threadStatics, Is.EqualTo(48));
        Assert.That((byte*)&threadStatics.offsetOfBaseOfThreadLocalData - (byte*)&threadStatics, Is.EqualTo(52));
    }

    [Test]
    public static void ArrayHeadersUseHostSizedLengthSlots()
    {
        var array = default(CORINFO_Array);
        var array8 = default(CORINFO_Array8);
        var refArray = default(CORINFO_RefArray);

        ref var arrayStart = ref Unsafe.As<CORINFO_Array, byte>(ref array);
        ref var arrayLength = ref Unsafe.As<int, byte>(ref array.length);
        ref var arrayElements = ref Unsafe.As<sbyte, byte>(ref array.i1Elems);
        ref var array8Start = ref Unsafe.As<CORINFO_Array8, byte>(ref array8);
        ref var array8Length = ref Unsafe.As<int, byte>(ref array8.length);
        ref var array8Elements = ref Unsafe.As<double, byte>(ref array8.r8Elems);
        ref var refArrayStart = ref Unsafe.As<CORINFO_RefArray, byte>(ref refArray);
        ref var refArrayLength = ref Unsafe.As<int, byte>(ref refArray.length);

        var arrayLengthOffset = Unsafe.ByteOffset(ref arrayStart, ref arrayLength);
        var arrayElementsOffset = Unsafe.ByteOffset(ref arrayStart, ref arrayElements);
        var array8LengthOffset = Unsafe.ByteOffset(ref array8Start, ref array8Length);
        var array8ElementsOffset = Unsafe.ByteOffset(ref array8Start, ref array8Elements);
        var refArrayLengthOffset = Unsafe.ByteOffset(ref refArrayStart, ref refArrayLength);
        var refArrayElementsOffset = Marshal.OffsetOf<CORINFO_RefArray>(nameof(CORINFO_RefArray.refElems));

        const byte paddingSentinel = 0x5A;
        var hasHostPadding = IntPtr.Size > sizeof(int);
        if (hasHostPadding)
        {
            Unsafe.Add(ref arrayLength, sizeof(int)) = paddingSentinel;
            Unsafe.Add(ref array8Length, sizeof(int)) = paddingSentinel;
            Unsafe.Add(ref refArrayLength, sizeof(int)) = paddingSentinel;
        }

        array.length = 0x12345678;
        array8.length = 0x23456789;
        refArray.length = 0x3456789A;

        var expectedArraySize = (2 * IntPtr.Size) + sizeof(int);
        if (hasHostPadding)
        {
            expectedArraySize += sizeof(int);
        }

        Assert.That(Unsafe.SizeOf<CORINFO_ArrayLengthAndPadding>(), Is.EqualTo(IntPtr.Size));
        Assert.That(arrayLengthOffset, Is.EqualTo((nint)IntPtr.Size));
        Assert.That(arrayElementsOffset, Is.EqualTo((nint)(2 * IntPtr.Size)));
        Assert.That(Unsafe.SizeOf<CORINFO_Array>(), Is.EqualTo(expectedArraySize));
        Assert.That(array8LengthOffset, Is.EqualTo((nint)IntPtr.Size));
        Assert.That(array8ElementsOffset, Is.EqualTo((nint)(2 * IntPtr.Size)));
        Assert.That(Unsafe.SizeOf<CORINFO_Array8>(), Is.EqualTo((2 * IntPtr.Size) + sizeof(long)));
        Assert.That(refArrayLengthOffset, Is.EqualTo((nint)IntPtr.Size));
        Assert.That(refArrayElementsOffset, Is.EqualTo((nint)(2 * IntPtr.Size)));
        Assert.That(Unsafe.SizeOf<CORINFO_RefArray>(), Is.EqualTo(3 * IntPtr.Size));
        Assert.That(array.length, Is.EqualTo(0x12345678));
        Assert.That(array8.length, Is.EqualTo(0x23456789));
        Assert.That(refArray.length, Is.EqualTo(0x3456789A));

        if (hasHostPadding)
        {
            Assert.That(Unsafe.Add(ref arrayLength, sizeof(int)), Is.EqualTo(paddingSentinel));
            Assert.That(Unsafe.Add(ref array8Length, sizeof(int)), Is.EqualTo(paddingSentinel));
            Assert.That(Unsafe.Add(ref refArrayLength, sizeof(int)), Is.EqualTo(paddingSentinel));
        }
    }
}
