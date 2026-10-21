// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private uint _idCodeSize;
        private instruction _idIns;
        private bool _idSmallDsc;
        private bool _idLargeCns;
        private bool _idLargeDsp;
        private bool _idCall;
        private byte _idCustomBits;
        private uint _idScaledPrevOffset;
        private instrDescDebugInfo? _debugInfo;

        public abstract int NativeLogicalSize { get; }

        public instruction idIns()
        {
            return _idIns;
        }

        public void idIns(instruction ins)
        {
            assert(ins is not INS_invalid &&
                   (uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)InstructionOrdinalMaps.GetNativeInstructionCount());
            _idIns = ins;
        }

        public bool idInsIs(instruction ins)
        {
            return idIns() == ins;
        }

        public bool idInsIs(instruction ins, params ReadOnlySpan<instruction> rest)
        {
            if (idInsIs(ins))
            {
                return true;
            }

            foreach (var candidate in rest)
            {
                if (idInsIs(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        public bool idIsEmptyAlign()
        {
            if (!Configuration.TARGET_ARM64)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "ARM64 empty-alignment descriptors are not supported for this target.");
                return default!;
            }

            return (idIns() == INS_align) && (idInsOpt() == INS_OPTS_NONE);
        }

        public uint idCodeSize()
        {
            if (Configuration.TARGET_XARCH || Configuration.TARGET_LOONGARCH64 ||
                Configuration.TARGET_RISCV64)
            {
                return _idCodeSize;
            }

            if (Configuration.TARGET_ARM)
            {
                return GetArm32CodeSize();
            }

            if (Configuration.TARGET_ARM64)
            {
                return GetArm64CodeSize();
            }

            if (Configuration.TARGET_WASM)
            {
                return GetTargetCodeSize();
            }

            return GetTargetCodeSize();
        }

        public uint idPrevSize()
        {
            if (!Configuration.TARGET_XARCH)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "Previous-instruction sizes are only supported on xarch.");
                return default!;
            }

            return _idScaledPrevOffset * 4;
        }

        public void idSetPrevSize(uint previousSize)
        {
            if (!Configuration.TARGET_XARCH)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "Previous-instruction sizes are only supported on xarch.");
                return;
            }

            assert((previousSize % 4) == 0);
            if (Configuration.HOST_64BIT)
            {
                _idScaledPrevOffset = (previousSize / 4) & 0x1F;
            }
            else
            {
                _idScaledPrevOffset = (previousSize / 4) & 0xF;
            }

            assert(idPrevSize() == previousSize);
        }

        private uint GetArm32CodeSize()
        {
            return _idInsSize == insSize.ISZ_16BIT ? 2u : _idInsSize == insSize.ISZ_32BIT ? 4u : 6u;
        }

        private uint GetArm64CodeSize()
        {
            uint size = 4;
            switch (idInsFmt())
            {
                case Arm64FrameFormats.IF_LARGEADR:
                case Arm64FrameFormats.IF_LARGEJMP:
                {
                    // adrp + add, or a conditional branch followed by an unconditional branch.
                    size = 8;
                    break;
                }

                case Arm64FrameFormats.IF_LARGELDC:
                {
                    // Vectors need adrp + ldr + fmov (or adrp + add + ld1); scalars need adrp + ldr.
                    size = idReg1() is >= regNumber.REG_ARM64_V0 and <= regNumber.REG_ARM64_V31 ? 12u : 8u;
                    break;
                }

                case Arm64FrameFormats.IF_SN_0A:
                {
                    if ((idIns() == INS_align) && (idInsOpt() == INS_OPTS_NONE))
                    {
                        size = 0;
                    }
                    break;
                }
            }

            return size;
        }

        public bool idIsSmallDsc()
        {
            return _idSmallDsc;
        }

        public void idSetIsSmallDsc()
        {
            _idSmallDsc = true;
        }

        public bool idIsLargeCns()
        {
            return _idLargeCns && !_idCall;
        }

        public void idSetIsLargeCns()
        {
            _idLargeCns = true;
        }

        public bool idIsLargeDsp()
        {
            return _idLargeDsp;
        }

        public void idSetIsLargeDsp()
        {
            _idLargeDsp = true;
        }

        public bool idIsCall()
        {
            return _idCall;
        }

        public void idSetIsCall()
        {
            _idCall = true;
        }

        public bool idIsNoGC()
        {
            assert(!IsSimdInstruction(idIns()));
            return (_idCustomBits & 4) != 0;
        }

        public void idSetIsNoGC(bool value)
        {
            assert(!IsSimdInstruction(idIns()));
            _idCustomBits = (byte)((_idCustomBits & ~4) | (value ? 4 : 0));
        }

        public bool idIsLargeCall()
        {
            return _idCall && _idLargeCns;
        }

        public void idSetIsLargeCall()
        {
            _idCall = true;
            _idLargeCns = true;
        }

        public bool idIsLclVarDecl()
        {
            RequireWasmDescriptor();
            return idInsFmt() == insFormat.IF_WASM_LOCAL_DECL;
        }

        public bool idIsValTypeImm()
        {
            RequireWasmDescriptor();
            return idInsFmt() == insFormat.IF_WASM_TRY_TABLE;
        }

        public bool idIsV128Imm()
        {
            RequireWasmDescriptor();
            return idInsFmt() == insFormat.IF_WASM_V128;
        }

        public bool idIsMemargLaneImm()
        {
            RequireWasmDescriptor();
            return idInsFmt() == insFormat.IF_WASM_MEMARG_LANE;
        }

        private static void RequireWasmDescriptor()
        {
            if (!Configuration.TARGET_WASM)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "WebAssembly instruction descriptors are not supported for this target.");
                return;
            }
        }

        public instrDescDebugInfo? idDebugOnlyInfo()
        {
            return _debugInfo;
        }

        public void idDebugOnlyInfo(instrDescDebugInfo? info)
        {
            _debugInfo = info;
        }
    }
}
