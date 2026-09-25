// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenStackArgumentTests
{
    [TestCase(TYP_BYTE, REG_RAX, false, INS_mov, EA_4BYTE)]
    [TestCase(TYP_INT, REG_RDX, true, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, REG_R8, false, INS_mov, EA_8BYTE)]
    [TestCase(TYP_REF, REG_RAX, true, INS_mov, EA_8BYTE)]
    [TestCase(TYP_BYREF, REG_RDX, false, INS_mov, EA_8BYTE)]
    [TestCase(TYP_FLOAT, REG_XMM0, false, INS_movss, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM1, true, INS_movsd_simd, EA_8BYTE)]
    public static void ScalarStoresUseActualWidthAndTheSelectedArgumentArea(
        var_types type, regNumber reg, bool incoming, instruction ins, emitAttr size)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = Physical(type, reg);
            var argument = Argument(compiler, source, 40, 8, incoming);
            codeGen.GCInfo.gcMarkRegPtrVal(reg, type);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], ins, size, incoming ? 0 : 1, 40, reg);
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GcType(type)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            AssertConsumed(source, true);
        });
    }

    [TestCase(TYP_BYTE, -1, false, EA_4BYTE)]
    [TestCase(TYP_USHORT, 65535, true, EA_4BYTE)]
    [TestCase(TYP_INT, int.MinValue, false, EA_4BYTE)]
    [TestCase(TYP_LONG, int.MinValue, true, EA_8BYTE)]
    [TestCase(TYP_REF, 0, false, EA_8BYTE)]
    public static void ContainedImmediatesDoNotConsumeRegisters(
        var_types type, int value, bool incoming, emitAttr size)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = compiler.gtNewIconNode(type, value);
            source.IsContained = true;
            var argument = Argument(compiler, source, 32, 8, incoming);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_BYREF);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], INS_mov, size, incoming ? 0 : 1, 32);
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)value));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(REG_R10)));
            AssertConsumed(source, false);
        });
    }

    [TestCase(TYP_SIMD8, false, INS_movsd_simd, EA_8BYTE)]
    [TestCase(TYP_SIMD16, true, INS_movups, EA_16BYTE)]
    [TestCase(TYP_SIMD32, false, INS_movups, EA_32BYTE)]
    [TestCase(TYP_SIMD64, true, INS_movups, EA_64BYTE)]
    public static void SimdArgumentsStoreTheirWholeNativeRegisterWidth(
        var_types type, bool incoming, instruction ins, emitAttr size)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = Physical(type, REG_XMM2);
            var argument = Argument(compiler, source, 32, (int)size, incoming);

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], ins, size, incoming ? 0 : 1, 32, REG_XMM2);
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
            Assert.That(StackArgOffset(codeGen), Is.EqualTo(32));
            AssertConsumed(source, true);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FieldListsPreserveFieldWidthsOrderGcTypesAndSimd12Stores(bool incoming)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var small = Physical(TYP_INT, REG_RAX);
            var reference = Physical(TYP_REF, REG_RDX);
            var vector = Physical(TYP_SIMD16, REG_XMM0);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddFieldLIR(compiler, small, 0, TYP_BYTE);
            fields.AddFieldLIR(compiler, reference, 8, TYP_REF);
            fields.AddFieldLIR(compiler, vector, 16, TYP_SIMD12);
            var argument = Argument(compiler, fields, 32, 32, incoming);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_REF);
#if DEBUG
            small.UseNum = 0;
            reference.UseNum = 1;
            vector.UseNum = 2;
