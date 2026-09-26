// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    public readonly void rsSpillBeg()
    {
        rsSpillChk();
    }

    public readonly void rsSpillEnd()
    {
        rsSpillChk();
    }

    public readonly void rsSpillDone()
    {
        rsSpillChk();
    }

    public readonly void tmpEnd()
    {
#if DEBUG
        if (Compiler.verbose && (tmpCount > 0))
        {
            jitprintf($"{tmpCount} tmps used\n");
        }
#endif
    }

    public readonly void rsSpillChk()
    {
#if DEBUG
        assert(tmpGetCount == 0);

        for (var reg = REG_FIRST; reg < REG_COUNT; reg++)
        {
            assert(_rsSpillDesc[(int)reg] is null);
        }
#endif
    }

    public readonly void tmpDone()
    {
#if DEBUG
        assert(tmpGetAllFree());
        var count = 0;
        for (var temp = tmpListBeg(); temp is not null; temp = tmpListNxt(temp))
        {
            assert(temp.tdLegalOffset);
            count++;
        }
        assert(count == tmpCount);
        assert(tmpGetCount == 0);
#endif
    }
}
