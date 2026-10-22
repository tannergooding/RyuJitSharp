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
        private const int REGNUM_BITS = 6;

        private insSize _idInsSize;
        private insFlags _idInsFlags;

        public bool idInstrIsT1()
        {
            Emitter.RequireArm32Target();
            return _idInsSize == insSize.ISZ_16BIT;
        }

        public insSize idInsSize()
        {
            Emitter.RequireArm32Target();
            return _idInsSize;
        }

        public void idInsSize(insSize size)
        {
            Emitter.RequireArm32Target();
            _idInsSize = (insSize)((uint)size & 3);
            assert(size == _idInsSize);
        }

        public insFlags idInsFlags()
        {
            Emitter.RequireArm32Target();
            return _idInsFlags;
        }

        public void idInsFlags(insFlags flags)
        {
            Emitter.RequireArm32Target();
            _idInsFlags = (insFlags)((uint)flags & 1);
            assert(flags == _idInsFlags);
        }

        public void idCodeSize(uint size)
        {
            if (Configuration.TARGET_XARCH)
            {
                assert(size <= 15);
                _idCodeSize = size & 0xF;
                assert(size == _idCodeSize);
                return;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                // A descriptor can represent several instructions, such as an immediate load.
                assert(size <= 16);
                _idCodeSize = size & 31;
                return;
            }

            if (Configuration.TARGET_RISCV64)
            {
                assert(size <= 32);
                _idCodeSize = size & 63;
                return;
            }

            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ExplicitInstructionSizesAreNotSupportedForThisTarget
    );
            return;
        }

        public partial struct idAddrUnion
        {
            [FieldOffset(0)]
            internal uint iiaEncodedInstr;

            [FieldOffset(0)]
            internal int iiaJmpOffset;

            public void iiaSetInstrEncode(uint encode)
            {
                if (Configuration.TARGET_LOONGARCH64)
                {
                    iiaEncodedInstr = encode;
                }
                else if (Configuration.TARGET_RISCV64)
                {
                    iiaInstrEncode = encode;
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.InstructionEncodingStorageIsNotSupportedForThisTarget
    );
                    return;
                }
            }

            public readonly uint iiaGetInstrEncode()
            {
                if (Configuration.TARGET_LOONGARCH64)
                {
                    return iiaEncodedInstr;
                }

                if (Configuration.TARGET_RISCV64)
                {
                    return iiaInstrEncode;
                }

                FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.InstructionEncodingStorageIsNotSupportedForThisTarget
    );
                return default!;
            }

            public void iiaSetJmpOffset(int offset)
            {
                if (!Configuration.TARGET_LOONGARCH64)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64JumpOffsetStorageIsNotSupportedForThisTarget
    );
                    return;
                }

                iiaJmpOffset = offset;
            }

            public readonly int iiaGetJmpOffset()
            {
                if (!Configuration.TARGET_LOONGARCH64)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64JumpOffsetStorageIsNotSupportedForThisTarget
    );
                    return default!;
                }

                return iiaJmpOffset;
            }
        }

        private uint GetTargetCodeSize()
        {
            if (Configuration.TARGET_ARM)
            {
                return _idInsSize == insSize.ISZ_16BIT ? 2u : _idInsSize == insSize.ISZ_32BIT ? 4u : 6u;
            }

            if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                return _idCodeSize;
            }

            if (Configuration.TARGET_WASM)
            {
                return Emitter.GetWasmCodeSize(this);
            }

            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.TargetSpecificInstructionSizeIsNotSupportedForThisTarget
    );
            return default!;
        }

        public bool idIsCallRegPtr()
        {
            assert(!IsSimdInstruction(idIns()));
            RequireCallRegPtrTarget();
            return (_idCustomBits & 8) != 0;
        }

        public void idSetIsCallRegPtr()
        {
            assert(!IsSimdInstruction(idIns()));
            RequireCallRegPtrTarget();
            _idCustomBits |= 8;
        }

        private static void RequireCallRegPtrTarget()
        {
            if (!(Configuration.TARGET_XARCH || Configuration.TARGET_LOONGARCH64 ||
                Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM))
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterPointerCallDescriptorsAreNotSupportedForThisTarget
    );
                return;
            }
        }
    }
}