#endif

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(4));
            var home = incoming ? 0 : 1;
            AssertStack(descriptors[0], INS_mov, EA_1BYTE, home, 32, REG_RAX);
            AssertStack(descriptors[1], INS_mov, EA_8BYTE, home, 40, REG_RDX);
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            AssertStack(descriptors[2], INS_movsd_simd, EA_8BYTE, home, 48, REG_XMM0);
            AssertStack(descriptors[3], INS_extractps, EA_16BYTE, home, 56, REG_XMM0);
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[3]), Is.EqualTo((nint)2));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            AssertConsumed(small, true);
            AssertConsumed(reference, true);
            AssertConsumed(vector, true);
        });
    }

    [TestCase(1, false, false, new[] { 1 })]
    [TestCase(7, true, false, new[] { 4, 2, 1 })]
    [TestCase(8, false, true, new[] { 8 })]
    [TestCase(15, true, true, new[] { 8, 4, 2, 1 })]
    [TestCase(16, false, false, new[] { 16 })]
    [TestCase(17, true, false, new[] { 16, 1 })]
    [TestCase(31, false, true, new[] { 16, 8, 4, 2, 1 })]
    [TestCase(32, true, true, new[] { 16, 16 })]
    public static void UnrolledStructCopiesUseNativeChunksAndLoadSize(
        int loadSize, bool indirect, bool incoming, int[] widths)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = StructSource(new ClassLayout(loadSize), indirect, REG_R10);
            var argument = Argument(compiler, source, 40, (loadSize + 7) & ~7, incoming);
            argument._kind = GenTreePutArgStk.Kind.Unroll;
            argument.ArgLoadSize = loadSize;
            var temps = default(regMaskTP);
            if (loadSize >= 16)
            {
                temps |= Mask(REG_XMM1);
            }
            if ((loadSize % 16) != 0)
            {
                temps |= Mask(REG_RAX);
            }
            codeGen.InternalRegisters.Add(argument, temps);
            if (indirect)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_BYREF);
            }

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(widths.Length * 2));
            var offset = 0;
            for (var i = 0; i < widths.Length; i++)
            {
                var width = widths[i];
                var ins = width == 16 ? INS_movdqu32 : INS_mov;
                var reg = width == 16 ? REG_XMM1 : REG_RAX;
                var load = descriptors[2 * i];
                Assert.That(load.idIns(), Is.EqualTo(ins));
                Assert.That(load.idOpSize(), Is.EqualTo((emitAttr)width));
                Assert.That(load.idReg1(), Is.EqualTo(reg));
                if (indirect)
                {
                    Assert.That(load.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R10));
                    Assert.That(load.idAddr().iiaAddrMode.amDisp, Is.EqualTo(offset));
                }
                else
                {
                    AssertStack(load, ins, (emitAttr)width, 2, 8 + offset, reg);
                }
                AssertStack(descriptors[(2 * i) + 1], ins, (emitAttr)width,
                    incoming ? 0 : 1, 40 + offset, reg);
                offset += width;
            }
            Assert.That(offset, Is.EqualTo(loadSize));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    [TestCase(false, false, false, false)]
    [TestCase(false, true, false, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, true, true, false)]
    public static void RepCopiesPrepareOnlyMissingAddressesAndUseStackByteSize(
        bool indirect, bool destinationReady, bool sourceReady, bool incoming)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var sourceReg = sourceReady ? REG_RSI : REG_R10;
            var source = StructSource(new ClassLayout(96), indirect, sourceReg);
            var argument = Argument(compiler, source, 40, 96, incoming);
            argument._kind = GenTreePutArgStk.Kind.RepInstr;
            if (destinationReady)
            {
                argument.RegNum = REG_RDI;
            }
            codeGen.InternalRegisters.Add(argument, Mask(REG_RDI) | Mask(REG_RSI) | Mask(REG_RCX));

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            var index = 0;
            if (!destinationReady)
            {
                AssertStack(descriptors[index++], INS_lea, EA_8BYTE, incoming ? 0 : 1, 40, REG_RDI);
            }
            if (!indirect)
            {
                AssertStack(descriptors[index++], INS_lea, EA_8BYTE, 2, 8, REG_RSI);
            }
            else if (!sourceReady)
            {
                var move = descriptors[index++];
                Assert.That(move.idIns(), Is.EqualTo(INS_mov));
                Assert.That(move.idReg1(), Is.EqualTo(REG_RSI));
                Assert.That(move.idReg2(), Is.EqualTo(REG_R10));
                Assert.That(move.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            }
            var count = descriptors[index++];
            Assert.That(count.idIns(), Is.EqualTo(INS_mov));
            Assert.That(count.idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(InstructionConstant(codeGen.Emitter, count), Is.EqualTo((nint)96));
            Assert.That(descriptors[index++].idIns(), Is.EqualTo(INS_r_movsb));
            Assert.That(descriptors, Has.Count.EqualTo(index));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
        });
    }

    [TestCase(3, false, false)]
    [TestCase(3, true, true)]
    [TestCase(4, false, true)]
    [TestCase(4, true, false)]
    public static void PartialRepCopiesRecordEveryGcSlotAndPreserveRunThresholds(
        int leadingPlainSlots, bool indirect, bool incoming)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var size = (leadingPlainSlots + 4) * 8;
            var builder = new ClassLayoutBuilder(compiler, size);
            builder.SetGCPtrType(leadingPlainSlots, TYP_REF);
            builder.SetGCPtrType(leadingPlainSlots + 3, TYP_BYREF);
            var source = StructSource(ClassLayout.Create(compiler, builder), indirect, REG_R10);
            var argument = Argument(compiler, source, 40, size, incoming);
            argument._kind = GenTreePutArgStk.Kind.PartialRepInstr;
            codeGen.InternalRegisters.Add(argument, Mask(REG_RDI) | Mask(REG_RSI) | Mask(REG_RCX));

            codeGen.genPutArgStk(argument);

            var descriptors = Descriptors(codeGen);
            var expected = new List<instruction> { INS_lea, indirect ? INS_mov : INS_lea };
            if (leadingPlainSlots < 4)
            {
                expected.AddRange(Enumerable.Repeat(INS_movsq, leadingPlainSlots));
            }
            else
            {
                expected.Add(INS_mov);
                expected.Add(INS_r_movsq);
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[2]), Is.EqualTo((nint)4));
                Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_4BYTE));
            }
            expected.AddRange([INS_mov, INS_mov, INS_add, INS_add, INS_movsq, INS_movsq, INS_mov, INS_mov]);
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(expected));

            var gcLoad = leadingPlainSlots < 4 ? 2 + leadingPlainSlots : 4;
            Assert.That(descriptors[gcLoad].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            AssertStack(descriptors[gcLoad + 1], INS_mov, EA_8BYTE,
                incoming ? 0 : 1, 40 + (leadingPlainSlots * 8), REG_RCX);
            Assert.That(descriptors[gcLoad + 1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(descriptors[gcLoad + 2].idReg1(), Is.EqualTo(REG_RSI));
            Assert.That(descriptors[gcLoad + 2].idGCref(),
                Is.EqualTo(indirect ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
            Assert.That(descriptors[gcLoad + 3].idReg1(), Is.EqualTo(REG_RDI));
            Assert.That(descriptors[gcLoad + 3].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
            Assert.That(descriptors[^2].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            AssertStack(descriptors[^1], INS_mov, EA_8BYTE,
                incoming ? 0 : 1, 40 + ((leadingPlainSlots + 3) * 8), REG_RCX);
            Assert.That(descriptors[^1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void D005RejectsBeforeStructContextConsumptionAndScratchOwnership(bool directStructEntry)
    {
        WithStackArea((compiler, codeGen) =>
        {
            var source = StructSource(new ClassLayout(17), true, REG_R10);
            var address = source.AsBlk().Addr;
            var argument = Argument(compiler, source, 40, 24, false);
            argument._kind = GenTreePutArgStk.Kind.Unroll;
            argument.ArgLoadSize = 17;
            var temps = Mask(REG_RAX) | Mask(REG_XMM1);
            codeGen.InternalRegisters.Add(argument, temps);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_BYREF);
            StackArgVariable(codeGen) = directStructEntry ? 1 : BAD_VAR_NUM;
            StackArgOffset(codeGen) = directStructEntry ? 40 : 123;
            var oldVariable = StackArgVariable(codeGen);
            var oldOffset = StackArgOffset(codeGen);
            compiler.opts.dspCode = true;

            void Generate()
            {
                if (directStructEntry)
                {
                    codeGen.genPutStructArgStk(argument);
                }
                else
                {
                    codeGen.genPutArgStk(argument);
                }
            }

            var exception = Assert.Throws<FatalJitException>(Generate);

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(oldVariable));
            Assert.That(StackArgOffset(codeGen), Is.EqualTo(oldOffset));
            Assert.That(codeGen.InternalRegisters.GetAll(argument), Is.EqualTo(temps));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(REG_R10)));
            AssertConsumed(address, false);

            compiler.opts.dspCode = false;
            Generate();
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(4));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(directStructEntry ? 1 : BAD_VAR_NUM));
            AssertConsumed(address, true);
        });
    }
#endif

    private static void WithStackArea(Action<Compiler, CodeGen> action)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.lvaTable = [
                new() { Type = TYP_LONG, lvIsParam = true, lvIsRegArg = true, lvOnFrame = true,
                    lvFramePointerBased = true, StackOffset = 16, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(256), lvOnFrame = true,
                    lvFramePointerBased = false, StackOffset = 0, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(256), lvOnFrame = true,
                    lvFramePointerBased = true, StackOffset = -256, RegNum = REG_STK },
            ];
            compiler.lvaCount = 3;
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaParameterStackSize = 256;
            AllFloat(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            EnableAvx2(compiler);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            action(compiler, codeGen);
        });
    }

    private static GenTreePutArgStk Argument(Compiler compiler, GenTree source, int offset, int size, bool incoming)
    {
        var call = new GenTreeCall(TYP_VOID);
        if (incoming)
        {
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
        }
        var node = new GenTreePutArgStk(TYP_VOID, source, call, offset, size, incoming);
        if (!varTypeIsStruct(source.Type))
        {
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(source));
            argument.EarlyNode = null;
            argument.LateNode = node;
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.OnStack(offset, 0, size));
            call.Args.PushLateBack(argument);
        }

        return node;
    }

    private static GenTree StructSource(ClassLayout layout, bool indirect, regNumber addressReg)
    {
        if (indirect)
        {
            return new GenTreeBlk(TYP_STRUCT, Physical(TYP_BYREF, addressReg), layout) { IsContained = true };
        }

        return new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 2, 8) { Layout = layout, IsContained = true };
    }

    private static GenTreePhysReg Physical(var_types type, regNumber reg) => new(reg, type) { RegNum = reg };

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static GCInfo.GCtype GcType(var_types type) => type switch
    {
        TYP_REF => GCInfo.GCtype.GCT_GCREF,
        TYP_BYREF => GCInfo.GCtype.GCT_BYREF,
        _ => GCInfo.GCtype.GCT_NONE,
    };

    private static void AssertStack(Emitter.instrDesc id, instruction ins, emitAttr size,
        int home, int offset, regNumber reg = REG_NA)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(home));
        Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
        if (reg != REG_NA)
        {
            Assert.That(id.idReg1(), Is.EqualTo(reg));
        }
    }

    private static void AssertConsumed(GenTree node, bool consumed)
    {
#if DEBUG
        Assert.That((node._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.EqualTo(consumed));
#else
        Assert.That(node.RegNum == REG_NA || node.IsUsedFromReg, Is.True);
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloat(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgVarNum")]
    private static extern ref int StackArgVariable(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgOffset")]
    private static extern ref int StackArgOffset(CodeGen codeGen);
}
