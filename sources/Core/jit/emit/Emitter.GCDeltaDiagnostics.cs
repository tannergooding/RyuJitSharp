// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitDispInsIndent()
    {
#if TARGET_AMD64
        const int basicIndent = 7;
        const int hexEncodingSize = 21;
#elif TARGET_X86
        const int basicIndent = 7;
        const int hexEncodingSize = 13;
#elif TARGET_ARM
        const int basicIndent = 12;
        const int hexEncodingSize = 11;
#elif TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
        const int basicIndent = 12;
        const int hexEncodingSize = 19;
#else
#error Unsupported instruction indentation target
#endif
        var compiler = _compiler ?? throw new FatalJitException("GC delta diagnostics require an active compiler.");
        // Native %.*s cannot print past its 29-character space literal.
        const string indentation = "                             ";
        var indent = compiler.opts.disDiffable ? basicIndent : basicIndent + hexEncodingSize;
        jitprintf(indentation[..System.Math.Min(indent, indentation.Length)]);
    }

    private void emitDispGCDeltaTitle(string title)
    {
        emitDispInsIndent();
        jitprintf($"; {title}");
    }

    private void emitDispGCRegDelta(string title, regMaskTP previous, regMaskTP current)
    {
        if (previous == current)
        {
            return;
        }

        emitDispGCDeltaTitle(title);
        var same = previous & current;
        var removed = previous ^ same;
        var added = current ^ same;
        if (removed.IsNonEmpty)
        {
            jitprintf(" -");
            dspRegMask(removed);
        }
        if (added.IsNonEmpty)
        {
            jitprintf(" +");
            dspRegMask(added);
        }
        jitprintf("\n");
    }

    private void emitDispGCVarDelta()
    {
        var compiler = _compiler ?? throw new FatalJitException("GC delta diagnostics require an active compiler.");
        if (VarSetOps.Equal(compiler, debugPrevGCrefVars, debugThisGCrefVars))
        {
            return;
        }

        emitDispGCDeltaTitle("GC ptr vars");
        _ = VarSetOps.Intersection(compiler, debugPrevGCrefVars, debugThisGCrefVars);
        var removed = VarSetOps.Diff(compiler, debugPrevGCrefVars, debugThisGCrefVars);
        var added = VarSetOps.Diff(compiler, debugThisGCrefVars, debugPrevGCrefVars);
        if (!VarSetOps.IsEmpty(compiler, removed))
        {
            jitprintf(" -");
            dumpConvertedVarSet(compiler, removed);
        }
        if (!VarSetOps.IsEmpty(compiler, added))
        {
            jitprintf(" +");
            dumpConvertedVarSet(compiler, added);
        }
        VarSetOps.Assign(compiler, ref debugPrevGCrefVars, debugThisGCrefVars);
        jitprintf("\n");
    }

    private void emitDispRegPtrListDelta()
    {
        var list = codeGen.GCInfo.DiagnosticRegPtrList;
        var last = codeGen.GCInfo.DiagnosticRegPtrLast;
        if (debugPrevRegPtrDsc == last)
        {
            return;
        }

        for (var descriptor = debugPrevRegPtrDsc is null ? list : debugPrevRegPtrDsc.rpdNext;
             descriptor is not null; descriptor = descriptor.rpdNext)
        {
            if (!descriptor.rpdArg)
            {
                continue;
            }

            string title;
            switch (descriptor.rpdGCtypeGet())
            {
                case GCT_NONE:
                {
                    title = "npt";
                    break;
                }

                case GCT_GCREF:
                {
                    title = "gcr";
                    break;
                }

                case GCT_BYREF:
                {
                    title = "byr";
                    break;
                }

                default:
                {
                    assert(false, "!\"Invalid GCtype\"");
                    title = "err";
                    break;
                }
            }
            emitDispGCDeltaTitle(title);
            var arg = descriptor.rpdCallData.rpdPtrArg;
            switch (descriptor.rpdArgTypeGet())
            {
                case rpdARG_PUSH:
                {
#if FEATURE_FIXED_OUT_ARGS
                    jitprintf(" arg write");
#else
                    jitprintf($" arg push {arg}");
#endif
                    break;
                }

                case rpdARG_POP:
                {
                    jitprintf($" arg pop {arg}");
                    break;
                }

                case rpdARG_KILL:
                {
                    jitprintf($" arg kill {arg}");
                    break;
                }

                default:
                {
                    jitprintf($" arg ??? {arg}");
                    break;
                }
            }
            jitprintf("\n");
        }

        debugPrevRegPtrDsc = codeGen.GCInfo.DiagnosticRegPtrLast;
    }

    private void emitDispGCInfoDelta()
    {
        emitDispGCRegDelta("gcrRegs", debugPrevGCrefRegs, new regMaskTP(emitThisGCrefRegs));
        emitDispGCRegDelta("byrRegs", debugPrevByrefRegs, new regMaskTP(emitThisByrefRegs));
        debugPrevGCrefRegs = new regMaskTP(emitThisGCrefRegs);
        debugPrevByrefRegs = new regMaskTP(emitThisByrefRegs);
        emitDispGCVarDelta();
        emitDispRegPtrListDelta();
    }

    public unsafe void emitDispGCinfo()
    {
        var compiler = _compiler ?? throw new FatalJitException("GC tracking diagnostics require an active compiler.");
        // Keep the diagnostic field addresses stable while formatting can allocate.
        fixed (regMask* previousGCref = &emitPrevGCrefRegs, previousByref = &emitPrevByrefRegs,
            initialGCref = &emitInitGCrefRegs, initialByref = &emitInitByrefRegs,
            currentGCref = &emitThisGCrefRegs, currentByref = &emitThisByrefRegs)
        {
            jitprintf("Emitter GC tracking info:");
            jitprintf("\n  emitPrevGCrefVars ");
            dumpConvertedVarSet(compiler, emitPrevGCrefVars);
            jitprintf($"\n  emitPrevGCrefRegs(0x{FMT_PTR((void*)dspPtr(previousGCref))})=");
            var registers = new regMaskTP(emitPrevGCrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf($"\n  emitPrevByrefRegs(0x{FMT_PTR((void*)dspPtr(previousByref))})=");
            registers = new regMaskTP(emitPrevByrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf("\n  emitInitGCrefVars ");
            dumpConvertedVarSet(compiler, emitInitGCrefVars);
            jitprintf($"\n  emitInitGCrefRegs(0x{FMT_PTR((void*)dspPtr(initialGCref))})=");
            registers = new regMaskTP(emitInitGCrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf($"\n  emitInitByrefRegs(0x{FMT_PTR((void*)dspPtr(initialByref))})=");
            registers = new regMaskTP(emitInitByrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf("\n  emitThisGCrefVars ");
            dumpConvertedVarSet(compiler, emitThisGCrefVars);
            jitprintf($"\n  emitThisGCrefRegs(0x{FMT_PTR((void*)dspPtr(currentGCref))})=");
            registers = new regMaskTP(emitThisGCrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf($"\n  emitThisByrefRegs(0x{FMT_PTR((void*)dspPtr(currentByref))})=");
            registers = new regMaskTP(emitThisByrefRegs);
            printRegMaskInt(registers);
            emitDispRegSet(registers);
            jitprintf("\n\n");
        }
    }
}
#endif
