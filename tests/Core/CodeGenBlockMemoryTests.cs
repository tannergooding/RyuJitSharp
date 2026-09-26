// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenBlockMemoryTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(15)]
    public static void ScalarMemmoveLoadsAllSourceBytesBeforeAnyStore(int size)
    {
        WithMemory(16, false, (compiler, codeGen) =>
        {
            var node = Copy(size, BlkOpKindUnrollMemmove, Physical(REG_RDX), Physical(REG_RCX));
            codeGen.InternalRegisters.Add(node, Mask(REG_R8) | (BitOperations.IsPow2(size) ? RBM_NONE : Mask(REG_R9)));
            codeGen.genCodeForStoreBlk(node);

            var ids = Descriptors(codeGen);
            var count = BitOperations.IsPow2(size) ? 1 : 2;
            Assert.That(ids, Has.Count.EqualTo(count * 2));
            Assert.That(ids.Take(count).All(id => id.idAddr().iiaAddrMode.amBaseReg == REG_RCX), Is.True);
            Assert.That(ids.Skip(count).All(id => id.idAddr().iiaAddrMode.amBaseReg == REG_RDX), Is.True);
            CheckMemmoveBytes(ids, size);
        });
    }

    [TestCase(16, 16, false)]
    [TestCase(17, 16, false)]
    [TestCase(31, 16, true)]
    [TestCase(33, 32, true)]
    [TestCase(47, 32, true)]
    [TestCase(65, 64, true)]
    [TestCase(97, 64, true)]
    [TestCase(129, 64, true)]
    public static void SimdMemmoveRetainsOverlappingTailWidthsAndLoadStoreSeparation(int size, int vectorSize, bool vex)
    {
        WithMemory(vectorSize, vex, (_, codeGen) =>
        {
            var node = Copy(size, BlkOpKindUnrollMemmove, Physical(REG_RDX), Physical(REG_RCX));
            codeGen.InternalRegisters.Add(node, Mask(REG_XMM0) | Mask(REG_XMM1) | Mask(REG_XMM2) | Mask(REG_XMM3));
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);
            var halves = ids.Count / 2;

            Assert.That(ids.Take(halves).All(id => id.idAddr().iiaAddrMode.amBaseReg == REG_RCX), Is.True);
            Assert.That(ids.Skip(halves).All(id => id.idAddr().iiaAddrMode.amBaseReg == REG_RDX), Is.True);
            Assert.That(ids.All(id => id.idIns() == (vex ? INS_movdqu32 : INS_movups)), Is.True);
            Assert.That(ids[0].idOpSize(), Is.EqualTo((emitAttr)vectorSize));
            CheckMemmoveBytes(ids, size);
        });
    }

    [TestCase(3, new[] { 2, 1 }, new[] { 0, 2 })]
    [TestCase(7, new[] { 4, 4 }, new[] { 0, 3 })]
    [TestCase(15, new[] { 8, 8 }, new[] { 0, 7 })]
    [TestCase(17, new[] { 16, 1 }, new[] { 0, 16 })]
    [TestCase(19, new[] { 16, 16 }, new[] { 0, 3 })]
    [TestCase(24, new[] { 16, 8 }, new[] { 0, 16 })]
    [TestCase(31, new[] { 16, 16 }, new[] { 0, 15 })]
    public static void UnrolledCopiesUseExactScalarFallbackAndSimdTailDecisions(int size, int[] widths, int[] offsets)
    {
        WithMemory(16, true, (_, codeGen) =>
        {
            var node = Copy(size, BlkOpKindUnroll, Physical(REG_RDX), Physical(REG_RCX));
            codeGen.InternalRegisters.Add(node, Mask(REG_XMM0) | Mask(REG_R8));
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);

            Assert.That(ids, Has.Count.EqualTo(widths.Length * 2));
            for (var index = 0; index < widths.Length; index++)
            {
                var load = ids[index * 2];
                var store = ids[(index * 2) + 1];
                Assert.That(load.idOpSize(), Is.EqualTo((emitAttr)widths[index]));
                Assert.That(store.idOpSize(), Is.EqualTo((emitAttr)widths[index]));
                Assert.That(load.idAddr().iiaAddrMode.amDisp, Is.EqualTo(offsets[index]));
                Assert.That(store.idAddr().iiaAddrMode.amDisp, Is.EqualTo(offsets[index]));
                Assert.That(load.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
                Assert.That(store.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDX));
            }
        });
    }

    [TestCase(32, 40, 8, 32)]
    [TestCase(32, 47, 16, 31)]
    [TestCase(64, 65, 1, 64)]
    [TestCase(64, 95, 32, 63)]
    public static void WideCopiesShrinkOnlyTheRemainder(int vectorSize, int size, int tailWidth, int tailOffset)
    {
        WithMemory(vectorSize, true, (_, codeGen) =>
        {
            var node = Copy(size, BlkOpKindUnroll, Physical(REG_RDX), Physical(REG_RCX));
            codeGen.InternalRegisters.Add(node, Mask(REG_XMM0) | Mask(REG_R8));
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);

            Assert.That(ids.Select(id => (int)id.idOpSize()),
                Is.EqualTo([vectorSize, vectorSize, tailWidth, tailWidth]));
            Assert.That(ids.Select(id => (int)id.idAddr().iiaAddrMode.amDisp),
                Is.EqualTo([0, 0, tailOffset, tailOffset]));
        });
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 4)]
    public static void CopyAddressFormsPreserveLocalOffsetsAndScale(int sourceKind, int destKind)
    {
        WithMemory(16, true, (_, codeGen) =>
        {
            var destination = Address(destKind, REG_RDX, REG_R10, 0);
            var source = sourceKind == 4
                ? (GenTree)new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 1, 12)
                    { Layout = new ClassLayout(8), IsContained = true }
                : new GenTreeIndir(GT_IND, TYP_STRUCT, Address(sourceKind, REG_RCX, REG_R11, 1))
                    { IsContained = true };
            var node = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout(8))
                { _kind = BlkOpKindUnroll };
            codeGen.InternalRegisters.Add(node, Mask(REG_R8));

            codeGen.genCodeForStoreBlk(node);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(2));
            AssertAddress(ids[0], sourceKind, 1, REG_RCX, REG_R11);
            AssertAddress(ids[1], destKind, 0, REG_RDX, REG_R10);
        });
    }

    [TestCase(3, new[] { 2, 1 }, new[] { 0, 2 })]
    [TestCase(7, new[] { 4, 4 }, new[] { 0, 3 })]
    [TestCase(15, new[] { 8, 8 }, new[] { 0, 7 })]
    [TestCase(17, new[] { 16, 16 }, new[] { 0, 1 })]
    [TestCase(24, new[] { 16, 16 }, new[] { 0, 8 })]
    public static void InitializationRetainsOverlappingStores(int size, int[] widths, int[] offsets)
    {
        WithMemory(16, true, (_, codeGen) =>
        {
            var node = Init(size, Physical(REG_RDX), simd: size >= 16, fill: 0);
            if (size >= 16)
            {
                codeGen.InternalRegisters.Add(node, Mask(REG_XMM0));
            }
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);
            var stores = ids.Where(id => id.idIns() is INS_mov or INS_movdqu32).ToArray();

            Assert.That(stores.Select(id => (int)id.idOpSize()), Is.EqualTo(widths));
            Assert.That(stores.Select(id => (int)id.idAddr().iiaAddrMode.amDisp), Is.EqualTo(offsets));
            if (size >= 16)
            {
                Assert.That(ids[0].idIns(), Is.EqualTo(INS_xorps));
            }
        });
    }

    [TestCase(0, INS_xorps)]
    [TestCase(255, INS_pcmpeqd)]
    public static void RepeatedByteInitConstantsUseCommittedVectorMaterialization(int fill, instruction materialize)
    {
        WithMemory(32, true, (_, codeGen) =>
        {
            var node = Init(48, Address(3, REG_RDX, REG_R10, 0), simd: true, fill);
            codeGen.InternalRegisters.Add(node, Mask(REG_XMM0));
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);

            Assert.That(ids[0].idIns(), Is.EqualTo(materialize));
            Assert.That(ids.Skip(1).Select(id => id.idOpSize()), Is.EqualTo([EA_32BYTE, EA_16BYTE]));
            Assert.That(ids.Skip(1).Select(id => id.idAddr().iiaLclVar.lvaOffset()), Is.EqualTo(new uint[] { 12, 44 }));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HeapGcSlotsStayAtomicAndOnlyPlainRunsUseSimd(bool useSimd)
    {
        WithMemory(16, true, (compiler, codeGen) =>
        {
            var builder = new ClassLayoutBuilder(compiler, 64);
            builder.SetGCPtrType(0, TYP_REF);
            builder.SetGCPtrType(3, TYP_BYREF);
            builder.SetGCPtrType(7, TYP_REF);
            var node = Init(64, Physical(REG_RDX), simd: false, fill: 0);
            node.Layout = ClassLayout.Create(compiler, builder);
            if (useSimd)
            {
                codeGen.InternalRegisters.Add(node, Mask(REG_XMM0));
            }
            codeGen.genCodeForStoreBlk(node);
            var ids = Descriptors(codeGen);
            var stores = ids.Where(id => id.idIns() is INS_mov or INS_movdqu32).ToArray();

            ReadOnlySpan<int> gcOffsets = [0, 24, 56];
            foreach (var gcOffset in gcOffsets)
            {
                var store = stores.Single(id => id.idAddr().iiaAddrMode.amDisp == gcOffset);
                Assert.That(store.idOpSize(), Is.EqualTo(EA_8BYTE));
            }
            Assert.That(ids.Count(id => id.idIns() == INS_xorps), Is.EqualTo(useSimd ? 1 : 0));
            Assert.That(stores.Count(id => id.idOpSize() == EA_16BYTE), Is.EqualTo(useSimd ? 2 : 0));
            foreach (var store in stores.Where(id => id.idOpSize() != EA_8BYTE))
            {
                Assert.That(store.idAddr().iiaAddrMode.amDisp, Is.AnyOf(8, 32));
            }
        });
    }

    [TestCase(8)]
    [TestCase(24)]
    public static void InitLoopTouchesBaseBeforeReverseLoopAndKeepsOnlyLoopAddressLive(int size)
    {
        WithMemory(16, true, (_, codeGen) =>
        {
            var node = Init(size, Physical(REG_RDX), simd: false, fill: 0);
            node._kind = BlkOpKindLoop;
            if (size > 8)
            {
                codeGen.InternalRegisters.Add(node, Mask(REG_R8));
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genCodeForStoreBlk(node);
            var ids = CodeGenLocalHeapTests.AllDescriptors(first, codeGen);

            Assert.That(ids[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids[0].idAddr().iiaAddrMode.amDisp, Is.Zero);
            Assert.That(ids[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDX));
            if (size > 8)
            {
                Assert.That(ids.Select(id => id.idIns()), Is.EqualTo([INS_mov, INS_mov, INS_mov, INS_sub, INS_jne]));
                Assert.That(InstructionConstant(codeGen.Emitter, ids[1]), Is.EqualTo((nint)(size - 8)));
                Assert.That(ids[2].idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_R8));
                Assert.That(InstructionConstant(codeGen.Emitter, ids[3]), Is.EqualTo((nint)8));
                Assert.That(codeGen.GCInfo.gcRegByrefSetCur & Mask(REG_RDX), Is.EqualTo(RBM_NONE));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RBX)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GcUnsafeCopyUsesNoGcGroupsWithoutAddingVolatileFences(bool memmove)
    {
        WithMemory(16, true, (_, codeGen) =>
        {
            var node = Copy(8, memmove ? BlkOpKindUnrollMemmove : BlkOpKindUnroll,
                Physical(REG_RDX), Physical(REG_RCX));
            node._gcUnsafe = true;
            node.Flags |= GTF_IND_VOLATILE;
            codeGen.InternalRegisters.Add(node, Mask(REG_R8));
            codeGen.genCodeForStoreBlk(node);

            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
        });
    }

#if DEBUG
    [TestCase(BlkOpKindUnroll)]
    [TestCase(BlkOpKindUnrollMemmove)]
    public static void DspCodeRecordsBlockStoresAndRestoresGcInterruptibility(BlkOpKind kind)
    {
        WithMemory(16, true, (compiler, codeGen) =>
        {
            var node = Copy(8, kind, Physical(REG_RDX), Physical(REG_RCX));
            node._gcUnsafe = true;
            codeGen.InternalRegisters.Add(node, Mask(REG_R8));
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForStoreBlk(node));

            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.InternalRegisters.Count(node), Is.Zero);
            Assert.That(node.Addr._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
        });
    }
#endif

    private static void CheckMemmoveBytes(List<Emitter.instrDesc> ids, int size)
    {
        foreach (var delta in new[] { -7, -1, 0, 1, 7 })
        {
            var memory = Enumerable.Range(0, 512).Select(index => (byte)(index % 251)).ToArray();
            var expected = (byte[])memory.Clone();
            Array.Copy(expected, 128, expected, 128 + delta, size);
            var registers = new Dictionary<regNumber, byte[]>();
            foreach (var id in ids)
            {
                var width = (int)id.idOpSize();
                var offset = (int)id.idAddr().iiaAddrMode.amDisp;
                var reg = id.idReg1();
                if (id.idAddr().iiaAddrMode.amBaseReg == REG_RCX)
                {
                    registers[reg] = memory.AsSpan(128 + offset, width).ToArray();
                }
                else
                {
                    registers[reg].AsSpan(0, width).CopyTo(memory.AsSpan(128 + delta + offset, width));
                }
            }
            Assert.That(memory, Is.EqualTo(expected), $"overlap delta {delta}");
        }
    }

    private static GenTreeBlk Copy(int size, BlkOpKind kind, GenTree dst, GenTree src)
        => new(TYP_STRUCT, dst, new GenTreeIndir(GT_IND, TYP_STRUCT, src) { IsContained = true }, new ClassLayout(size))
            { _kind = kind };

    private static GenTreeBlk Init(int size, GenTree dst, bool simd, int fill)
    {
        var value = new GenTreeIntCon(TYP_INT, fill) { IsContained = simd, RegNum = simd ? REG_NA : REG_RAX };
        GenTree data = value;
        if (fill != 0)
        {
            data = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, value) { IsContained = true };
        }

        return new GenTreeBlk(TYP_STRUCT, dst, data, new ClassLayout(size)) { _kind = BlkOpKindUnroll };
    }

    private static GenTreePhysReg Physical(regNumber reg) => new(reg, TYP_BYREF) { RegNum = reg };

    private static GenTree Address(int kind, regNumber baseReg, regNumber index, int local)
    {
        return kind switch
        {
            0 => Physical(baseReg),
            1 => new GenTreeAddrMode(TYP_BYREF, Physical(baseReg), null, 0, 12) { IsContained = true },
            2 => new GenTreeAddrMode(TYP_BYREF, Physical(baseReg), Physical(index), 4, 12) { IsContained = true },
            3 or 4 => new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, local, 12) { IsContained = true },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static void AssertAddress(Emitter.instrDesc id, int kind, int local, regNumber baseReg, regNumber index)
    {
        if (kind is 3 or 4)
        {
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(local));
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
        }
        else
        {
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.EqualTo(kind == 0 ? 0 : 12));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(kind == 2 ? index : REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(kind == 2 ? 2u : 0u));
        }
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void WithMemory(int width, bool vex, Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable =
            [
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(256), lvOnFrame = true,
                    lvFramePointerBased = true, StackOffset = -256, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(256), lvOnFrame = true,
                    lvFramePointerBased = true, StackOffset = -512, RegNum = REG_STK },
            ];
            compiler.lvaCount = 2;
            AllFloat(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            if (vex)
            {
                EnableAvx2(compiler);
            }
            if (width == 64)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            }
            compiler.opts.preferredVectorByteLength = width;
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = width == 64;
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloat(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);
}
