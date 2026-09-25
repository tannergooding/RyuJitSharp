// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        // emit.h:932-1007: AMD64 uses 20 extra flag bits, two relocation bits,
        // and five backwards-navigation bits, leaving five signed constant bits.
        private const int ID_BIT_SMALL_CNS = 5;
        private const int ID_MIN_SMALL_CNS = -(1 << (ID_BIT_SMALL_CNS - 1));
        private const int ID_MAX_SMALL_CNS = (1 << (ID_BIT_SMALL_CNS - 1)) - 1;

        private int _idSmallCns;

        public static bool fitsInSmallCns(nint value)
        {
#if TARGET_AMD64
            return (value >= ID_MIN_SMALL_CNS) && (value <= ID_MAX_SMALL_CNS);
#else
            throw new PlatformNotSupportedException("Small instruction constants are not yet ported for this target.");
#endif
        }

        public int idSmallCns()
        {
#if TARGET_AMD64
            return _idSmallCns;
#else
            throw new PlatformNotSupportedException("Small instruction constants are not yet ported for this target.");
#endif
        }

        public void idSmallCns(nint value)
        {
#if TARGET_AMD64
            assert(fitsInSmallCns(value));
            _idSmallCns = unchecked((int)value << (32 - ID_BIT_SMALL_CNS)) >> (32 - ID_BIT_SMALL_CNS);
            assert(value == idSmallCns());
#else
            throw new PlatformNotSupportedException("Small instruction constants are not yet ported for this target.");
#endif
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

#if TARGET_AMD64
        public override int NativeLogicalSize => ConstantDescriptorSizes.Constant;
#else
        public override int NativeLogicalSize => throw new PlatformNotSupportedException("Constant descriptor size is not yet ported for this target.");
#endif
    }

    protected sealed class instrDescDsp : instrDesc
    {
        public nint iddDspVal;

#if TARGET_AMD64
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

#if TARGET_AMD64
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
