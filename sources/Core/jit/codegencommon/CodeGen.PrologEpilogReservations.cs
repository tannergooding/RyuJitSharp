// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insGroupPlaceholderType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genReserveProlog(BasicBlock block)
    {
        JITDUMP($"Reserving prolog IG for block {FMT_BB(block.bbNum)}\n");
        Emitter.emitCreatePlaceholderIG(IGPT_PROLOG, block, VarSetOps.MakeEmpty(_compiler), default, default, last: false);
    }

    public void genReserveEpilog(BasicBlock block)
    {
        JITDUMP($"Reserving epilog IG for block {FMT_BB(block.bbNum)}\n");
        Emitter.emitCreatePlaceholderIG(IGPT_EPILOG, block, VarSetOps.MakeEmpty(_compiler),
            GCInfo.gcRegGCrefSetCur, GCInfo.gcRegByrefSetCur, block.IsLast);
    }

    public void genReserveFuncletProlog(BasicBlock block)
    {
        // Stack roots stay live while the non-interruptible prolog reestablishes the frame pointer.
        // Only the exception object may arrive in a register; the VM restores no other register locals.
        noway_assert((GCInfo.gcRegGCrefSetCur & new regMaskTP(SRBM_EXCEPTION_OBJECT)) == GCInfo.gcRegGCrefSetCur);
        noway_assert(GCInfo.gcRegByrefSetCur == default);

        JITDUMP($"Reserving funclet prolog IG for block {FMT_BB(block.bbNum)}\n");
        Emitter.emitCreatePlaceholderIG(IGPT_FUNCLET_PROLOG, block, GCInfo.gcVarPtrSetCur,
            GCInfo.gcRegGCrefSetCur, GCInfo.gcRegByrefSetCur, last: false);
    }

    public void genReserveFuncletEpilog(BasicBlock block)
    {
        JITDUMP($"Reserving funclet epilog IG for block {FMT_BB(block.bbNum)}\n");
        Emitter.emitCreatePlaceholderIG(IGPT_FUNCLET_EPILOG, block, GCInfo.gcVarPtrSetCur,
            GCInfo.gcRegGCrefSetCur, GCInfo.gcRegByrefSetCur, block.IsLast);
    }
}
