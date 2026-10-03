// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private nint emitGetInsCIdisp(instrDesc id)
    {
        if (id.idIsLargeCall())
        {
            return ((instrDescCGCA)id).idcDisp;
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());
            return id.idAddr().iiaAddrMode.amDisp;
        }
    }
#endif
}
