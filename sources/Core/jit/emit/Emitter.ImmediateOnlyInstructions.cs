// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if !TARGET_ARM64 && !TARGET_WASM
    public void emitIns_I(instruction ins, emitAttr attr, nint val)
    {
#if !TARGET_XARCH
#if TARGET_ARM
        recordArm32InsI(ins, attr, val);
#elif TARGET_LOONGARCH64
        emitInsILoongArch64(ins, attr, val);
#elif TARGET_RISCV64
        recordRiscVInsI(ins, attr, val);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Immediate-only instruction recording requires xarch.");
#endif
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
#if TARGET_X86
        var valInByte = unchecked((sbyte)val) == unchecked((int)val);
#else
        var valInByte = unchecked((sbyte)val) == val;
#endif
#if TARGET_AMD64
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
#endif

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
                throw new FatalJitException("Unexpected immediate-only instruction.");
            }
        }

        var id = emitNewInstrSC(attr, val);
        id.idIns(ins);
        id.idInsFmt(insFormat.IF_CNS);
        size += emitGetAdjustedSize(id, insCodeMI(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#if TARGET_X86
        emitAdjustStackDepthPushPop(ins);
#endif
#endif
    }
#endif

#if TARGET_X86
    private void emitAdjustStackDepthPushPop(instruction ins)
    {
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
#endif
}
