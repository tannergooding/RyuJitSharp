// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Globalization;
#endif

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitPrintLabel(insGroup ig)
    {
        jitprintf(emitLabelString(ig));
    }

    public string emitLabelString(insGroup ig)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction group labels require an active compiler.");
        return $"G_M{unchecked((uint)compiler.compMethodID):D3}_IG{ig.GetDisplayId():D2}";
    }

#if DEBUG
    public void emitDispIGflags(InsGroupFlags flags)
    {
        if ((flags & InsGroupFlags.GCVars) != 0)
        {
            jitprintf(", gcvars");
        }
        if ((flags & InsGroupFlags.ByrefRegs) != 0)
        {
            jitprintf(", byref");
        }
        if ((flags & InsGroupFlags.Prolog) != 0)
        {
            jitprintf(", prolog");
        }
        if ((flags & InsGroupFlags.Epilog) != 0)
        {
            jitprintf(", epilog");
        }
        if ((flags & InsGroupFlags.FuncletProlog) != 0)
        {
            jitprintf(", funclet prolog");
        }
        if ((flags & InsGroupFlags.FuncletEpilog) != 0)
        {
            jitprintf(", funclet epilog");
        }
        if ((flags & InsGroupFlags.NoGCInterrupt) != 0)
        {
            jitprintf(", nogc");
        }
        if ((flags & InsGroupFlags.UpdatedInstructionSize) != 0)
        {
            jitprintf(", isz");
        }
        if ((flags & InsGroupFlags.Extend) != 0)
        {
            jitprintf(", extend");
        }
        if ((flags & InsGroupFlags.HasAlign) != 0)
        {
            jitprintf(", align");
        }
    }

    public unsafe void emitDispIG(insGroup ig, bool displayFunc = false, bool displayInstructions = false, bool displayLocation = true)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction-group diagnostics outside AMD64 are not implemented.");
#else
        var compiler = _compiler ?? throw new FatalJitException("Instruction-group diagnostics require an active compiler.");
        var label = emitLabelString(ig) + ":        ";
        jitprintf(label + "; ");

        var jitdump = compiler.verbose;
        if (jitdump && displayFunc)
        {
            jitprintf($"func={ig.igFuncIdx:D2}, ");
        }

        if ((ig.igFlags & InsGroupFlags.Placeholder) != 0)
        {
            var placeholder = ig.igPhData
                ?? throw new FatalJitException("A placeholder instruction group requires placeholder data.");
            var typeName = placeholder.igPhType switch
            {
                insGroupPlaceholderType.IGPT_PROLOG => "prolog",
                insGroupPlaceholderType.IGPT_EPILOG => "epilog",
                insGroupPlaceholderType.IGPT_FUNCLET_PROLOG => "funclet prolog",
                insGroupPlaceholderType.IGPT_FUNCLET_EPILOG => "funclet epilog",
                _ => "UNKNOWN",
            };
            jitprintf($"{typeName} placeholder, next placeholder=");
            if (placeholder.igPhNext is not null)
            {
                jitprintf($"IG{placeholder.igPhNext.GetDisplayId():D2} ");
            }
            else
            {
                jitprintf("<END>");
            }

            if (placeholder.igPhBB is not null)
            {
                jitprintf($", {placeholder.igPhBB.dspToString()}");
            }

            emitDispIGflags(ig.igFlags);
            if (displayLocation)
            {
                if (ReferenceEquals(ig, emitCurIG))
                {
                    jitprintf(" <-- Current IG");
                }
                if (ReferenceEquals(ig, emitPlaceholderList))
                {
                    jitprintf(" <-- First placeholder");
                }
                if (ReferenceEquals(ig, emitPlaceholderLast))
                {
                    jitprintf(" <-- Last placeholder");
                }
            }

            jitprintf("\n");
            jitprintf(new string(' ', label.Length) + ";   PrevGCVars=" +
                VarSetOps.ToString(compiler, placeholder.igPhPrevGCrefVars) + " ");
            dumpConvertedVarSet(compiler, placeholder.igPhPrevGCrefVars);
            jitprintf(", PrevGCrefRegs=");
            printRegMaskInt(placeholder.igPhPrevGCrefRegs);
            emitDispRegSet(placeholder.igPhPrevGCrefRegs);
            jitprintf(", PrevByrefRegs=");
            printRegMaskInt(placeholder.igPhPrevByrefRegs);
            emitDispRegSet(placeholder.igPhPrevByrefRegs);
            jitprintf("\n");

            jitprintf(new string(' ', label.Length) + ";   InitGCVars=" +
                VarSetOps.ToString(compiler, placeholder.igPhInitGCrefVars) + " ");
            dumpConvertedVarSet(compiler, placeholder.igPhInitGCrefVars);
            jitprintf(", InitGCrefRegs=");
            printRegMaskInt(placeholder.igPhInitGCrefRegs);
            emitDispRegSet(placeholder.igPhInitGCrefRegs);
            jitprintf(", InitByrefRegs=");
            printRegMaskInt(placeholder.igPhInitByrefRegs);
            emitDispRegSet(placeholder.igPhInitByrefRegs);
            jitprintf("\n");

            assert((ig.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs)) == 0);
        }
        else
        {
            var separator = "";
            if (jitdump)
            {
                jitprintf($"{separator}offs=0x{ig.igOffs:X6}, size=0x{ig.igSize:X4}");
                separator = ", ";
            }

            jitprintf($"{separator}bbWeight={refCntWtd2str(ig.igWeight)}");
            separator = ", ";

            if (compiler.compCodeGenDone)
            {
                jitprintf(separator + "PerfScore " + ig.igPerfScore.ToString("F2", CultureInfo.InvariantCulture));
                separator = ", ";
            }

            if ((ig.igFlags & InsGroupFlags.GCVars) != 0)
            {
                var vars = ig.igGCvars();
                jitprintf($"{separator}gcVars={VarSetOps.ToString(compiler, vars)} ");
                dumpConvertedVarSet(compiler, vars);
                separator = ", ";
            }

            if ((ig.igFlags & InsGroupFlags.Extend) == 0)
            {
                jitprintf($"{separator}gcrefRegs=");
                var registers = new regMaskTP(ig.igGCregs);
                printRegMaskInt(registers);
                emitDispRegSet(registers);
                separator = ", ";
            }

            if ((ig.igFlags & InsGroupFlags.ByrefRegs) != 0)
            {
                jitprintf($"{separator}byrefRegs=");
                var registers = new regMaskTP((regMask)ig.igByrefRegs());
                printRegMaskInt(registers);
                emitDispRegSet(registers);
                separator = ", ";
            }

#if FEATURE_LOOP_ALIGN
            if (ig.igLoopBackEdge is not null)
            {
                jitprintf($"{separator}loop=IG{ig.igLoopBackEdge.GetDisplayId():D2}");
                separator = ", ";
            }
#endif

            if (jitdump)
            {
                foreach (var block in ig.igBlocks)
                {
                    jitprintf(separator + block.dspToString());
                    separator = ", ";
                }
            }

            emitDispIGflags(ig.igFlags);
            if (displayLocation && ReferenceEquals(ig, emitCurIG))
            {
                jitprintf(" <-- Current IG");
            }

            jitprintf("\n");

            if (displayInstructions && (ig.igInsCnt != 0))
            {
                var instructions = ig.igData.AsSpan();
                assert(instructions.Length >= ig.igInsCnt);
                var offset = ig.igOffs;
                jitprintf("\n");
                for (var index = 0; index < ig.igInsCnt; index++)
                {
                    var instruction = instructions[index];
                    if (emitJmpInstHasNoCode(instruction))
                    {
                        assert(index == ig.igInsCnt - 1);
                        break;
                    }

                    emitDispIns(instruction, false, true, false, offset, null, 0, ig);
                    offset = unchecked(offset + instruction.idCodeSize());
                }
                jitprintf("\n");
            }
        }
