// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitPrintLabel(insGroup ig)
    {
        jitprintf(emitLabelString(ig));
    }

    public string emitLabelString(insGroup ig)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InstructionGroupLabelsMissingCompiler);
            return default!;
        }
        return $"G_M{unchecked((uint)compiler.compMethodID):D3}_IG{ig.GetDisplayId():D2}";
    }

    [Conditional("DEBUG")]
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

    [Conditional("DEBUG")]
    public unsafe void emitDispIG(insGroup ig, bool displayFunc = false, bool displayInstructions = false, bool displayLocation = true)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.InstructionGroupDiagnosticsRequireAnActiveCompiler);
            return;
        }
        var label = emitLabelString(ig) + ":        ";
        jitprintf(label + "; ");

        var jitdump = compiler.verbose;
        if (jitdump && displayFunc)
        {
            jitprintf($"func={ig.igFuncIdx:D2}, ");
        }

        if ((ig.igFlags & InsGroupFlags.Placeholder) != 0)
        {
            var placeholder = ig.igPhData;
            if (placeholder is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.PlaceholderInstructionGroupRequiresPlaceholderData);
                return;
            }
            var typeName = placeholder.igPhType switch {
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

            assert((ig.igFlags & InsGroupFlags.GCVars) == 0);
            assert((ig.igFlags & InsGroupFlags.ByrefRegs) == 0);
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

            if (Configuration.DEBUG && compiler.compCodeGenDone)
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

            if (Configuration.FEATURE_LOOP_ALIGN && (ig.igLoopBackEdge is not null))
            {
                jitprintf($"{separator}loop=IG{ig.igLoopBackEdge.GetDisplayId():D2}");
                separator = ", ";
            }

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

            if (displayInstructions)
            {
                var storage = ig.igData;
                var instruction = storage is not null ? emitFirstInstrDesc(storage) : null;
                var offset = ig.igOffs;
                uint count = ig.igInsCnt;

                if (count != 0)
                {
                    assert(storage is not null);
                    assert(storage.Length >= count);
                    jitprintf("\n");
                    do
                    {
                        assert(instruction is not null);
                        if (Configuration.TARGET_XARCH && emitJmpInstHasNoCode(instruction))
                        {
                            assert(count == 1);
                            break;
                        }

                        emitDispIns(instruction, false, true, false, offset, null, 0, ig);
                        offset = unchecked(offset + instruction.idCodeSize());
                        var descriptorSize = (nuint)emitSizeOfInsDsc(instruction);
                        emitAdvanceInstrDesc(ref instruction, descriptorSize);
                    }
                    while (--count != 0);

                    jitprintf("\n");
                }
            }
        }
    }

    [Conditional("DEBUG")]
    public void emitDispIGlist(bool displayInstructions = false)
    {
        insGroup? previous = null;
        var useBackwardNavigation = Configuration.EMIT_BACKWARDS_NAVIGATION || Configuration.TARGET_XARCH;
        for (var ig = emitIGlist; ig is not null; ig = ig.igNext)
        {
            var displayFunc = useBackwardNavigation
                ? (ig.igPrev is null) || (ig.igPrev.igFuncIdx != ig.igFuncIdx)
                : (previous is null) || (previous.igFuncIdx != ig.igFuncIdx);
            previous = ig;
            emitDispIG(ig, displayFunc, displayInstructions);
        }
    }

    [Conditional("DEBUG")]
    public void emitDispJumpList()
    {
        jitprintf("Emitter Jump List:\n");
        uint jumpCount = 0;

        for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
        {
            var group = jump.idjIG;
            if (group is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.SavedJumpMissingInstructionGroup);
                return;
            }
            var debugInfo = jump.idDebugOnlyInfo();
            if (debugInfo is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.JumpListDiagnosticsRequireDescriptorDebugInformation);
                return;
            }

            jitprintf(
                $"IG{group.GetDisplayId():D2} IN{debugInfo.idNum:x4} {codeGen.genInsDisplayName(jump),3}[{jump.idCodeSize()}]");

            if (!jump.idIsBound())
            {
                var isArm64LargeAddress = Configuration.TARGET_ARM64 &&
                    jump.idInsFmt() is (insFormat.IF_ARM64_LARGEADR or insFormat.IF_ARM64_LARGELDC);
                if (isArm64LargeAddress)
                {
                    jitprintf($" -> {jump.idReg1().Name}");
                }
                else
                {
                    var target = jump.idjTarget is BasicBlock block ? emitCodeGetCookie(block) : null;
                    jitprintf(target is null ? " -> ILLEGAL" : $" -> IG{target.GetDisplayId():D2}");
                }

                if (jump.idjShort)
                {
                    jitprintf(" (short)");
                }
                if (jump.idjKeepLong)
                {
                    jitprintf(" (long)");
                }
                if (Configuration.TARGET_XARCH && jump.idjIsRemovableJmpCandidate)
                {
                    jitprintf(" ; removal candidate");
                }

                if (Configuration.TARGET_AMD64 && jump.idjIsAfterCallBeforeEpilog)
                {
                    jitprintf(" ; after call before epilog");
                }
            }

            jitprintf("\n");
            jumpCount = unchecked(jumpCount + 1);
        }

        jitprintf($"  total jump count: {jumpCount}\n");
    }
}
