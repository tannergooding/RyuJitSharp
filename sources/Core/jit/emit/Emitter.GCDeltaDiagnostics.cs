// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_XARCH
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
#else
        const int basicIndent = 7;
        const int hexEncodingSize = 13;
#endif
        var compiler = _compiler ?? throw new FatalJitException("GC delta diagnostics require an active compiler.");
        jitprintf(new string(' ', basicIndent + (compiler.opts.disDiffable ? 0 : hexEncodingSize)));
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

            var title = descriptor.rpdGCtypeGet() switch
            {
                GCT_NONE => "npt",
                GCT_GCREF => "gcr",
                GCT_BYREF => "byr",
                _ => throw new FatalJitException("Invalid GC type in register pointer descriptor."),
            };
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

        debugPrevRegPtrDsc = last;
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
}
#endif
