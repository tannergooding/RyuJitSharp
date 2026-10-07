// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARMARCH
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if TARGET_ARM64
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArmArchStackArgumentTests
{
    [TestCase(TYP_BYTE, REG_R1)]
    [TestCase(TYP_USHORT, REG_R2)]
    [TestCase(TYP_INT, REG_R3)]
    [TestCase(TYP_REF, REG_R1)]
    [TestCase(TYP_BYREF, REG_R2)]
#if TARGET_ARM64
    [TestCase(TYP_FLOAT, REG_V0)]
    [TestCase(TYP_DOUBLE, REG_V0)]
    [TestCase(TYP_LONG, REG_R3)]
#else
    [TestCase(TYP_FLOAT, REG_F0)]
    [TestCase(TYP_DOUBLE, REG_F0)]
#endif
    public static void DispatchStoresScalarsAndConsumesTheirGcRegisters(var_types type, regNumber reg)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = Physical(type, reg);
            codeGen.GCInfo.gcMarkRegPtrVal(reg, type);
            var argument = Argument(source, 32, TARGET_POINTER_SIZE);

            codeGen.genCodeForTreeNode(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], codeGen.ins_Store(type.ActualType), type.ActualType.EmitSize, 1, 32, reg);
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GcType(type)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            AssertConsumed(source);
        });
    }

    [TestCase(1, TYP_BYTE)]
    [TestCase(2, TYP_SHORT)]
    public static void SmallSlotsUseApplePackingOnly(int size, var_types packedType)
    {
        WithStackArea((_, codeGen) =>
        {
            var source = Physical(TYP_INT, REG_R1);
            var argument = Argument(source, 32, size);
            var storeType = compAppleArm64Abi() ? packedType : TYP_INT;

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], codeGen.ins_Store(storeType), storeType.EmitSize, 1, 32, REG_R1);
        });
    }

#if FEATURE_FASTTAILCALL
    [Test]
    public static void FastTailCallsUseTheFirstStackParameterAndIncomingAreaSize()
    {
        WithStackArea((compiler, codeGen) =>
        {
            compiler.info.compArgsCount = 2;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_R0, 0, TARGET_POINTER_SIZE)),
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.OnStack(0, 0, TARGET_POINTER_SIZE)),
            ];
            compiler.lvaOutgoingArgSpaceVar = 0;
            compiler.lvaOutgoingArgSpaceSize.Value = 0;
            var call = new GenTreeCall(TYP_VOID);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
            var source = Physical(TYP_INT, REG_R1);
            var argument = new GenTreePutArgStk(TYP_VOID, source, call, 32, TARGET_POINTER_SIZE, true);

            codeGen.genCodeForTreeNode(argument);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            AssertStack(Descriptors(codeGen)[0], INS_str, EA_4BYTE, 1, 32, REG_R1);
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void FieldListsKeepFieldOffsetsWidthsOrderAndGcAttributes(bool dispatch)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var small = Physical(TYP_INT, REG_R1);
            var reference = Physical(TYP_REF, REG_R2);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddFieldLIR(compiler, small, 0, TYP_BYTE);
            fields.AddFieldLIR(compiler, reference, TARGET_POINTER_SIZE, TYP_REF);
#if DEBUG
            small.UseNum = 0;
            reference.UseNum = 1;
