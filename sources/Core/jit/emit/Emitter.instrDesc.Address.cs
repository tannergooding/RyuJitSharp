// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private idAddrUnion _idAddrUnion;

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public partial struct idAddrUnion
        {
            [FieldOffset(0)]
            internal int iiaEncodedInstrCount;

            [FieldOffset(0)]
            private emitLclVarAddr _iiaLclVarAtZero;

            [FieldOffset(4)]
            private emitLclVarAddr _iiaLclVarAtFour;

            public readonly emitLclVarAddr iiaLclVar
            {
                get
                {
                    if (Configuration.TARGET_LOONGARCH64)
                    {
                        return _iiaLclVarAtFour;
                    }

                    return _iiaLclVarAtZero;
                }
            }

            public void iiaInitLclVarAddr(int varNum, uint offset)
            {
                if (Configuration.TARGET_LOONGARCH64)
                {
                    _iiaLclVarAtFour.initLclVarAddr(varNum, offset);
                }
                else
                {
                    _iiaLclVarAtZero.initLclVarAddr(varNum, offset);
                }
            }

            [FieldOffset(0)]
            private uint _iiaRegisterBitsAtZero;

            [FieldOffset(0)]
            public emitAddrMode iiaAddrMode;

            [FieldOffset(4)]
            private uint _iiaRegisterBitsAtFour;

            internal uint iiaRegisterBits
            {
                readonly get
                {
                    if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64)
                    {
                        return _iiaRegisterBitsAtFour;
                    }

                    return _iiaRegisterBitsAtZero;
                }
                set
                {
                    if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64)
                    {
                        _iiaRegisterBitsAtFour = value;
                    }
                    else
                    {
                        _iiaRegisterBitsAtZero = value;
                    }
                }
            }

            [FieldOffset(0)]
            internal insSvePattern iiaSvePattern;

            // Two six-bit register fields precede the aligned instruction code.
            [FieldOffset(4)]
            public uint iiaInstrEncode;

            [FieldOffset(0)]
            public bool iiaSecRel;

            public readonly bool iiaHasInstrCount()
            {
                return (iiaEncodedInstrCount & (int)iaut_MASK) == (int)iaut_INST_COUNT;
            }

            public readonly int iiaGetInstrCount()
            {
                assert(iiaHasInstrCount());
                return iiaEncodedInstrCount >> (int)iaut_SHIFT;
            }

            public void iiaSetInstrCount(int count)
            {
                assert(count is > -10 and < 10);
                iiaEncodedInstrCount = (count << (int)iaut_SHIFT) | (int)iaut_INST_COUNT;
            }
        }

        public ref idAddrUnion idAddr()
        {
            assert(!idIsSmallDsc());
            return ref _idAddrUnion;
        }
    }
}
