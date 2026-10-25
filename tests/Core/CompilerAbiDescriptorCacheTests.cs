// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHFAElemType;
using static RyuJitSharp.CorInfoWasmType;
using static RyuJitSharp.SystemVClassificationType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerAbiDescriptorCacheTests
{
    private static int s_hfaTypeCalls;
    private static int s_wasmLoweringCalls;
    private static int s_systemVDescriptorCalls;

    [Test]
    public static void AbiDescriptorsCacheByClassAndCompiler()
    {
        s_hfaTypeCalls = 0;
        s_wasmLoweringCalls = 0;
        s_systemVDescriptorCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getHFAType = &GetHfaType;
        vtable.Base.Base.getWasmLowering = &GetWasmLowering;
        vtable.Base.Base.getSystemVAmd64PassStructInRegisterDescriptor = &GetSystemVDescriptor;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;

        Assert.That(firstCompiler.eeGetHfaType(firstClass), Is.EqualTo(CORINFO_HFA_ELEM_FLOAT));
        Assert.That(firstCompiler.eeGetHfaType(firstClass), Is.EqualTo(CORINFO_HFA_ELEM_FLOAT));
        Assert.That(firstCompiler.eeGetHfaType(secondClass), Is.EqualTo(CORINFO_HFA_ELEM_DOUBLE));

        Assert.That(firstCompiler.eeGetWasmLowering(firstClass), Is.EqualTo(CORINFO_WASM_TYPE_I32));
        Assert.That(firstCompiler.eeGetWasmLowering(firstClass), Is.EqualTo(CORINFO_WASM_TYPE_I32));
        Assert.That(firstCompiler.eeGetWasmLowering(secondClass), Is.EqualTo(CORINFO_WASM_TYPE_I64));

        firstCompiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(firstClass, out var firstDescriptor);
        Assert.That(firstDescriptor.passedInRegisters, Is.True);
        Assert.That(firstDescriptor.eightByteCount, Is.EqualTo(2));
        Assert.That(firstDescriptor.eightByteClassifications[0], Is.EqualTo(SystemVClassificationTypeInteger));
        Assert.That(firstDescriptor.eightByteSizes[0], Is.EqualTo(4));
        Assert.That(firstDescriptor.eightByteOffsets[0], Is.EqualTo(0));
        Assert.That(firstDescriptor.eightByteClassifications[1], Is.EqualTo(SystemVClassificationTypeSSE));
        Assert.That(firstDescriptor.eightByteSizes[1], Is.EqualTo(4));
        Assert.That(firstDescriptor.eightByteOffsets[1], Is.EqualTo(4));

        firstCompiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(firstClass, out var cachedDescriptor);
        Assert.That(cachedDescriptor.passedInRegisters, Is.True);
        Assert.That(cachedDescriptor.eightByteCount, Is.EqualTo(2));
        Assert.That(cachedDescriptor.eightByteClassifications[1], Is.EqualTo(SystemVClassificationTypeSSE));
        Assert.That(cachedDescriptor.eightByteOffsets[1], Is.EqualTo(4));

        firstCompiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(secondClass, out var secondDescriptor);
        Assert.That(secondDescriptor.passedInRegisters, Is.False);
        Assert.That(secondDescriptor.eightByteCount, Is.EqualTo(1));
        Assert.That(secondDescriptor.eightByteClassifications[0], Is.EqualTo(SystemVClassificationTypeMemory));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeGetHfaType(firstClass), Is.EqualTo(CORINFO_HFA_ELEM_FLOAT));
        Assert.That(secondCompiler.eeGetWasmLowering(firstClass), Is.EqualTo(CORINFO_WASM_TYPE_I32));
        secondCompiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(firstClass, out var secondCompilerDescriptor);
        Assert.That(secondCompilerDescriptor.eightByteCount, Is.EqualTo(2));

        Assert.Multiple(() => {
            Assert.That(s_hfaTypeCalls, Is.EqualTo(3));
            Assert.That(s_wasmLoweringCalls, Is.EqualTo(3));
            Assert.That(s_systemVDescriptorCalls, Is.EqualTo(3));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_hfaTypeCalls++;
        return (nint)classHandle == 0x101 ? CORINFO_HFA_ELEM_FLOAT : CORINFO_HFA_ELEM_DOUBLE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoWasmType GetWasmLowering(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_wasmLoweringCalls++;
        return (nint)classHandle == 0x101 ? CORINFO_WASM_TYPE_I32 : CORINFO_WASM_TYPE_I64;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetSystemVDescriptor(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle,
        SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* descriptor)
    {
        s_systemVDescriptorCalls++;
        descriptor->passedInRegisters = (nint)classHandle == 0x101;
        descriptor->eightByteCount = (byte)((nint)classHandle == 0x101 ? 2 : 1);

        if ((nint)classHandle == 0x101)
        {
            descriptor->eightByteClassifications[0] = SystemVClassificationTypeInteger;
            descriptor->eightByteSizes[0] = 4;
            descriptor->eightByteOffsets[0] = 0;
            descriptor->eightByteClassifications[1] = SystemVClassificationTypeSSE;
            descriptor->eightByteSizes[1] = 4;
            descriptor->eightByteOffsets[1] = 4;
        }
        else
        {
            descriptor->eightByteClassifications[0] = SystemVClassificationTypeMemory;
            descriptor->eightByteSizes[0] = 12;
            descriptor->eightByteOffsets[0] = 0;
        }

        return 1;
    }
}
