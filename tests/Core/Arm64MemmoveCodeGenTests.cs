// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class Arm64MemmoveCodeGenTests
{
    [TestCase(1, new[] { INS_ldrb, INS_strb }, new[] { 1, 1 }, new[] { 0, 0 })]
    [TestCase(2, new[] { INS_ldrh, INS_strh }, new[] { 2, 2 }, new[] { 0, 0 })]
    [TestCase(3, new[] { INS_ldrh, INS_ldrh, INS_strh, INS_strh }, new[] { 2, 2, 2, 2 }, new[] { 0, 1, 0, 1 })]
    [TestCase(4, new[] { INS_ldr, INS_str }, new[] { 4, 4 }, new[] { 0, 0 })]
    [TestCase(5, new[] { INS_ldr, INS_ldr, INS_str, INS_str }, new[] { 4, 4, 4, 4 }, new[] { 0, 1, 0, 1 })]
    [TestCase(7, new[] { INS_ldr, INS_ldr, INS_str, INS_str }, new[] { 4, 4, 4, 4 }, new[] { 0, 3, 0, 3 })]
    [TestCase(8, new[] { INS_ldr, INS_str }, new[] { 8, 8 }, new[] { 0, 0 })]
    [TestCase(9, new[] { INS_ldr, INS_ldr, INS_str, INS_str }, new[] { 8, 8, 8, 8 }, new[] { 0, 1, 0, 1 })]
    [TestCase(15, new[] { INS_ldr, INS_ldr, INS_str, INS_str }, new[] { 8, 8, 8, 8 }, new[] { 0, 7, 0, 7 })]
    public static void SmallMemmoveLoadsBeforeStoresUsingOverlappingTails(
        int size,
        instruction[] expectedInstructions,
        int[] expectedWidths,
        int[] expectedOffsets)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateMemmove(size);
            var intRegisters = size is 1 or 2 or 4 or 8
                ? Mask(REG_R16)
                : Mask(REG_R16) | Mask(REG_R17);
            codeGen.InternalRegisters.Add(block, intRegisters);
            codeGen.genCodeForMemmove(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));
            Assert.That(emitted.ConvertAll(static descriptor => (int)descriptor.idOpSize()), Is.EqualTo(expectedWidths));
            Assert.That(emitted.ConvertAll(static descriptor => Emitter.emitGetInsSC(descriptor)),
                Is.EqualTo(EncodedOffsets(expectedWidths, expectedOffsets)));

            var loadCount = expectedInstructions.Length / 2;
            for (var index = 0; index < loadCount; index++)
            {
                Assert.That(emitted[index].idReg2(), Is.EqualTo(REG_R2));
                Assert.That(emitted[loadCount + index].idReg2(), Is.EqualTo(REG_R1));
                Assert.That(emitted[loadCount + index].idReg1(), Is.EqualTo(emitted[index].idReg1()));
            }
        });
    }

    [TestCase(16, new[] { 0 }, 1)]
    [TestCase(17, new[] { 0, 1 }, 2)]
    [TestCase(32, new[] { 0, 16 }, 2)]
    [TestCase(33, new[] { 0, 16, 17 }, 3)]
    [TestCase(48, new[] { 0, 16, 32 }, 3)]
    public static void SimdMemmoveLoadsWholeSourceBeforeStoresAndOverlapsTheTail(
        int size,
        int[] expectedOffsets,
        int registerCount)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateMemmove(size);
            codeGen.InternalRegisters.Add(block, SimdRegisters(registerCount));
            codeGen.genCodeForMemmove(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo(Instructions(INS_ldr, INS_str, registerCount)));
            Assert.That(emitted.ConvertAll(static descriptor => (int)descriptor.idOpSize()),
                Is.EqualTo(Widths(registerCount)));
            var expectedWidths = Widths(registerCount);
            var offsets = Offsets(expectedOffsets);
            Assert.That(emitted.ConvertAll(static descriptor => Emitter.emitGetInsSC(descriptor)),
                Is.EqualTo(EncodedOffsets(expectedWidths, offsets)));

            for (var index = 0; index < registerCount; index++)
            {
                Assert.That(emitted[index].idReg2(), Is.EqualTo(REG_R2));
                Assert.That(emitted[registerCount + index].idReg2(), Is.EqualTo(REG_R1));
                Assert.That(emitted[registerCount + index].idReg1(), Is.EqualTo(emitted[index].idReg1()));
            }
        });
    }

    private static GenTreeBlk CreateMemmove(int size)
    {
        var sourceAddress = Physical(REG_R2);
        var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress)
        {
            IsContained = true,
        };

        return new GenTreeBlk(TYP_STRUCT, Physical(REG_R1), source, new ClassLayout((uint)size))
        {
            _kind = BlkOpKindUnrollMemmove,
        };
    }

    private static GenTreePhysReg Physical(regNumber reg)
    {
        return new GenTreePhysReg(reg, TYP_BYREF) { RegNum = reg };
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private static regMaskTP SimdRegisters(int count)
    {
        return count switch
        {
            1 => Mask(REG_V16),
            2 => Mask(REG_V16) | Mask(REG_V17),
            3 => Mask(REG_V16) | Mask(REG_V17) | Mask(REG_V18),
            _ => throw new ArgumentOutOfRangeException(nameof(count)),
        };
    }

    private static instruction[] Instructions(instruction load, instruction store, int count)
    {
        var instructions = new instruction[2 * count];
        Array.Fill(instructions, load, 0, count);
        Array.Fill(instructions, store, count, count);
        return instructions;
    }

    private static int[] Widths(int count)
    {
        var widths = new int[2 * count];
        Array.Fill(widths, 16);
        return widths;
    }

    private static int[] Offsets(int[] offsets)
    {
        var allOffsets = new int[2 * offsets.Length];
        Array.Copy(offsets, allOffsets, offsets.Length);
        Array.Copy(offsets, 0, allOffsets, offsets.Length, offsets.Length);
        return allOffsets;
    }

    private static nint[] EncodedOffsets(int[] widths, int[] offsets)
    {
        var encodedOffsets = new nint[offsets.Length];
        for (var index = 0; index < offsets.Length; index++)
        {
            var offset = offsets[index];
            var width = widths[index];
            encodedOffsets[index] = (offset > 0) && (offset % width == 0)
                ? offset / width
                : offset;
        }

        return encodedOffsets;
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
