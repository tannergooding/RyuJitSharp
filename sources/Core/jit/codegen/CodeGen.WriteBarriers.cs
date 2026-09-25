// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if DEBUG
    private bool _genWriteBarrierUsed;

    internal bool genWriteBarrierUsed => _genWriteBarrierUsed;
#endif

    public bool genUseOptimizedWriteBarriers(GCInfo.WriteBarrierForm writeBarrierForm)
    {
#if TARGET_X86 && NOGC_WRITE_BARRIERS
        return true;
#else
        return false;
#endif
    }

    public CorInfoHelpFunc genWriteBarrierHelperForWriteBarrierForm(GCInfo.WriteBarrierForm writeBarrierForm)
    {
#if DEBUG
        _genWriteBarrierUsed = true;
#endif
        return writeBarrierForm switch
        {
            GCInfo.WriteBarrierForm.WBF_BarrierChecked => CORINFO_HELP_CHECKED_ASSIGN_REF,
            GCInfo.WriteBarrierForm.WBF_BarrierUnchecked => CORINFO_HELP_ASSIGN_REF,
            _ => throw new FatalJitException($"No write-barrier helper exists for {writeBarrierForm}."),
        };
    }

    public void genGCWriteBarrier(GCInfo.WriteBarrierForm writeBarrierForm)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Write-barrier generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var helper = genWriteBarrierHelperForWriteBarrierForm(writeBarrierForm);
        genEmitHelperCall(helper, 0, EA_PTRSIZE);
#endif
    }
}
