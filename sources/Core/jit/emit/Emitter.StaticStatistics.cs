// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private struct NativeLayout
    {
        private int _offset;
        private int _alignment;

        public (int Offset, int Size) Add(int size, int alignment)
        {
            _offset = (_offset + alignment - 1) & -alignment;
            _alignment = Math.Max(_alignment, alignment);
            var offset = _offset;
            _offset += size;
            return (offset, size);
        }

        public readonly int Size => (_offset + _alignment - 1) & -_alignment;
    }

    private static void PrintField(string name, ref NativeLayout layout, int size, int alignment)
    {
        var (offset, width) = layout.Add(size, alignment);
        jitprintf(FormattableString.Invariant($"Offset / size of {name}= {offset,3} / {width,2}\n"));
    }

    internal static void emitStaticStats()
    {
        var (word, regMaskSize) = NativeStaticLayout();
#if TARGET_ARM64
        const int cgcaSize = 80;
#elif TARGET_RISCV64
        var cgcaSize = instrDescCGCA.NativeSize;
#else
        const int cgcaSize = 72;
#endif

        var igBuffSize = (SC_IG_BUFFER_NUM_SMALL_DESCS * SMALL_IDSC_SIZE)
            + (SC_IG_BUFFER_NUM_LARGE_DESCS * INSTR_DESC_SIZE);

        var group = new NativeLayout();
        jitprintf("\ninsGroup:\n");
        PrintField("igNext             ", ref group, word, word);
#if EMIT_BACKWARDS_NAVIGATION
        PrintField("igPrev             ", ref group, word, word);
#endif
#if DEBUG
        PrintField("igSelf             ", ref group, word, word);
#endif
#if DEBUG || LATE_DISASM
        PrintField("igWeight           ", ref group, sizeof(double), sizeof(double));
        PrintField("igPerfScore        ", ref group, sizeof(double), sizeof(double));
#endif
#if DEBUG
        PrintField("lastGeneratedBlock ", ref group, word, word);
        // The pinned jitstd::list<BasicBlock*> occupies five pointer-sized words.
        PrintField("igBlocks           ", ref group, 5 * word, word);
        PrintField("igDataSize         ", ref group, word, word);
#endif
        PrintField("igNum              ", ref group, sizeof(uint), sizeof(uint));
        PrintField("igOffs             ", ref group, sizeof(uint), sizeof(uint));
        PrintField("igFuncIdx          ", ref group, sizeof(uint), sizeof(uint));
        PrintField("igFlags            ", ref group, sizeof(ushort), sizeof(ushort));
        PrintField("igSize             ", ref group, sizeof(ushort), sizeof(ushort));
#if FEATURE_LOOP_ALIGN
        PrintField("igLoopBackEdge     ", ref group, word, word);
#endif
#if REGMASK_BITS_64
        PrintField("igGCregs           ", ref group, sizeof(ulong), sizeof(ulong));
#endif
        var (groupDataOffset, groupDataSize) = group.Add(word, word);
        jitprintf(FormattableString.Invariant($"Offset / size of igData             = {groupDataOffset,3} / {groupDataSize,2}\n"));
        jitprintf(FormattableString.Invariant($"Offset / size of igPhData           = {groupDataOffset,3} / {groupDataSize,2}\n"));
#if EMIT_BACKWARDS_NAVIGATION
        PrintField("igLastIns          ", ref group, word, word);
#endif
#if EMIT_TRACK_STACK_DEPTH
        PrintField("igStkLvl           ", ref group, sizeof(uint), sizeof(uint));
#endif
#if !REGMASK_BITS_64
        PrintField("igGCregs           ", ref group, sizeof(uint), sizeof(uint));
#endif
        PrintField("igInsCnt           ", ref group, sizeof(byte), sizeof(byte));
        jitprintf(FormattableString.Invariant($"\nSize of insGroup                    = {group.Size}\n"));

        var placeholder = new NativeLayout();
        jitprintf("\ninsPlaceholderGroupData:\n");
        PrintField("igPhNext           ", ref placeholder, word, word);
        PrintField("igPhBB             ", ref placeholder, word, word);
        PrintField("igPhInitGCrefVars  ", ref placeholder, word, word);
        PrintField("igPhInitGCrefRegs  ", ref placeholder, regMaskSize, word);
        PrintField("igPhInitByrefRegs  ", ref placeholder, regMaskSize, word);
        PrintField("igPhPrevGCrefVars  ", ref placeholder, word, word);
        PrintField("igPhPrevGCrefRegs  ", ref placeholder, regMaskSize, word);
        PrintField("igPhPrevByrefRegs  ", ref placeholder, regMaskSize, word);
        PrintField("igPhType           ", ref placeholder, sizeof(byte), sizeof(byte));
        jitprintf(FormattableString.Invariant($"\nSize of insPlaceholderGroupData     = {placeholder.Size}\n\n"));

        jitprintf(FormattableString.Invariant($"SMALL_IDSC_SIZE                = {SMALL_IDSC_SIZE,2}\n"));
        PrintDescriptor("instrDesc             ", INSTR_DESC_SIZE);
        PrintDescriptor("instrDescCns          ", ConstantDescriptorSizes.Constant);
        PrintDescriptor("instrDescDsp          ", ConstantDescriptorSizes.Displacement);
#if TARGET_ARM64
        PrintDescriptor("instrDescLclVarPair   ", 24);
        PrintDescriptor("instrDescLclVarPairCns", 32);
#endif
#if TARGET_ARM
        PrintDescriptor("instrDescReloc        ", 24);
#endif
#if TARGET_XARCH
        PrintDescriptor("instrDescAmd          ", ConstantDescriptorSizes.AddressMode);
        PrintDescriptor("instrDescCnsAmd       ", ConstantDescriptorSizes.ConstantAddressMode);
#endif
        PrintDescriptor("instrDescCnsDsp       ", ConstantDescriptorSizes.ConstantDisplacement);
#if FEATURE_LOOP_ALIGN
        PrintDescriptor("instrDescAlign        ", DescriptorSizes.Align);
#endif
        PrintDescriptor("instrDescJmp          ", DescriptorSizes.Jump);
#if !TARGET_ARM64
        PrintDescriptor("instrDescLbl          ", DescriptorSizes.Label);
#endif
        PrintDescriptor("instrDescCGCA         ", cgcaSize);

        jitprintf("\n");
        PrintCapacity("igBuffSize                          ", igBuffSize, 1, false);
        PrintCapacity("SMALL_IDSC_SIZE        per IG buffer", igBuffSize, SMALL_IDSC_SIZE);
        PrintCapacity("instrDesc              per IG buffer", igBuffSize, INSTR_DESC_SIZE);
        PrintCapacity("instrDescCns           per IG buffer", igBuffSize, ConstantDescriptorSizes.Constant);
        PrintCapacity("instrDescDsp           per IG buffer", igBuffSize, ConstantDescriptorSizes.Displacement);
#if TARGET_ARM64
        PrintCapacity("instrDescLclVarPair    per IG buffer", igBuffSize, 24);
        PrintCapacity("instrDescLclVarPairCns per IG buffer", igBuffSize, 32);
#endif
#if TARGET_ARM
        PrintCapacity("instrDescReloc         per IG buffer", igBuffSize, 24);
#endif
#if TARGET_XARCH
        PrintCapacity("instrDescAmd           per IG buffer", igBuffSize, ConstantDescriptorSizes.AddressMode);
        PrintCapacity("instrDescCnsAmd        per IG buffer", igBuffSize, ConstantDescriptorSizes.ConstantAddressMode);
#endif
        PrintCapacity("instrDescCnsDsp        per IG buffer", igBuffSize, ConstantDescriptorSizes.ConstantDisplacement);
#if FEATURE_LOOP_ALIGN
        PrintCapacity("instrDescAlign         per IG buffer", igBuffSize, DescriptorSizes.Align);
#endif
        PrintCapacity("instrDescJmp           per IG buffer", igBuffSize, DescriptorSizes.Jump);
#if !TARGET_ARM64
        PrintCapacity("instrDescLbl           per IG buffer", igBuffSize, DescriptorSizes.Label);
#endif
        PrintCapacity("instrDescCGCA          per IG buffer", igBuffSize, cgcaSize);

        var gcPointer = new NativeLayout();
        var (nextOffset, _) = gcPointer.Add(word, word);
        var (offsetOffset, _) = gcPointer.Add(sizeof(uint), sizeof(uint));
        var (unionOffset, _) = gcPointer.Add(regMaskSize, word);
        gcPointer.Add(sizeof(byte), sizeof(byte));
        gcPointer.Add(sizeof(ushort), sizeof(ushort));
        jitprintf("\nGCInfo::regPtrDsc:\n");
        jitprintf(FormattableString.Invariant($"Offset of rpdNext           = {nextOffset,2}\n"));
        jitprintf(FormattableString.Invariant($"Offset of rpdOffs           = {offsetOffset,2}\n"));
        jitprintf(FormattableString.Invariant($"Offset of <union>           = {unionOffset + (2 * sizeof(uint)),2}\n"));
        jitprintf(FormattableString.Invariant($"Size of GCInfo::regPtrDsc   = {gcPointer.Size,2}\n\n"));
    }

    private static (int Word, int RegisterMaskSize) NativeStaticLayout()
    {
#if TARGET_64BIT && REGMASK_BITS_64 && HAS_MORE_THAN_64_REGISTERS && ((TARGET_AMD64 && WINDOWS_AMD64_ABI && !MULTIREG_HAS_SECOND_GC_RET) || (TARGET_ARM64 && TARGET_WINDOWS && MULTIREG_HAS_SECOND_GC_RET))
        // target.h: two 64-bit register-mask words; emit.h:2484-2525 adds a
        // second-GC-return bitfield on ARM64. Windows places the following
        // bool bitfield in another storage unit, making that descriptor 80 bytes.
        return (8, 16);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Native emitter layout reporting requires the target-specific pointer, register-mask and descriptor layouts.");
#endif
    }

    private static void PrintDescriptor(string name, int size)
    {
        jitprintf(FormattableString.Invariant($"Size of {name} = {size,2}\n"));
    }

    private static void PrintCapacity(string name, int capacity, int size, bool divide = true)
    {
        var value = divide ? capacity / size : capacity;
        jitprintf(FormattableString.Invariant($"{name} = {value,2}\n"));
    }
#endif
}
