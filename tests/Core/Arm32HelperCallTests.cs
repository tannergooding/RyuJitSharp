// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorInfoReloc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32HelperCallTests
{
    [Test]
    public static void InRangeHelperAddressesUseDirectThumbCalls()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            Assert.That(compiler.opts.OptimizationDisabled, Is.False);
            const nint helperAddress = 0x1234;
            using var callbacks = new HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)helperAddress);
            compiler.opts.compReloc = true;
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var call = LastInstruction(codeGen.Emitter);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_FAIL_FAST));
            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(call.idIns(), Is.EqualTo(INS_bl));
            Assert.That((nint)call.idAddr().iiaAddr, Is.EqualTo(helperAddress));
            Assert.That(call.idIsDspReloc(), Is.True);
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    [Test]
    public static void OutOfRangeHelperAddressesUseTheDefaultCallTargetRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            const nint helperAddress = 0x12345678;
            using var callbacks = new HelperCallbacks(compiler, IAT_VALUE, NONE, (void*)helperAddress);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_blx]));
            Assert.That(descriptors[^1].idReg3(), Is.EqualTo(REG_R12));
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    [Test]
    public static void IndirectHelperAddressesLoadTheCellBeforeCallingThroughTheDefaultRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            using var callbacks = new HelperCallbacks(compiler, IAT_PVALUE, NONE, (void*)0x12345678);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_ldr, INS_blx]));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R12));
            Assert.That(descriptors[3].idReg3(), Is.EqualTo(REG_R12));
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    private static void AssertCalleeTrashAccounting(CodeGen codeGen)
        => Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(),
            Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH)));

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    private static Emitter.instrDesc LastInstruction(Emitter emitter)
        => Descriptors(emitter)[^1];

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    internal sealed class HelperCallbacks : IDisposable
    {
        private readonly ICorJitInfo.Vtbl<ICorJitInfo>* _vtbl;
        private readonly ICorJitInfo* _jitInfo;

        public static CorInfoHelpFunc Helper => s_helper;
        public static int Assertions => s_assertions;

        public HelperCallbacks(
            Compiler compiler, InfoAccessType accessType, CorInfoReloc relocation, void* address)
        {
            s_accessType = accessType;
            s_relocation = relocation;
            s_address = address;
            s_helper = CORINFO_HELP_UNDEF;
            s_assertions = 0;
            _vtbl = (ICorJitInfo.Vtbl<ICorJitInfo>*)NativeMemory.AllocZeroed(
                (nuint)sizeof(ICorJitInfo.Vtbl<ICorJitInfo>));
            _jitInfo = (ICorJitInfo*)NativeMemory.AllocZeroed((nuint)sizeof(ICorJitInfo));
            _vtbl->Base.getHelperFtn = &GetHelperFtn;
            _vtbl->getRelocTypeHint = &GetRelocTypeHint;
            _vtbl->doAssert = &RecordAssertion;
            _jitInfo->lpVtbl = _vtbl;
            compiler.info.compCompHnd = _jitInfo;
            compiler.info.compMatchedVM = true;
        }

        public void Dispose()
        {
            NativeMemory.Free(_jitInfo);
            NativeMemory.Free(_vtbl);
        }
    }

    private static InfoAccessType s_accessType;
    private static CorInfoReloc s_relocation;
    private static void* s_address;
    private static CorInfoHelpFunc s_helper;
    private static int s_assertions;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(
        ICorJitInfo* self, CorInfoHelpFunc helper, CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = s_accessType;
        lookup->addr = s_address;
        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address)
        => s_relocation;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        return 0;
    }
}
#endif
