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
        if (Configuration.DEBUG && Compiler.verbose && (tmpCount > 0))
        {
            jitprintf($"{tmpCount} tmps used\n");
        }
    }

    public readonly void rsSpillChk()
    {
        if (Configuration.DEBUG)
        {
            assert(tmpGetCount == 0);

            for (var reg = TargetRuntimeMetadata.FirstRegister; reg < REG_COUNT; reg++)
            {
                assert(_rsSpillDesc[(int)reg] is null);
            }
        }
    }

    public readonly void tmpDone()
    {
        if (Configuration.DEBUG)
        {
            assert(tmpGetAllFree());
            var count = 0;
            for (var temp = tmpListBeg(); temp is not null; temp = tmpListNxt(temp))
            {
                assert(temp.tdLegalOffset);
                count++;
            }
            assert(count == tmpCount);
            assert(tmpGetCount == 0);
        }
    }
}
