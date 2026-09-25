// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterConstantDescriptorTests
{
    [TestCase(long.MinValue, false)]
    [TestCase(int.MinValue, false)]
    [TestCase(-17L, false)]
    [TestCase(-16L, true)]
    [TestCase(-1L, true)]
    [TestCase(0L, true)]
    [TestCase(15L, true)]
    [TestCase(16L, false)]
    [TestCase(int.MaxValue, false)]
    [TestCase(long.MaxValue, false)]
    public static void ConstantsUseTheSignedFiveBitRange(long value, bool small)
    {
        var emitter = CreateEmitter();
        var constant = (nint)value;
        Assert.That(Emitter.instrDesc.fitsInSmallCns(constant), Is.EqualTo(small));

        foreach (var compact in new[] { false, true })
        {
            var descriptor = compact
                ? NewSmallConstant(emitter, EA_8BYTE, constant)
                : NewConstant(emitter, EA_8BYTE, constant);
            descriptor.idIns(INS_mov);
            var expectedSize = small ? (compact ? 8 : 16) : 24;

            Assert.That(descriptor.idIsSmallDsc(), Is.EqualTo(small && compact));
            Assert.That(descriptor.idIsLargeCns(), Is.EqualTo(!small));
            Assert.That(descriptor.idIsLargeDsp(), Is.False);
            Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(expectedSize));
            Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)(expectedSize + Prefix(emitter))));
            Assert.That(GetConstant(emitter, descriptor), Is.EqualTo(constant));
            Assert.That(ReadConstant(emitter, descriptor, "emitGetInsCns").Constant, Is.EqualTo(constant));

            if (small)
            {
                Assert.That(descriptor.idSmallCns(), Is.EqualTo((int)value));
            }
        }
    }

    [Test]
    public static void SmallConstantsPreserveAllSignedValuesAndIndependentFlags()
    {
        var emitter = CreateEmitter();
        var descriptor = NewConstant(emitter, EA_8BYTE, 0);
        descriptor.idIns(INS_add);
        descriptor.idReg1(REG_R31);
        descriptor.idSetIsDspReloc();
        descriptor.idSetPrevSize(120);

        for (var value = -16; value <= 15; value++)
        {
            descriptor.idSmallCns(value);
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(value));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R31));
            Assert.That(descriptor.idIsDspReloc(), Is.True);
            Assert.That(descriptor.idPrevSize(), Is.EqualTo(120u));
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
        }
    }

#if !DEBUG
    [TestCase(-17L, 15)]
    [TestCase(16L, -16)]
    [TestCase(long.MinValue, 0)]
    [TestCase(long.MaxValue, -1)]
    public static void UncheckedSmallConstantWritesRetainNativeBitfieldTruncation(long value, int expected)
    {
        var emitter = CreateEmitter();
        var descriptor = NewConstant(emitter, EA_8BYTE, 0);
        descriptor.idSmallCns((nint)value);

        Assert.That(descriptor.idSmallCns(), Is.EqualTo(expected));
    }
