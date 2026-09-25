// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
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