#endif
    }

    public void emitDispIGlist(bool displayInstructions = false)
    {
#if !EMIT_BACKWARDS_NAVIGATION
        insGroup? previous = null;
#endif
        for (var ig = emitIGlist; ig is not null; ig = ig.igNext)
        {
#if EMIT_BACKWARDS_NAVIGATION
            var displayFunc = (ig.igPrev is null) || (ig.igPrev.igFuncIdx != ig.igFuncIdx);
#else
            var displayFunc = (previous is null) || (previous.igFuncIdx != ig.igFuncIdx);
            previous = ig;
#endif
            emitDispIG(ig, displayFunc, displayInstructions);
        }
    }

    public void emitDispJumpList()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Jump-list diagnostics require AMD64.");
#else
        jitprintf("Emitter Jump List:\n");
        uint jumpCount = 0;

        for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
        {
            var group = jump.idjIG ?? throw new FatalJitException("A saved jump must belong to an instruction group.");
            var debugInfo = jump.idDebugOnlyInfo()
                ?? throw new FatalJitException("Jump-list diagnostics require descriptor debug information.");
            var instructionName = jump.idIns() switch
            {
                INS_push_hide => "push",
                INS_lea or INS_push or INS_call or INS_jmp
                    or INS_jo or INS_jno or INS_jb or INS_jae or INS_je or INS_jne or INS_jbe or INS_ja
                    or INS_js or INS_jns or INS_jp or INS_jnp or INS_jl or INS_jge or INS_jle or INS_jg
                    => jump.idIns().ToString()[4..],
                _ => throw new FatalJitException(CORJIT_SKIPPED, "Jump-list instruction name is not implemented."),
            };

            jitprintf($"IG{group.GetDisplayId():D2} IN{debugInfo.idNum:x4} {instructionName,3}[{jump.idCodeSize()}]");

            if (!jump.idIsBound())
            {
                var target = jump.idjTarget is BasicBlock block ? emitCodeGetCookie(block) : null;
                jitprintf(target is null ? " -> ILLEGAL" : $" -> IG{target.GetDisplayId():D2}");

                if (jump.idjShort)
                {
                    jitprintf(" (short)");
                }
                if (jump.idjKeepLong)
                {
                    jitprintf(" (long)");
                }
                if (jump.idjIsRemovableJmpCandidate)
                {
                    jitprintf(" ; removal candidate");
                }
                if (jump.idjIsAfterCallBeforeEpilog)
                {
                    jitprintf(" ; after call before epilog");
                }
            }

            jitprintf("\n");
            jumpCount++;
        }

        jitprintf($"  total jump count: {jumpCount}\n");
#endif
    }
#endif
}
