// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool _genWriteBarrierUsed;

    internal bool genWriteBarrierUsed
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Write-barrier diagnostic state is only available in Debug builds.");
            }

            return _genWriteBarrierUsed;
        }
    }

    public bool genUseOptimizedWriteBarriers(GCInfo.WriteBarrierForm writeBarrierForm)
    {
        return Configuration.TARGET_X86 && Configuration.NOGC_WRITE_BARRIERS;
    }

    public CorInfoHelpFunc genWriteBarrierHelperForWriteBarrierForm(GCInfo.WriteBarrierForm writeBarrierForm)
    {
        if (Configuration.DEBUG)
        {
            _genWriteBarrierUsed = true;
        }

        return writeBarrierForm switch
        {
            GCInfo.WriteBarrierForm.WBF_BarrierChecked => CORINFO_HELP_CHECKED_ASSIGN_REF,
            GCInfo.WriteBarrierForm.WBF_BarrierUnchecked => CORINFO_HELP_ASSIGN_REF,
            _ => throw new FatalJitException($"No write-barrier helper exists for {writeBarrierForm}."),
        };
    }

    public void genGCWriteBarrier(GCInfo.WriteBarrierForm writeBarrierForm)
    {
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var helper = genWriteBarrierHelperForWriteBarrierForm(writeBarrierForm);
        genEmitHelperCall(helper, 0, EA_PTRSIZE);
    }
}
