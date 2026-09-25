// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public BasicBlock genCreateTempLabel()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Temporary code-generation labels require AMD64.");
#else
#if DEBUG
        _compiler.fgSafeBasicBlockCreation = true;
#endif
        var block = BasicBlock.New(_compiler);
#if DEBUG
        _compiler.fgSafeBasicBlockCreation = false;
        if (_compiler.verbose)
        {
            jitprintf($"Mark {FMT_BB(block.bbNum)} as label: codegen temp block\n");
        }
#endif
        block.SetFlags(BBF_HAS_LABEL);
        assert(_compiler.compCurBB is not null);
        block.CopyFlags(_compiler.compCurBB, BBF_COLD);
#if DEBUG
        block.bbTgtStkDepth = unchecked((int)(genStackLevel / sizeof(int)));
#endif

        return block;
#endif
    }

    private void genLogLabel(BasicBlock block)
    {
#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf($"\n      L_M{unchecked((uint)_compiler.compMethodID):D3}_{FMT_BB(block.bbNum)}:\n");
        }
#endif
    }

    public void genDefineTempLabel(BasicBlock label)
    {
        genLogLabel(label);
        label.bbEmitCookie = Emitter.emitAddLabel(GCInfo.gcVarPtrSetCur, GCInfo.gcRegGCrefSetCur, GCInfo.gcRegByrefSetCur);
    }

#if !TARGET_WASM
    public void genDefineInlineTempLabel(BasicBlock label)
    {
        genLogLabel(label);
        label.bbEmitCookie = Emitter.emitAddInlineLabel();
    }
#endif
}