#endif
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R2, TYP_REF);
            var argument = Argument(fields, 32, 2 * TARGET_POINTER_SIZE);

            if (dispatch)
            {
                codeGen.genCodeForTreeNode(argument);
            }
            else
            {
                codeGen.genPutArgStk(argument);
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            AssertStack(descriptors[0], INS_strb, EA_1BYTE, 1, 32, REG_R1);
            AssertStack(descriptors[1], INS_str, EA_PTRSIZE, 1, 32 + TARGET_POINTER_SIZE, REG_R2);
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            AssertConsumed(small);
            AssertConsumed(reference);
        });
    }

    [TestCase(1, false, false)]
    [TestCase(3, true, false)]
    [TestCase(7, true, false)]
    [TestCase(7, false, true)]
    [TestCase(12, false, false)]
    [TestCase(12, false, true)]
    [TestCase(12, true, true)]
    [TestCase(16, false, false)]
    [TestCase(16, true, false)]
    public static void StructCopiesPreserveChunkOrderOffsetsAndLocalOnlyWidening(
        int size, bool indirect, bool padded)
    {
        WithStackArea((_, codeGen) =>
        {
            var layout = new ClassLayout(size);
            var source = StructSource(layout, indirect, REG_R3);
            var stackSize = padded ? (int)roundUp((uint)size, TARGET_POINTER_SIZE) : size;
            var argument = Argument(source, 32, stackSize);
            AddTemps(codeGen, argument);

            codeGen.genCodeForTreeNode(argument);

            var copiedSize = indirect ? size : stackSize;
            var descriptors = Descriptors(codeGen);
            var index = 0;
            var offset = 0;
            while (offset < copiedSize)
            {
                var remaining = copiedSize - offset;
#if TARGET_ARM64
                var pair = remaining >= 16;
                var width = pair ? 16 : remaining >= 8 ? 8 : remaining >= 4 ? 4 : remaining >= 2 ? 2 : 1;
#else
                var pair = false;
                var width = remaining >= 4 ? 4 : remaining >= 2 ? 2 : 1;
#endif
                var load = descriptors[index++];
                var store = descriptors[index++];
                var loadIns = width == 1 ? INS_ldrb : width == 2 ? INS_ldrh : INS_ldr;
                var storeIns = width == 1 ? INS_strb : width == 2 ? INS_strh : INS_str;
#if TARGET_ARM64
                if (pair)
                {
                    loadIns = INS_ldp;
                    storeIns = INS_stp;
                }
#endif
                Assert.That(load.idIns(), Is.EqualTo(loadIns));
                Assert.That(store.idIns(), Is.EqualTo(storeIns));
                var descriptorWidth = pair ? TARGET_POINTER_SIZE : Math.Max(4, width);
                Assert.That(load.idOpSize(), Is.EqualTo((emitAttr)descriptorWidth));
                Assert.That(store.idOpSize(), Is.EqualTo(load.idOpSize()));
                Assert.That(load.idReg1(), Is.EqualTo(REG_R1));
                Assert.That(store.idReg1(), Is.EqualTo(REG_R1));
                if (indirect)
                {
                    Assert.That(load.idReg2(), Is.EqualTo(pair ? REG_R2 : REG_R3));
                    if (pair)
                    {
                        Assert.That(load.idReg3(), Is.EqualTo(REG_R3));
                    }

                    var displacement = offset;
#if TARGET_ARM64
                    displacement /= pair ? 8 : width;
                    Assert.That(Emitter.emitGetInsSC(load), Is.EqualTo((nint)displacement));
#else
                    Assert.That(codeGen.Emitter.emitGetInsSC(load), Is.EqualTo((nint)displacement));
#endif
                }
                else
                {
                    Assert.That(load.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(2));
                    Assert.That(load.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)(8 + offset)));
                }
                Assert.That(store.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(1));
                Assert.That(store.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)(32 + offset)));
                offset += width;
            }
            Assert.That(index, Is.EqualTo(descriptors.Count));
            Assert.That(offset, Is.EqualTo(copiedSize));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructCopiesReportGcTypesForEachPointerSlot(bool indirect)
    {
        WithStackArea((_, codeGen) =>
        {
            var layout = new ClassLayout(2 * TARGET_POINTER_SIZE) { GCPtrCount = 2 };
            layout._inlineGCPtrs[0] = CorInfoGCType.TYPE_GC_REF;
            layout._inlineGCPtrs[1] = CorInfoGCType.TYPE_GC_BYREF;
            var argument = Argument(StructSource(layout, indirect, REG_R3), 32, 2 * TARGET_POINTER_SIZE);
            AddTemps(codeGen, argument);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
#if TARGET_ARM64
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            // The pinned indirect LDP passes type0 for both attributes; retain that contract.
            Assert.That(descriptors[0].idGCrefReg2(),
                Is.EqualTo(indirect ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_BYREF));
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(descriptors[1].idGCrefReg2(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
#else
            Assert.That(descriptors, Has.Count.EqualTo(4));
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(descriptors[2].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            Assert.That(descriptors[3].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
#endif
        });
    }

#if TARGET_ARM64
    [TestCase(REG_R1, REG_R2, REG_R1)]
    [TestCase(REG_R2, REG_R1, REG_R2)]
    public static void IndirectPairsOnlyOverwriteTheAddressAfterItsLastLoad(
        regNumber addressReg, regNumber lowReg, regNumber highReg)
    {
        WithStackArea((_, codeGen) =>
        {
            var argument = Argument(StructSource(new ClassLayout(16), true, addressReg), 32, 16);
            AddTemps(codeGen, argument);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldp));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(lowReg));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(highReg));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(addressReg));
            AssertStack(descriptors[1], INS_stp, EA_8BYTE, 1, 32, lowReg);
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(highReg));
        });
    }

    [TestCase(TYP_INT, 8)]
    [TestCase(TYP_REF, 8)]
    [TestCase(TYP_INT, 1)]
    [TestCase(TYP_INT, 2)]
    public static void ContainedZeroStoresUseTheZeroRegisterWithoutConsumption(var_types type, int size)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = compiler.gtNewIconNode(type, 0);
            source.IsContained = true;
            var argument = Argument(source, 32, size);
            var storeType = compAppleArm64Abi() && size < 4 ? size == 1 ? TYP_BYTE : TYP_SHORT : type;

            codeGen.genCodeForTreeNode(argument);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            AssertStack(Descriptors(codeGen)[0], codeGen.ins_Store(storeType), storeType.EmitSize, 1, 32, REG_ZR);
