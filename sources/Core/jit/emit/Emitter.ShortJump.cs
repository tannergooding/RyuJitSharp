// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if !TARGET_WASM
    public void emitIns_ShortJ(instruction ins, BasicBlock dst)
    {
        const InsGroupFlags IGF_OUT_OF_ORDER_MASK = InsGroupFlags.Prolog | InsGroupFlags.Epilog
            | InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog;
        assert(emitCurIG is not null);
        assert((emitCurIG.igFlags & IGF_OUT_OF_ORDER_MASK) != 0);
        // Prolog unwind offsets are fixed while recording, so binding cannot resize these jumps.
        emitIns_J(ins, dst, keepShort: true);
    }
#endif
}
