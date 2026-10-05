// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitPacInProlog()
    {
        if (JitConfig.JitPacEnabled == 0)
        {
            return;
        }

        emitIns(TargetOS.IsWindows ? INS_pacibsp : INS_paciasp);
        assert(_compiler is not null);
        _compiler.unwindPacSignLR();
    }

    public void emitPacInEpilog()
    {
        if (JitConfig.JitPacEnabled == 0)
        {
            return;
        }

        emitIns(TargetOS.IsWindows ? INS_autibsp : INS_autiasp);
        assert(_compiler is not null);
        _compiler.unwindPacSignLR();
    }
}
#endif
