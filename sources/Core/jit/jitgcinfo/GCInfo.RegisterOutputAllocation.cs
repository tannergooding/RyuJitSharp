// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    internal regPtrDsc gcRegPtrAllocDsc()
    {
        assert(Compiler.IsFullPtrRegMapRequired);
        var descriptor = new regPtrDsc();
        if (gcRegPtrLast is null)
        {
            assert(gcRegPtrList is null);
            gcRegPtrList = gcRegPtrLast = descriptor;
        }
        else
        {
            assert(gcRegPtrList is not null);
            gcRegPtrLast.rpdNext = descriptor;
            gcRegPtrLast = descriptor;
        }
        return descriptor;
    }
}
