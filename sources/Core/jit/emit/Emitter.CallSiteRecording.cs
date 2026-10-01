// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitRecordCallSite(uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle)
    {
#if DEBUG
        assert(_compiler is not null);
        CORINFO_SIG_INFO sigInfo = default;
        if (callSig == null)
        {
            if ((methodHandle != null) && (Compiler.eeGetHelperNum(methodHandle) == CORINFO_HELP_UNDEF))
            {
                _compiler.eeGetMethodSig(methodHandle, out sigInfo);
                callSig = &sigInfo;
            }
        }

        emitCmpHandle->recordCallSite(unchecked((int)instrOffset), callSig, methodHandle);
#endif
    }
}
