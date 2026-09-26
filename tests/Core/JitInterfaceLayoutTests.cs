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

        // Measured with MSVC against corjit.h at 206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc.
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
}
