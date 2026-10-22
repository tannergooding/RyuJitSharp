// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_I(instruction ins, emitAttr attr, nint val)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_IArm64(ins, attr, val);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            emitIns_IWasm(ins, attr, val);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            recordArm32InsI(ins, attr, val);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsILoongArch64(ins, attr, val);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            recordRiscVInsI(ins, attr, val);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ImmediateOnlyInstructionRecordingRequiresXarch
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        var valInByte = Configuration.TARGET_X86
            ? unchecked((sbyte)val) == unchecked((int)val)
            : unchecked((sbyte)val) == val;

        if (Configuration.TARGET_AMD64)
        {
            noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
        }

        if (EA_IS_CNS_RELOC(attr))
        {
            valInByte = false;
        }

        uint size;
        switch (ins)
        {
            case INS_loop:
            case INS_jge:
            {
                size = 2;
                break;
            }

            case INS_ret:
            {
                size = 3;
                break;
            }

            case INS_push_hide:
            case INS_push:
            {
                size = valInByte ? 2u : 5u;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UnexpectedImmediateOnlyInstruction);
                return;
            }
        }

        var id = emitNewInstrSC(attr, val);
        id.idIns(ins);
        id.idInsFmt(insFormat.IF_XARCH_CNS);
        size += emitGetAdjustedSize(id, insCodeMI(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
        if (Configuration.TARGET_X86)
        {
            emitAdjustStackDepthPushPop(ins);
        }
    }

    private void emitAdjustStackDepthPushPop(instruction ins)
    {
        if (!Configuration.TARGET_X86)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.X86StackDepthAdjustmentIsNotSupportedForThisTarget
    );
            return;
        }

        if (ins == INS_push)
        {
            emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) +
                unchecked((uint)emitCntStackDepth)));

            if (unchecked((uint)emitMaxStackDepth) < unchecked((uint)emitCurStackLvl))
            {
                JITDUMP($"Upping emitMaxStackDepth from {emitMaxStackDepth} to {emitCurStackLvl}\n");
                emitMaxStackDepth = emitCurStackLvl;
            }
        }
        else if (ins == INS_pop)
        {
            emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) -
                unchecked((uint)emitCntStackDepth)));
            assert(unchecked((int)emitCurStackLvl) >= 0);
        }
    }
}
