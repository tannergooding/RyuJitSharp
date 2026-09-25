// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenImmediateTests
{
    private static CorInfoReloc s_hint;
    private static nuint s_address;
    private static int s_hintCalls;

    [TestCase(0L, INS_xor)]
    [TestCase(1L, INS_mov)]
    [TestCase(-1L, INS_mov)]
    [TestCase(0xFFFFFFFFL, INS_mov)]
    [TestCase(0x100000000L, INS_mov)]
    [TestCase(long.MinValue, INS_mov)]
    [TestCase(long.MaxValue, INS_mov)]
    public static void PlainConstantsSelectZeroingOrImmediateMoveWithoutQueryingTheEe(long value, instruction expected)
    {
        WithCodeGen(CorInfoReloc.RELATIVE32, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE, REG_RAX, unchecked((nint)value));

            var descriptor = Last(codeGen);
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(s_hintCalls, Is.Zero);
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(Mask(REG_RAX)));
            if (value == 0)
            {
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_RAX));
            }
            else
            {
                Assert.That(GetConstant(codeGen.Emitter, descriptor), Is.EqualTo(unchecked((nint)value)));
            }
        });
    }

    [TestCase(false, CorInfoReloc.NONE, INS_mov, false, false)]
    [TestCase(true, CorInfoReloc.NONE, INS_mov, true, false)]
    [TestCase(false, CorInfoReloc.RELATIVE32, INS_lea, false, true)]
    [TestCase(true, CorInfoReloc.RELATIVE32, INS_lea, false, true)]
    public static void OriginalRelocationEligibilityControlsAddressMaterialization(
        bool reloc, CorInfoReloc hint, instruction expected, bool immediateReloc, bool displacementReloc)
    {
        WithCodeGen(hint, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = reloc;
            const nint address = 0x12345678;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG, REG_RAX, address);

            var descriptor = Last(codeGen);
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idIsCnsReloc(), Is.EqualTo(immediateReloc));
            Assert.That(descriptor.idIsDspReloc(), Is.EqualTo(displacementReloc));
            Assert.That(s_hintCalls, Is.EqualTo(1));
            Assert.That(s_address, Is.EqualTo((nuint)address));
            if (expected == INS_lea)
            {
                Assert.That(descriptor.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
                Assert.That(descriptor.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
                Assert.That(descriptor.idCodeSize(), Is.EqualTo(7));
            }
        });
    }

    [TestCase(false, INS_xor, 0)]
    [TestCase(true, INS_lea, 1)]
    public static void ARelocatableZeroIsNotMistakenForANullConstant(bool reloc, instruction expected, int queries)
    {
        WithCodeGen(CorInfoReloc.RELATIVE32, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = reloc;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG, REG_RAX, 0);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            Assert.That(s_hintCalls, Is.EqualTo(queries));
        });
    }

    [Test]
    public static void SectionRelativeConstantsUseMovEvenWithAPcRelativeHint()
    {
        WithCodeGen(CorInfoReloc.RELATIVE32, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG | EA_CNS_SEC_RELOC, REG_RAX, 0x1234);

            var descriptor = Last(codeGen);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idAddr().iiaSecRel, Is.True);
            Assert.That(descriptor.idIsDspReloc(), Is.False);
            Assert.That(s_hintCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public static void TlsAddressMaterializationKeepsItsData16PrefixAndDescriptorKind()
    {
        WithCodeGen(CorInfoReloc.RELATIVE32, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG | EA_CNS_TLSGD_RELOC, REG_RAX, 0x1234);

            var descriptors = CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptors.");
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_data16));
            Assert.That(descriptors[0].idCodeSize(), Is.EqualTo(1));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptors[1].idIsTlsGD(), Is.True);
            Assert.That(descriptors[1].idIsDspReloc(), Is.True);
            Assert.That(descriptors[1].idIsCnsReloc(), Is.False);
        });
    }

    [Test]
    public static void AnUnmatchedVmDoesNotReceiveRelocationQueries()
    {
        WithCodeGen(CorInfoReloc.RELATIVE32, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = false;

            codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG, REG_RAX, 0x1234);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_mov));
            Assert.That(s_hintCalls, Is.Zero);
        });
    }

    [TestCase(EA_4BYTE)]
    [TestCase(EA_8BYTE)]
    [TestCase(EA_GCREF)]
    [TestCase(EA_BYREF)]
    public static void ExplicitZeroingPreservesTheRequestedGcClassification(emitAttr attr)
    {
        WithCodeGen(CorInfoReloc.NONE, (_, codeGen) =>
        {
            codeGen.instGen_Set_Reg_To_Zero(attr, REG_RAX);

            var descriptor = Last(codeGen);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_xor));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_RAX));
            Assert.That(descriptor.idGCref(), Is.EqualTo(
                attr == EA_GCREF ? GCInfo.GCtype.GCT_GCREF :
                attr == EA_BYREF ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
        });
    }

#if DEBUG
    [TestCase(CorInfoReloc.NONE, 0)]
    [TestCase(CorInfoReloc.NONE, 1)]
    [TestCase(CorInfoReloc.RELATIVE32, 1)]
    public static void DebugHandleMetadataReachesTheSelectedDescriptor(CorInfoReloc hint, int reloc)
    {
        WithCodeGen(hint, (compiler, codeGen) =>
        {
            compiler.opts.compReloc = reloc != 0;
            var attr = EA_8BYTE | (reloc != 0 ? EA_CNS_RELOC_FLG : EA_UNKNOWN);
            var targetHandle = unchecked((nuint)0xFEDCBA9876543210UL);

            codeGen.instGen_Set_Reg_To_Imm(attr, REG_RAX, 0x1234, targetHandle: targetHandle, gtFlags: GTF_ICON_CLASS_HDL);

            var debug = Last(codeGen).idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
            Assert.That(debug.idMemCookie, Is.EqualTo(unchecked((nint)targetHandle)));
            Assert.That(debug.idFlags, Is.EqualTo(GTF_ICON_CLASS_HDL));
        });
    }
#endif

    private static void WithCodeGen(CorInfoReloc hint, Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            s_hint = hint;
            s_address = 0;
            s_hintCalls = 0;

            action(compiler, codeGen);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* address)
    {
        s_address = (nuint)address;
        s_hintCalls++;
        return s_hint;
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static Emitter.instrDesc Last(CodeGen codeGen) =>
        LastInstruction(codeGen.Emitter) ?? throw new AssertionException("No instruction was recorded.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint GetConstant(Emitter emitter, Emitter.instrDesc descriptor);
}
