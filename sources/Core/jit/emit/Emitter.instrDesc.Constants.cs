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
    private static int ID_EXTRA_BITFIELD_BITS =>
        Configuration.TARGET_ARM ? 16 :
        Configuration.TARGET_ARM64 ? 23 :
        Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64 ? 14 :
        Configuration.TARGET_X86 ? 18 :
        Configuration.TARGET_AMD64 ? 20 :
        Configuration.TARGET_WASM ? -4 :
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction descriptor constants are not supported for this target.");

    private static int ID_EXTRA_PREV_OFFSET_BITS =>
        Configuration.TARGET_XARCH ? Configuration.HOST_64BIT ? 5 : 4 : 0;

    private const int ID_EXTRA_RELOC_BITS = 2;
    private static int ID_EXTRA_BITS => ID_EXTRA_RELOC_BITS + ID_EXTRA_BITFIELD_BITS + ID_EXTRA_PREV_OFFSET_BITS;
    private static int ID_BIT_SMALL_CNS => ID_EXTRA_BITS <= 0 ? 30 : 32 - ID_EXTRA_BITS;
    private static int ID_MIN_SMALL_CNS => -(1 << (ID_BIT_SMALL_CNS - 1));
    private static int ID_MAX_SMALL_CNS => (1 << (ID_BIT_SMALL_CNS - 1)) - 1;

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

    // emit.h:2343-2380: native x86/ARM/x64 bases are 12/12/16 bytes. Pointer-sized
    // payloads add four/eight bytes; CnsDsp's trailing int rounds x64 to 32.
    private static class ConstantDescriptorSizes
    {
        internal static int Constant =>
            Configuration.TARGET_X86 || Configuration.TARGET_ARM ||
            (Configuration.TARGET_WASM && !Configuration.HOST_64BIT) ? 16 : 24;

        internal static int Displacement =>
            Configuration.TARGET_64BIT ? 24 : 16;

        internal static int ConstantDisplacement =>
            Configuration.TARGET_64BIT ? 32 : 20;

        internal static int AddressMode =>
            Configuration.TARGET_64BIT ? 24 : 16;

        internal static int ConstantAddressMode =>
            Configuration.TARGET_64BIT ? 32 : 20;
    }

    protected class instrDescCns : instrDesc
    {
        public nint idcCnsVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!(Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64 ||
                    Configuration.TARGET_WASM))
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Constant descriptor size is not ported for this target.");
                }

                return ConstantDescriptorSizes.Constant;
            }
        }
    }

    protected sealed class instrDescDsp : instrDesc
    {
        public nint iddDspVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!(Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64))
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Displacement descriptor size is not ported for this target.");
                }

                return ConstantDescriptorSizes.Displacement;
            }
        }
    }

    // Native casts to instrDescCns read the common first payload slot. Inheritance
    // and ref aliases preserve that view without depending on CLR object layout.
    protected sealed class instrDescCnsDsp : instrDescCns
    {
        public ref nint iddcCnsVal => ref idcCnsVal;
        public int iddcDspVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!(Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64))
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Constant/displacement descriptor size is not ported for this target.");
                }

                return ConstantDescriptorSizes.ConstantDisplacement;
            }
        }
    }

    protected sealed class instrDescAmd : instrDesc
    {
        public nint idaAmdVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!Configuration.TARGET_XARCH)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Address-mode descriptor size is not ported for this target.");
                }

                return ConstantDescriptorSizes.AddressMode;
            }
        }
    }

    protected sealed class instrDescCnsAmd : instrDescCns
    {
        public ref nint idacCnsVal => ref idcCnsVal;
        public nint idacAmdVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!Configuration.TARGET_XARCH)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Constant/address-mode descriptor size is not ported for this target.");
                }

                return ConstantDescriptorSizes.ConstantAddressMode;
            }
        }
    }
}