#endif

    [TestCase(long.MinValue, true)]
    [TestCase(-8193L, true)]
    [TestCase(-8192L, true)]
    [TestCase(-8191L, false)]
    [TestCase(-1L, false)]
    [TestCase(0L, false)]
    [TestCase(8191L, false)]
    [TestCase(8192L, true)]
    [TestCase(long.MaxValue, true)]
    public static void AddressDisplacementsReserveTheMostNegativeBitfieldValue(long displacement, bool large)
    {
        var emitter = CreateEmitter();
        var descriptor = NewAddress(emitter, EA_8BYTE, (nint)displacement);
        descriptor.idIns(INS_mov);

        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(large));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(large ? 24 : 16));
        Assert.That(GetAddress(emitter, descriptor), Is.EqualTo((nint)displacement));
        Assert.That(GetAnyAddress(emitter, descriptor), Is.EqualTo((nint)displacement));
        AssertStoredDisplacement(descriptor, (nint)displacement, large);
    }

    [Test]
    public static void CombinedAddressConstantsSelectAllFourNativeLayouts(
        [Values(int.MinValue, -17, -16, 0, 15, 16, int.MaxValue)] int constant,
        [Values(long.MinValue, -8192L, -8191L, 0L, 8191L, 8192L, long.MaxValue)] long displacement,
        [Values(false, true)] bool relocatable)
    {
        var emitter = CreateEmitter(relocatable: relocatable);
        var descriptor = NewAddressConstant(emitter, EA_8BYTE | EA_CNS_RELOC_FLG | EA_DSP_RELOC_FLG,
            (nint)displacement, constant);
        descriptor.idIns(INS_add);
        var largeConstant = constant is < -16 or > 15;
        var largeDisplacement = displacement is < -8191 or > 8191;
        var expectedSize = 16 + (largeConstant ? 8 : 0) + (largeDisplacement ? 8 : 0);
        var (readDisplacement, readConstant, readRelocatable) = ReadConstant(emitter, descriptor, "emitGetInsAmdCns");

        Assert.That(descriptor.idIsSmallDsc(), Is.False);
        Assert.That(descriptor.idIsLargeCns(), Is.EqualTo(largeConstant));
        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(largeDisplacement));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(expectedSize));
        Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)(expectedSize + Prefix(emitter))));
        Assert.That(readConstant, Is.EqualTo((nint)constant));
        Assert.That(readDisplacement, Is.EqualTo((nint)displacement));
        Assert.That(readRelocatable, Is.EqualTo(relocatable));
        Assert.That(descriptor.idIsDspReloc(), Is.True);
        Assert.That(GetAnyAddress(emitter, descriptor), Is.EqualTo((nint)displacement));
        Assert.That(GetConstant(emitter, descriptor), Is.EqualTo((nint)constant));
        AssertStoredDisplacement(descriptor, (nint)displacement, largeDisplacement);
    }

    [Test]
    public static void OrdinaryDisplacementConstantsUseZeroAsTheirOnlySmallDisplacement(
        [Values(long.MinValue, -17L, -16L, 15L, 16L, long.MaxValue)] long constant,
        [Values(int.MinValue, -1, 0, 1, int.MaxValue)] int displacement)
    {
        var emitter = CreateEmitter(relocatable: true);
        var descriptor = NewDisplacementConstant(emitter, EA_8BYTE | EA_CNS_RELOC_FLG,
            (nint)constant, displacement);
        descriptor.idIns(INS_add);
        var largeConstant = constant is < -16 or > 15;
        var largeDisplacement = displacement != 0;
        var expectedSize = 16 + (largeConstant ? 8 : 0) + (largeDisplacement ? 8 : 0);
        var (_, readConstant, readRelocatable) = ReadConstant(emitter, descriptor, "emitGetInsDcmCns");

        Assert.That(descriptor.idIsLargeCns(), Is.EqualTo(largeConstant));
        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(largeDisplacement));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(expectedSize));
        Assert.That(GetDisplacement(emitter, descriptor), Is.EqualTo((nint)displacement));
        Assert.That(GetConstant(emitter, descriptor), Is.EqualTo((nint)constant));
        Assert.That(readConstant, Is.EqualTo((nint)constant));
        Assert.That(readRelocatable, Is.True);
    }

    [TestCase(long.MinValue)]
    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(long.MaxValue)]
    public static void OrdinaryDisplacementsRetainNativePointerWidth(long displacement)
    {
        var emitter = CreateEmitter();
        var descriptor = NewDisplacement(emitter, EA_8BYTE, (nint)displacement);
        descriptor.idIns(INS_mov);

        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(displacement != 0));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(displacement == 0 ? 16 : 24));
        Assert.That(GetDisplacement(emitter, descriptor), Is.EqualTo((nint)displacement));
    }

    [Test]
    public static void AddressSetterChangesTagsWithoutCorruptingRegistersOrRetainedAliases()
    {
        var emitter = CreateEmitter();
        var descriptor = NewAddress(emitter, EA_8BYTE, nint.MaxValue);
        descriptor.idIns(INS_mov);
        ref var address = ref descriptor.idAddr().iiaAddrMode;
        address.amBaseReg = REG_R31;
        address.amIndxReg = REG_R30;
        address.amScale = 3;

        foreach (var displacement in new nint[] { -8191, 8191, -8192, nint.MinValue, 0 })
        {
#if !DEBUG
            var previousDisplacement = address.amDisp;
#endif
            SetAddress(emitter, descriptor, displacement);
            var large = displacement is < -8191 or > 8191;

            Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(large));
            Assert.That(GetAddress(emitter, descriptor), Is.EqualTo(displacement));
            Assert.That(GetAnyAddress(emitter, descriptor), Is.EqualTo(displacement));
            Assert.That(address.amBaseReg, Is.EqualTo(REG_R31));
            Assert.That(address.amIndxReg, Is.EqualTo(REG_R30));
            Assert.That(address.amScale, Is.EqualTo(3u));

            if (!large)
            {
                Assert.That(address.amDisp, Is.EqualTo((int)displacement));
            }
#if DEBUG
            else
            {
                Assert.That(address.amDisp, Is.EqualTo(-8192));
            }
#else
            else
            {
                Assert.That(address.amDisp, Is.EqualTo(previousDisplacement));
            }
#endif
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CombinedConstantAndAddressAliasesSurviveGroupSaving(bool addressMode)
    {
        var emitter = CreateEmitter();
        var descriptor = addressMode
            ? NewAddressConstant(emitter, EA_8BYTE, nint.MaxValue, int.MinValue)
            : NewDisplacementConstant(emitter, EA_8BYTE, nint.MinValue, int.MaxValue);
        descriptor.idIns(INS_add);
        ref var constant = ref DescriptorFactory.ConstantAlias(descriptor, addressMode);
        ref var address = ref descriptor.idAddr();
        address.iiaLclVar.initLclVarAddr(32768, 255);
        var group = Save(emitter, false);

        constant = nint.MaxValue;
        address.iiaLclVar.initLclVarAddr(-1, 7);
        Assert.That(group.igData, Has.Length.EqualTo(1));
        var saved = group.igData![0];
        Assert.That(saved, Is.SameAs(descriptor));
        Assert.That(GetConstant(emitter, saved), Is.EqualTo(nint.MaxValue));
        Assert.That(ReadConstant(emitter, saved, addressMode ? "emitGetInsAmdCns" : "emitGetInsDcmCns").Constant,
            Is.EqualTo(nint.MaxValue));
        Assert.That(saved.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(-1));
        Assert.That(saved.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(7u));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LogicalPayloadAndDebugPrefixAccountingAreIndependentOfManagedInheritance(bool disassembly)
    {
        var emitter = CreateEmitter(disassembly: disassembly);
        Emitter.instrDesc[] descriptors =
        [
            NewSmallConstant(emitter, EA_8BYTE, 15),
            NewConstant(emitter, EA_8BYTE, 15),
            NewConstant(emitter, EA_8BYTE, 16),
            NewDisplacement(emitter, EA_8BYTE, 1),
            NewDisplacementConstant(emitter, EA_8BYTE, 16, 1),
            NewAddress(emitter, EA_8BYTE, 8192),
            NewAddressConstant(emitter, EA_8BYTE, 8192, 16),
        ];
        int[] sizes = [8, 16, 24, 24, 32, 24, 32];
        var prefix = Prefix(emitter);
#if DEBUG
        Assert.That(prefix, Is.EqualTo(8));
#else
        Assert.That(prefix, Is.EqualTo(disassembly ? 8 : 0));
#endif
        nuint offset = 0;

        for (var index = 0; index < descriptors.Length; index++)
        {
            var descriptor = descriptors[index];
            descriptor.idIns(INS_mov);
            Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(sizes[index]));
            Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)(sizes[index] + prefix)));
            Assert.That(descriptor.StorageOffset, Is.EqualTo(offset + (nuint)prefix));
            Assert.That(descriptor.StorageIndex, Is.EqualTo(index));
            Assert.That(descriptor.idPrevSize(), Is.EqualTo(index == 0 ? 0u : (uint)(sizes[index - 1] + prefix)));

            if (prefix != 0)
            {
                Assert.That(descriptor.idDebugOnlyInfo()?.idSize, Is.EqualTo((nuint)sizes[index]));
                Assert.That(descriptor.idDebugOnlyInfo()?.idNum, Is.EqualTo((uint)(index + 1)));
            }
            else
            {
                Assert.That(descriptor.idDebugOnlyInfo(), Is.Null);
            }

            offset += (nuint)(sizes[index] + prefix);
        }

        Assert.That(Used(emitter), Is.EqualTo(offset));
        var group = Save(emitter, false);
        Assert.That(group.igData, Is.EqualTo(descriptors));
    }

    [TestCase("emitAllocInstrCns", 24)]
    [TestCase("emitAllocInstrDsp", 24)]
    [TestCase("emitAllocInstrCnsDsp", 32)]
    [TestCase("emitAllocInstrAmd", 24)]
    [TestCase("emitAllocInstrCnsAmd", 32)]
    public static void RawAllocationWrappersReserveTheirLayoutWithoutSettingPayloadTags(string methodName, int size)
    {
        var emitter = CreateEmitter();
        var allocator = typeof(Emitter).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(emitAttr)])!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [EA_BYREF])!;

        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(size));
        Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)(size + Prefix(emitter))));
        Assert.That(descriptor.idIsLargeCns(), Is.False);
        Assert.That(descriptor.idIsLargeDsp(), Is.False);
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
        Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
    }

    [Test]
    public static void ConstantAllocationPreservesUnsignedPayloadBitsAndLargeCallTagSemantics()
    {
        var emitter = CreateEmitter();
        var allocator = typeof(Emitter).GetMethod("emitAllocInstrCns", BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(emitAttr), typeof(nuint)])!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [EA_8BYTE, nuint.MaxValue])!;

        Assert.That(descriptor.idIsLargeCns(), Is.True);
        Assert.That(GetConstant(emitter, descriptor), Is.EqualTo((nint)(-1)));
        descriptor.idSetIsCall();
        Assert.That(descriptor.idIsLargeCall(), Is.True);
        Assert.That(descriptor.idIsLargeCns(), Is.False);
    }

    private static void AssertStoredDisplacement(Emitter.instrDesc descriptor, nint displacement, bool large)
    {
        if (large)
        {
#if DEBUG
            Assert.That(descriptor.idAddr().iiaAddrMode.amDisp, Is.EqualTo(-8192));
#else
            Assert.That(descriptor.idAddr().iiaAddrMode.amDisp, Is.Zero);
#endif
        }
        else
        {
            Assert.That(descriptor.idAddr().iiaAddrMode.amDisp, Is.EqualTo((int)displacement));
        }
    }

    private static (nint Displacement, nint Constant, bool Relocatable) ReadConstant(
        Emitter emitter, Emitter.instrDesc descriptor, string methodName)
    {
        var type = typeof(Emitter).GetNestedType("CnsVal", BindingFlags.NonPublic)!;
        var method = typeof(Emitter).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(Emitter.instrDesc), type.MakeByRefType()])!;
        object?[] arguments = [descriptor, Activator.CreateInstance(type)];
        var displacement = method.Invoke(emitter, arguments);
        var constant = (nint)type.GetField("cnsVal")!.GetValue(arguments[1])!;
        var relocatable = (bool)type.GetField("cnsReloc")!.GetValue(arguments[1])!;

        return (displacement is nint value ? value : 0, constant, relocatable);
    }

    private static void SetAddress(Emitter emitter, Emitter.instrDesc descriptor, nint displacement)
    {
        var method = typeof(Emitter).GetMethod("emitSetAmdDisp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _ = method.Invoke(emitter, [descriptor, displacement]);
    }

    private static DescriptorFactory CreateEmitter(bool disassembly = false, bool relocatable = false)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.opts.disAsm = disassembly;
        compiler.opts.compReloc = relocatable;
        var emitter = new DescriptorFactory(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );

        return emitter;
    }

    private sealed class DescriptorFactory : Emitter
    {
        internal DescriptorFactory(CodeGen codeGen) : base(codeGen)
        {
        }

        internal static ref nint ConstantAlias(instrDesc descriptor, bool addressMode)
        {
            if (addressMode)
            {
                return ref ((instrDescCnsAmd)descriptor).idacCnsVal;
            }

            return ref ((instrDescCnsDsp)descriptor).iddcCnsVal;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCns")]
    private static extern Emitter.instrDesc NewConstant(Emitter emitter, emitAttr attr, nint constant);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSC")]
    private static extern Emitter.instrDesc NewSmallConstant(Emitter emitter, emitAttr attr, nint constant);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrDsp")]
    private static extern Emitter.instrDesc NewDisplacement(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCnsDsp")]
    private static extern Emitter.instrDesc NewDisplacementConstant(Emitter emitter, emitAttr attr, nint constant, int displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmdCns")]
    private static extern Emitter.instrDesc NewAddressConstant(Emitter emitter, emitAttr attr, nint displacement, int constant);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint GetConstant(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsDsp")]
    private static extern nint GetDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmd")]
    private static extern nint GetAddress(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint GetAnyAddress(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int Prefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
