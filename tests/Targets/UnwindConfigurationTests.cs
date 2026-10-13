// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class UnwindConfigurationTests
{
    [Test]
    public static void CfiStorageIsAvailableWithoutAFeatureDefineAndRetainsTheNativeLayout()
    {
        Assert.That(Unsafe.SizeOf<CFI_CODE>(), Is.EqualTo(8));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.CodeOffset)), Is.EqualTo((nint)0));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.CfiOpCode)), Is.EqualTo((nint)1));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.DwarfReg)), Is.EqualTo((nint)2));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.Offset)), Is.EqualTo((nint)4));

        var descriptor = new FuncInfoDsc { cfiCodes = [new CFI_CODE(7, 2, 3, 32)] };
        Assert.That(descriptor.cfiCodes.Count, Is.EqualTo(1));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.GetUnwindInfo());
    }

    [Test]
    public static void CfiRegisterPolicyPreservesTheNativeMasksBoundsAndWidths()
    {
        if (!SupportsCfiRegisterMapping())
        {
            var failure = Assert.Throws<FatalJitException>(() => GetRegisterPolicy(null));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            return;
        }

        var (calleeSavedMask, firstFloatRegister, lastFloatRegister, lastIntegerRegister, registerSize) =
            GetRegisterPolicy(null);
        Assert.That(calleeSavedMask, Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED)));
        Assert.That(firstFloatRegister, Is.EqualTo(REG_FP_FIRST));
        Assert.That(lastFloatRegister, Is.EqualTo(REG_FP_LAST));
        Assert.That(lastIntegerRegister, Is.EqualTo(TargetRuntimeMetadata.IntegerRegisterLast));
        Assert.That(registerSize, Is.EqualTo(REGSIZE_BYTES));
    }

    [Test]
    public static void CfiDwarfMappingUsesTheSelectedArchitecture()
    {
        if (!SupportsCfiRegisterMapping())
        {
            var failure = Assert.Throws<FatalJitException>(() => MapRegister(null, REG_NA));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            for (var register = 0u; register < 16; register++)
            {
                Assert.That(MapRegister(null, (regNumber)register), Is.EqualTo((short)register));
                Assert.That(MapRegister(null, REG_ARM32_F0 + (2 * register)), Is.EqualTo((short)(256 + register)));
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            for (var register = 0u; register < 32; register++)
            {
                Assert.That(MapRegister(null, (regNumber)register), Is.EqualTo((short)register));
                Assert.That(MapRegister(null, REG_ARM64_V0 + register), Is.EqualTo((short)(64 + register)));
            }
        }
        else if (Configuration.UNIX_AMD64_ABI)
        {
            short[] dwarfRegisters = [0, 2, 1, 3, 7, 6, 4, 5];
            for (var register = 0; register < 32; register++)
            {
                var expected = register < dwarfRegisters.Length ? dwarfRegisters[register] : (short)register;
                Assert.That(MapRegister(null, (regNumber)register), Is.EqualTo(expected));
            }
        }
        else
        {
            for (var register = 0u; register < 64; register++)
            {
                Assert.That(MapRegister(null, (regNumber)register), Is.EqualTo((short)register));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CfiAllocationPreservesTheEeRoutePayloadAndUncheckedOffsets(bool matchedVm)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.allocUnwindInfo = &Allocate;
        var context = new AllocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compMatchedVM = matchedVm;
        var code = new CFI_CODE(byte.MaxValue, 2, short.MinValue, int.MinValue);

        if (Configuration.TARGET_X86 || Configuration.TARGET_WASM)
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                AllocateCfi(compiler, null, null, 0, 0, 0, null, CorJitFuncKind.CORJIT_FUNC_ROOT));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(context.Calls, Is.Zero);
            return;
        }

        AllocateCfi(compiler, (byte*)0x1000, (byte*)0x2000, 0x80000000, uint.MaxValue,
            8, (byte*)&code, CorJitFuncKind.CORJIT_FUNC_FILTER);

        Assert.That(context.Calls, Is.EqualTo(matchedVm ? 1 : 0));
        if (matchedVm)
        {
            Assert.That(context.HotCode, Is.EqualTo((nint)0x1000));
            Assert.That(context.ColdCode, Is.EqualTo((nint)0x2000));
            Assert.That(context.Start, Is.EqualTo(int.MinValue));
            Assert.That(context.End, Is.EqualTo(-1));
            Assert.That(context.Size, Is.EqualTo(8));
            Assert.That(context.Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_FILTER));
            Assert.That(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in context.Code, 1)).ToArray(),
                Is.EqualTo(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in code, 1)).ToArray()));
        }
    }

    [Test]
    public static void UnwindEmitterRejectsUnsupportedTargetsBeforeTraversingDescriptors()
    {
        if (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            Assert.Ignore("Fragmented-unwind targets support emitter padding traversal.");
        }

        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var failure = Assert.Throws<FatalJitException>(() => emitter.emitUnwindNopPadding(default, compiler));
        Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
    }

    private static bool SupportsCfiRegisterMapping()
    {
        return Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64 || Configuration.UNIX_AMD64_ABI;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetCfiRegisterPolicy")]
    private static extern (regMaskTP CalleeSavedMask, regNumber FirstFloatRegister, regNumber LastFloatRegister,
        regNumber LastIntegerRegister, int RegisterSize) GetRegisterPolicy(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "MapCfiRegNumToDwarfReg")]
    private static extern short MapRegister(Compiler? compiler, regNumber register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CfiAllocateUnwindInfo")]
    private static extern void AllocateCfi(Compiler compiler, byte* hotCode, byte* coldCode, uint startOffset,
        uint endOffset, int size, byte* block, CorJitFuncKind kind);

    private struct AllocationContext
    {
        public ICorJitInfo JitInfo;
        public int Calls;
        public nint HotCode;
        public nint ColdCode;
        public int Start;
        public int End;
        public int Size;
        public CorJitFuncKind Kind;
        public CFI_CODE Code;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, byte* hotCode, byte* coldCode, int start, int end,
        int size, byte* block, CorJitFuncKind kind)
    {
        var context = (AllocationContext*)jitInfo;
        context->Calls++;
        context->HotCode = (nint)hotCode;
        context->ColdCode = (nint)coldCode;
        context->Start = start;
        context->End = end;
        context->Size = size;
        context->Kind = kind;
        context->Code = Unsafe.ReadUnaligned<CFI_CODE>(block);
    }
}
