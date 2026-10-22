// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal bool genWriteBarrierUsed
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.WriteBarrierDiagnosticStateIsOnlyAvailableInDebugBuilds);
                return false;
            }

            return Compiler.DebugData.Get(_compiler).CodeGenWriteBarrierUsed;
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
            Compiler.DebugData.Get(_compiler).CodeGenWriteBarrierUsed = true;
        }

        switch (writeBarrierForm)
        {
            case GCInfo.WriteBarrierForm.WBF_BarrierChecked:
            {
                return CORINFO_HELP_CHECKED_ASSIGN_REF;
            }

            case GCInfo.WriteBarrierForm.WBF_BarrierUnchecked:
            {
                return CORINFO_HELP_ASSIGN_REF;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedWriteBarrierForm,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? writeBarrierForm : default)
    );
                return default!;
            }
        }
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
