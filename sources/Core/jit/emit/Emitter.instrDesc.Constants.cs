// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    // emit.h:932-1007: small constants fill the remainder of the second
    // descriptor word after target flags, relocation and predecessor bits.
#if TARGET_ARM
    private const int ID_EXTRA_BITFIELD_BITS = 16;
#elif TARGET_ARM64
    private const int ID_EXTRA_BITFIELD_BITS = 23;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
    private const int ID_EXTRA_BITFIELD_BITS = 14;
#elif TARGET_X86
    private const int ID_EXTRA_BITFIELD_BITS = 18;
#elif TARGET_AMD64
    private const int ID_EXTRA_BITFIELD_BITS = 20;
#elif TARGET_WASM
    private const int ID_EXTRA_BITFIELD_BITS = -4;
#else
#error Unsupported or unset target architecture
#endif

#if TARGET_XARCH && HOST_64BIT
    private const int ID_EXTRA_PREV_OFFSET_BITS = 5;
#elif TARGET_XARCH
    private const int ID_EXTRA_PREV_OFFSET_BITS = 4;
#else
    private const int ID_EXTRA_PREV_OFFSET_BITS = 0;
#endif

    private const int ID_EXTRA_RELOC_BITS = 2;
    private const int ID_EXTRA_BITS = ID_EXTRA_RELOC_BITS + ID_EXTRA_BITFIELD_BITS + ID_EXTRA_PREV_OFFSET_BITS;
    private const int ID_BIT_SMALL_CNS = ID_EXTRA_BITS <= 0 ? 30 : 32 - ID_EXTRA_BITS;
    private const int ID_MIN_SMALL_CNS = -(1 << (ID_BIT_SMALL_CNS - 1));
    private const int ID_MAX_SMALL_CNS = (1 << (ID_BIT_SMALL_CNS - 1)) - 1;

    public abstract partial class instrDesc
    {
        private int _idSmallCns;

        public static bool fitsInSmallCns(nint value)
        {
            return (value >= ID_MIN_SMALL_CNS) && (value <= ID_MAX_SMALL_CNS);
        }

        public int idSmallCns()
        {
            return _idSmallCns;
        }

        public void idSmallCns(nint value)
        {
            assert(fitsInSmallCns(value));
            _idSmallCns = unchecked((int)value << (32 - ID_BIT_SMALL_CNS)) >> (32 - ID_BIT_SMALL_CNS);
            assert(value == idSmallCns());
        }

        public void idSetIsSmallDsp()
        {
            _idLargeDsp = false;
        }
    }

    // emit.h:2343-2380: the native AMD64 base is 16 bytes. One pointer-sized
    // payload makes 24; two make 32. CnsDsp's trailing int also rounds to 32.
    private static class ConstantDescriptorSizes
    {
        internal const int Constant = 24;
        internal const int Displacement = 24;
        internal const int ConstantDisplacement = 32;
        internal const int AddressMode = 24;
        internal const int ConstantAddressMode = 32;
    }

    protected class instrDescCns : instrDesc
    {
        public nint idcCnsVal;

#if TARGET_AMD64 || TARGET_ARM64
        public override int NativeLogicalSize => ConstantDescriptorSizes.Constant;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Constant descriptor size is not yet ported for this target.");
#endif
    }

    protected sealed class instrDescDsp : instrDesc
    {
        public nint iddDspVal;

#if TARGET_AMD64 || TARGET_ARM64
        public override int NativeLogicalSize => ConstantDescriptorSizes.Displacement;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Displacement descriptor size is not yet ported for this target.");
#endif
    }

    // Native casts to instrDescCns read the common first payload slot. Inheritance
    // and ref aliases preserve that view without depending on CLR object layout.
    protected sealed class instrDescCnsDsp : instrDescCns
    {
        public ref nint iddcCnsVal => ref idcCnsVal;
        public int iddcDspVal;

#if TARGET_AMD64 || TARGET_ARM64
        public override int NativeLogicalSize => ConstantDescriptorSizes.ConstantDisplacement;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Constant/displacement descriptor size is not yet ported for this target.");
#endif
    }

#if TARGET_XARCH
    protected sealed class instrDescAmd : instrDesc
    {
        public nint idaAmdVal;

#if TARGET_AMD64
        public override int NativeLogicalSize => ConstantDescriptorSizes.AddressMode;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Address-mode descriptor size is not yet ported for this target.");
#endif
    }

    protected sealed class instrDescCnsAmd : instrDescCns
    {
        public ref nint idacCnsVal => ref idcCnsVal;
        public nint idacAmdVal;

#if TARGET_AMD64
        public override int NativeLogicalSize => ConstantDescriptorSizes.ConstantAddressMode;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Constant/address-mode descriptor size is not yet ported for this target.");
#endif
    }
#endif
}
