// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class JitInterfaceBoolTests
{
    private static uint s_result;

    [TestCase(0x00000100u, false)]
    [TestCase(0x00000101u, true)]
    [TestCase(0xFFFFFF00u, false)]
    [TestCase(0xFFFFFF01u, true)]
    public static void NativeBooleanResultsIgnoreUpperRegisterBits(uint result, bool expected)
    {
        s_result = result;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        // C++ bool defines only the low byte of the return register.
        vtable.Base.Base.isIntrinsic =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, uint>)&ReturnStatic;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, uint>)&ReturnDynamic;
        vtable.logMsg =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, int, byte*, void*, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, int, byte*, void*, uint>)&ReturnJit;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var dynamicInfo = (ICorDynamicInfo*)&jitInfo;
        var staticInfo = (ICorStaticInfo*)&jitInfo;

        Assert.That(staticInfo->isIntrinsic(null), Is.EqualTo(expected));
        Assert.That(dynamicInfo->isIntrinsic(null), Is.EqualTo(expected));
        Assert.That(jitInfo.isIntrinsic(null), Is.EqualTo(expected));
        Assert.That(dynamicInfo->notifyInstructionSetUsage(default, true), Is.EqualTo(expected));
        Assert.That(jitInfo.notifyInstructionSetUsage(default, true), Is.EqualTo(expected));
        Assert.That(jitInfo.logMsg(0, null, null), Is.EqualTo(expected));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint ReturnStatic(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method) => s_result;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint ReturnDynamic(ICorJitInfo* self, CORINFO_InstructionSet instructionSet, byte support) => s_result;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint ReturnJit(ICorJitInfo* self, int level, byte* format, void* args) => s_result;
}
