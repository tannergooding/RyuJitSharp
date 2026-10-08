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

    private static void AddField(ref NativeLayout layout, string name, int size, int alignment, bool print)
    {
        var (offset, width) = layout.Add(size, alignment);

        if (print)
        {
            jitprintf(FormattableString.Invariant($"Offset / size of {name}= {offset,3} / {width,2}\n"));
        }
    }

    internal static void emitStaticStats()
    {
        var (word, regMaskSize, regMaskAlignment, doubleAlignment) = NativeStaticLayout();
#if TARGET_ARM64
        const int cgcaSize = 80;
#elif TARGET_RISCV64
        var cgcaSize = instrDescCGCA.NativeSize;
#else
        const int cgcaSize = 72;
#endif

        var igBuffSize = (SC_IG_BUFFER_NUM_SMALL_DESCS * SMALL_IDSC_SIZE)
            + (SC_IG_BUFFER_NUM_LARGE_DESCS * INSTR_DESC_SIZE);

        jitprintf("\ninsGroup:\n");
        var group = BuildNativeIGLayout(word, regMaskAlignment, doubleAlignment, true);
        jitprintf(FormattableString.Invariant($"\nSize of insGroup                    = {group.Size}\n"));

        var placeholder = new NativeLayout();
        jitprintf("\ninsPlaceholderGroupData:\n");
        AddField(ref placeholder, "igPhNext           ", word, word, true);
        AddField(ref placeholder, "igPhBB             ", word, word, true);
        AddField(ref placeholder, "igPhInitGCrefVars  ", word, word, true);
        AddField(ref placeholder, "igPhInitGCrefRegs  ", regMaskSize, regMaskAlignment, true);
        AddField(ref placeholder, "igPhInitByrefRegs  ", regMaskSize, regMaskAlignment, true);
        AddField(ref placeholder, "igPhPrevGCrefVars  ", word, word, true);
        AddField(ref placeholder, "igPhPrevGCrefRegs  ", regMaskSize, regMaskAlignment, true);
        AddField(ref placeholder, "igPhPrevByrefRegs  ", regMaskSize, regMaskAlignment, true);
        AddField(ref placeholder, "igPhType           ", sizeof(byte), sizeof(byte), true);
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
        var (unionOffset, _) = gcPointer.Add(regMaskSize, regMaskAlignment);
        gcPointer.Add(sizeof(byte), sizeof(byte));
        gcPointer.Add(sizeof(ushort), sizeof(ushort));
        jitprintf("\nGCInfo::regPtrDsc:\n");
        jitprintf(FormattableString.Invariant($"Offset of rpdNext           = {nextOffset,2}\n"));
        jitprintf(FormattableString.Invariant($"Offset of rpdOffs           = {offsetOffset,2}\n"));
        jitprintf(FormattableString.Invariant($"Offset of <union>           = {unionOffset + (2 * sizeof(uint)),2}\n"));
        jitprintf(FormattableString.Invariant($"Size of GCInfo::regPtrDsc   = {gcPointer.Size,2}\n\n"));
    }

    internal static nuint emitNativeIGSize()
    {
        var (word, _, regMaskAlignment, doubleAlignment) = NativeStaticLayout();
        return (nuint)BuildNativeIGLayout(word, regMaskAlignment, doubleAlignment, false).Size;
    }

    private static NativeLayout BuildNativeIGLayout(int word, int regMaskAlignment, int doubleAlignment, bool print)
    {
        var group = new NativeLayout();
        AddField(ref group, "igNext             ", word, word, print);
#if EMIT_BACKWARDS_NAVIGATION
        AddField(ref group, "igPrev             ", word, word, print);
#endif
#if DEBUG
        AddField(ref group, "igSelf             ", word, word, print);
#endif
#if DEBUG || LATE_DISASM
        AddField(ref group, "igWeight           ", sizeof(double), doubleAlignment, print);
        AddField(ref group, "igPerfScore        ", sizeof(double), doubleAlignment, print);
#endif
#if DEBUG
        AddField(ref group, "lastGeneratedBlock ", word, word, print);
        // The pinned jitstd::list<BasicBlock*> occupies five pointer-sized words.
        AddField(ref group, "igBlocks           ", 5 * word, word, print);
        AddField(ref group, "igDataSize         ", word, word, print);
#endif
        AddField(ref group, "igNum              ", sizeof(uint), sizeof(uint), print);
        AddField(ref group, "igOffs             ", sizeof(uint), sizeof(uint), print);
        AddField(ref group, "igFuncIdx          ", sizeof(uint), sizeof(uint), print);
        AddField(ref group, "igFlags            ", sizeof(ushort), sizeof(ushort), print);
        AddField(ref group, "igSize             ", sizeof(ushort), sizeof(ushort), print);
#if FEATURE_LOOP_ALIGN
        AddField(ref group, "igLoopBackEdge     ", word, word, print);
#endif
#if REGMASK_BITS_64
        AddField(ref group, "igGCregs           ", sizeof(ulong), regMaskAlignment, print);
#endif
        var (groupDataOffset, groupDataSize) = group.Add(word, word);

        if (print)
        {
            jitprintf(FormattableString.Invariant($"Offset / size of igData             = {groupDataOffset,3} / {groupDataSize,2}\n"));
            jitprintf(FormattableString.Invariant($"Offset / size of igPhData           = {groupDataOffset,3} / {groupDataSize,2}\n"));
        }

#if EMIT_BACKWARDS_NAVIGATION
        AddField(ref group, "igLastIns          ", word, word, print);
#endif
#if EMIT_TRACK_STACK_DEPTH
        AddField(ref group, "igStkLvl           ", sizeof(uint), sizeof(uint), print);
#endif
#if !REGMASK_BITS_64
        AddField(ref group, "igGCregs           ", sizeof(uint), sizeof(uint), print);
#endif
        AddField(ref group, "igInsCnt           ", sizeof(byte), sizeof(byte), print);
        return group;
    }

    private static (int Word, int RegisterMaskSize, int RegisterMaskAlignment, int DoubleAlignment) NativeStaticLayout()
    {
#if TARGET_32BIT && TARGET_ARM && REGMASK_BITS_64
#if TARGET_WINDOWS
        // emit.h packs ARM32 structures to four-byte alignment for MSVC.
        return (4, 8, 4, 4);
#elif TARGET_UNIX
        // ARM Unix uses the ABI's natural eight-byte alignment for double and regMaskSmall.
        return (4, 8, 8, 8);
#endif
#elif TARGET_64BIT && REGMASK_BITS_64 && HAS_MORE_THAN_64_REGISTERS && ((TARGET_AMD64 && WINDOWS_AMD64_ABI && !MULTIREG_HAS_SECOND_GC_RET) || (TARGET_ARM64 && TARGET_WINDOWS && MULTIREG_HAS_SECOND_GC_RET))
        // target.h: two 64-bit register-mask words; emit.h:2484-2525 adds a
        // second-GC-return bitfield on ARM64. Windows places the following
        // bool bitfield in another storage unit, making that descriptor 80 bytes.
        return (8, 16, 8, 8);
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
