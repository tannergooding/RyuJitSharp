// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitDispInsHex(instrDesc id, byte* code, nuint sz)
    {
        assert(_compiler is not null);
        if (!_compiler.opts.disCodeBytes)
        {
            return;
        }

        // Diffable disassembly omits the encoded bytes entirely.
        if (!_compiler.opts.disDiffable)
        {
            if (sz == 4)
            {
                jitprintf($"  {*(uint*)code:X8}    ");
            }
            else
            {
                jitprintf("              ");
            }
        }
    }

    private unsafe void emitDispLargeJmp(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset, byte* pCode, nuint sz, insGroup? ig)
    {
        // Do not modify the recorded descriptor: these two branches exist only for display.
        var pidJmp = new instrDescJmp();
        var ins = id.idIns();
        instruction reverseIns;
        insFormat reverseFmt;
        switch (ins)
        {
            case INS_cbz:
            {
                reverseIns = INS_cbnz;
                reverseFmt = IF_BI_1A;
                break;
            }
            case INS_cbnz:
            {
                reverseIns = INS_cbz;
                reverseFmt = IF_BI_1A;
                break;
            }
            case INS_tbz:
            {
                reverseIns = INS_tbnz;
                reverseFmt = IF_BI_1B;
                break;
            }
            case INS_tbnz:
            {
                reverseIns = INS_tbz;
                reverseFmt = IF_BI_1B;
                break;
            }
            default:
            {
                reverseIns = emitJumpKindToIns(emitReverseJumpKindArm64(emitInsToJumpKindArm64(ins)));
                reverseFmt = IF_BI_0B;
                break;
            }
        }

        pidJmp.idIns(reverseIns);
        pidJmp.idInsFmt(reverseFmt);
        pidJmp.idOpSize(id.idOpSize());
        pidJmp.idAddr().iiaSetInstrCount(1);
        pidJmp.idDebugOnlyInfo(id.idDebugOnlyInfo());

        nuint bcondSizeOrZero = pCode == null ? 0u : 4u;
        emitDispInsHelp(pidJmp, false, doffs, asmfm, offset, pCode, bcondSizeOrZero, null);
        pCode = unchecked(pCode + bcondSizeOrZero);
        offset = unchecked(offset + 4);

        // A fresh descriptor corresponds to clearing the native inline descriptor with memset.
        pidJmp = new instrDescJmp();
        pidJmp.idIns(INS_b);
        pidJmp.idInsFmt(IF_LARGEJMP);
        var recordedJump = (instrDescJmp)id;
        if (id.idIsBound())
        {
            pidJmp.idSetIsBound();
            pidJmp.idjTargetIG = recordedJump.idjTargetIG;
        }
        else
        {
            pidJmp.idjTarget = recordedJump.idjTarget;
        }
        pidJmp.idDebugOnlyInfo(id.idDebugOnlyInfo());

        nuint brSizeOrZero = pCode == null ? 0u : 4u;
        emitDispInsHelp(pidJmp, isNew, doffs, asmfm, offset, pCode, brSizeOrZero, ig);
    }

    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        if ((id.idInsFmt() == IF_LARGEJMP) && id.idIsBound())
        {
            // The large conditional branch expands to an inverted condition followed by b.
            emitDispLargeJmp(id, isNew, doffs, asmfm, offset, code, size, ig);
        }
        else
        {
            emitDispInsHelp(id, isNew, doffs, asmfm, offset, code, size, ig);
        }
    }
}
#endif