#if DEBUG
            Assert.That((source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED) == 0, Is.True);
#endif
        });
    }

    [TestCase(TYP_SIMD8, 8)]
    [TestCase(TYP_SIMD16, 16)]
    public static void SimdArgumentsStoreTheirRegisterWidth(var_types type, int size)
    {
        WithStackArea((_, codeGen) =>
        {
            var source = Physical(type, REG_V0);
            codeGen.genCodeForTreeNode(Argument(source, 32, size));

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            AssertStack(Descriptors(codeGen)[0], INS_str, type.EmitSize, 1, 32, REG_V0);
            AssertConsumed(source);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void Simd12StoresExactlyTwelveBytesAndPreservesExtractionOrder(bool temporary, bool fieldList)
    {
        if (!fieldList && !compAppleArm64Abi())
        {
            Assert.Ignore("Twelve-byte scalar SIMD stack slots require the Apple ARM64 ABI.");
        }

        WithStackArea((compiler, codeGen) =>
        {
            var vector = Physical(TYP_SIMD16, REG_V0);
            GenTree source = vector;
            if (fieldList)
            {
                var fields = new GenTreeFieldList { IsContained = true };
                fields.AddFieldLIR(compiler, vector, 0, TYP_SIMD12);
                source = fields;
            }
            var argument = Argument(source, 32, 12);
            if (temporary)
            {
                codeGen.InternalRegisters.Add(argument, Mask(REG_R1));
            }

            codeGen.genCodeForTreeNode(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(temporary ? 3 : 4));
            AssertStack(descriptors[0], INS_str, EA_8BYTE, 1, 32, REG_V0);
            Assert.That(descriptors[1].idIns(), Is.EqualTo(temporary ? INS_mov : INS_ext));
            AssertStack(descriptors[2], INS_str, EA_4BYTE, 1, 40, temporary ? REG_R1 : REG_V0);
            if (!temporary)
            {
                Assert.That(descriptors[3].idIns(), Is.EqualTo(INS_ext));
            }
            AssertConsumed(vector);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HfaStructsCopyAllFourPointerChunks(bool indirect)
    {
        WithStackArea((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getHFAType = &GetHfaType;
            ICorJitInfo ee = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)1, true, 32, TYP_STRUCT, "Hfa", "Hfa");
            var argument = Argument(StructSource(layout, indirect, REG_R3), 32, 32);
            AddTemps(codeGen, argument);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(4));
            AssertStack(descriptors[1], INS_stp, EA_8BYTE, 1, 32, REG_R1);
            AssertStack(descriptors[3], INS_stp, EA_8BYTE, 1, 48, REG_R1);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        return CorInfoHFAElemType.CORINFO_HFA_ELEM_DOUBLE;
    }
#else
    [Test]
    public static void RetypedDoubleStoresBothIntegerRegistersAtConsecutiveWordOffsets()
    {
        WithStackArea((_, codeGen) =>
        {
            var source = new GenTreeCopyOrReload(GT_RELOAD, TYP_LONG, Physical(TYP_DOUBLE, REG_F0));
            source.SetRegNumByIdx(REG_R1, 0);
            source.SetRegNumByIdx(REG_R2, 1);
            codeGen.genCodeForTreeNode(Argument(source, 32, 8));

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            AssertStack(descriptors[0], INS_str, TYP_LONG.EmitSize, 1, 32, REG_R1);
            AssertStack(descriptors[1], INS_str, TYP_LONG.EmitSize, 1, 36, REG_R2);
            AssertConsumed(source);
        });
    }

    [Test]
    public static void FinalWordLoadMayOverwriteItsAddressRegister()
    {
        WithStackArea((_, codeGen) =>
        {
            var argument = Argument(StructSource(new ClassLayout(4), true, REG_R1), 32, 4);
            AddTemps(codeGen, argument);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            AssertStack(descriptors[1], INS_str, EA_4BYTE, 1, 32, REG_R1);
        });
    }
#endif

    private static void WithStackArea(Action<Compiler, CodeGen> action)
    {
#if TARGET_ARM64
        Arm64CodeGenLocalVariableTests.WithCodeGen(Initialize);
#else
        ArmCalleeSavedRegisterTests.WithCodeGen(Initialize);
#endif

        void Initialize(Compiler compiler, CodeGen codeGen)
        {
            compiler.lvaCount = 3;
            compiler.lvaTable =
            [
                new() { Type = TYP_I_IMPL, lvOnFrame = true, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(128), lvOnFrame = true,
                    StackOffset = 0, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(64), lvOnFrame = true,
                    StackOffset = 128, RegNum = REG_STK },
            ];
            compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaOutgoingArgSpaceSize.Value = 128;
            compiler.lvaParameterStackSize = 128;
            codeGen.IsFramePointerUsed = false;
            action(compiler, codeGen);
        }
    }

    private static GenTreePutArgStk Argument(GenTree source, int offset, int size)
    {
        return new GenTreePutArgStk(TYP_VOID, source, new GenTreeCall(TYP_VOID), offset, size, false);
    }

    private static GenTreePhysReg Physical(var_types type, regNumber reg)
    {
        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    private static GenTree StructSource(ClassLayout layout, bool indirect, regNumber addressReg)
    {
        if (indirect)
        {
            return new GenTreeBlk(TYP_STRUCT, Physical(TYP_BYREF, addressReg), layout) { IsContained = true };
        }

        return new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 2, 8) { Layout = layout, IsContained = true };
    }

    private static void AddTemps(CodeGen codeGen, GenTreePutArgStk argument)
    {
        var temps = Mask(REG_R1);
#if TARGET_ARM64
        temps |= Mask(REG_R2);
#endif
        codeGen.InternalRegisters.Add(argument, temps);
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private static GCInfo.GCtype GcType(var_types type)
    {
        return type switch
        {
            TYP_REF => GCInfo.GCtype.GCT_GCREF,
            TYP_BYREF => GCInfo.GCtype.GCT_BYREF,
            _ => GCInfo.GCtype.GCT_NONE,
        };
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    private static void AssertStack(Emitter.instrDesc descriptor, instruction ins, emitAttr size,
        int home, int offset, regNumber reg)
    {
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_SIZE(size)));
        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idIsLclVar(), Is.True);
        Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(home));
        Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
    }

    private static void AssertConsumed(GenTree source)
    {
#if DEBUG
        Assert.That((source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.True);
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
