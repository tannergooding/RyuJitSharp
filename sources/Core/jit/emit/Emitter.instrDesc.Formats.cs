// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private insFormat _idInsFmt;

        public insFormat idInsFmt()
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return insFormat.IF_NONE;
            }

            if (Configuration.TARGET_RISCV64)
            {
                NYI_RISCV64("idInsFmt-----unimplemented on RISCV64 yet----");
                return (insFormat)0;
            }

            if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM ||
                Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
            {
                return _idInsFmt;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Instruction formats are not supported for this target.");
        }

        public void idInsFmt(insFormat insFmt)
        {
            if (Configuration.TARGET_ARM64)
            {
                assert(Arm64FrameFormats.Count <= 1024);
                noway_assert(insFmt != Arm64FrameFormats.IF_NONE);
                assert((uint)insFmt < Arm64FrameFormats.Count);
                _idInsFmt = (insFormat)((uint)insFmt & 0x3FF);
                return;
            }

            if (Configuration.TARGET_ARM)
            {
                assert((uint)emitFmtToOps.Length <= 1024);
                assert((uint)insFmt < (uint)emitFmtToOps.Length);
                _idInsFmt = (insFormat)((uint)insFmt & 0x3FF);
                return;
            }

            if (Configuration.TARGET_WASM)
            {
                assert((uint)emitFmtToOps.Length <= 1024);
                assert((uint)insFmt < (uint)emitFmtToOps.Length);
                _idInsFmt = (insFormat)((uint)insFmt & 0x3FF);
                return;
            }

            if (Configuration.TARGET_XARCH)
            {
                assert((uint)insFmt < 128);
                assert((uint)insFmt < (uint)emitFmtToOps.Length);
                _idInsFmt = (insFormat)((uint)insFmt & 0x7F);
                return;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return;
            }

            if (Configuration.TARGET_RISCV64)
            {
                NYI_RISCV64("idInsFmt-----unimplemented on RISCV64 yet----");
                return;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Instruction formats are not supported for this target.");
        }

        public bool idHasReg1()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R1_RD | IS_R1_RW | IS_R1_WR)) != 0;
        }

        public bool idIsReg1Read()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R1_RD | IS_R1_RW)) != 0;
        }

        public bool idIsReg1Write()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R1_RW | IS_R1_WR)) != 0;
        }

        public bool idHasReg2()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R2_RD | IS_R2_RW | IS_R2_WR)) != 0;
        }

        public bool idIsReg2Read()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R2_RD | IS_R2_RW)) != 0;
        }

        public bool idIsReg2Write()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R2_RW | IS_R2_WR)) != 0;
        }

        public bool idHasReg3()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R3_RD | IS_R3_RW | IS_R3_WR)) != 0;
        }

        public bool idIsReg3Read()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R3_RD | IS_R3_RW)) != 0;
        }

        public bool idIsReg3Write()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R3_RW | IS_R3_WR)) != 0;
        }

        public bool idHasReg4()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R4_RD | IS_R4_RW | IS_R4_WR)) != 0;
        }

        public bool idIsReg4Read()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R4_RD | IS_R4_RW)) != 0;
        }

        public bool idIsReg4Write()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_R4_RW | IS_R4_WR)) != 0;
        }

        public bool idHasMemGen()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_GM_RD | IS_GM_RW | IS_GM_WR)) != 0;
        }

        public bool idHasMemGenRead()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_GM_RD | IS_GM_RW)) != 0;
        }

        public bool idHasMemGenWrite()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_GM_RW | IS_GM_WR)) != 0;
        }

        public bool idHasMemStk()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_SF_RD | IS_SF_RW | IS_SF_WR)) != 0;
        }

        public bool idHasMemStkRead()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_SF_RD | IS_SF_RW)) != 0;
        }

        public bool idHasMemStkWrite()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_SF_RW | IS_SF_WR)) != 0;
        }

        public bool idHasMemAdr()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_AM_RD | IS_AM_RW | IS_AM_WR)) != 0;
        }

        public bool idHasMemAdrRead()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_AM_RD | IS_AM_RW)) != 0;
        }

        public bool idHasMemAdrWrite()
        {
            var isInfo = emitGetSchedInfo(idInsFmt());
            return (isInfo & (IS_AM_RW | IS_AM_WR)) != 0;
        }

        public bool idHasMem()
        {
            return idHasMemGen() || idHasMemStk() || idHasMemAdr();
        }

        public bool idHasMemRead()
        {
            return idHasMemGenRead() || idHasMemStkRead() || idHasMemAdrRead();
        }

        public bool idHasMemWrite()
        {
            return idHasMemGenWrite() || idHasMemStkWrite() || idHasMemAdrWrite();
        }

        public bool idHasMemAndCns()
        {
            assert((uint)idInsFmt() < (uint)emitFmtToOps.Length);
            var idOp = (ID_OPS)emitFmtToOps[(int)idInsFmt()];
            return idOp is ID_OP_CNS or ID_OP_DSP_CNS or ID_OP_AMD_CNS;
        }
    }
}
