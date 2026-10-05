// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial struct RegSet
{
    public void rsSpillFPStack(GenTreeCall call)
    {
        var treeType = call.Type;

        var spill = _rsSpillFree;
        if (spill is not null)
        {
            _rsSpillFree = spill.spillNext;
        }
        else
        {
            spill = new SpillDsc();
        }

        var temp = tmpGetTemp(treeType);
        spill.spillTemp = temp;
        spill.spillTree = call;

        var reg = call.RegNum;
        spill.spillNext = _rsSpillDesc[(int)reg];
        _rsSpillDesc[(int)reg] = spill;

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("\n");
        }
#endif

        _codeGen.GetEmitter().emitIns_S(INS_fstp, treeType.EmitActualSize, temp.tdTempNum, 0);

        rsMarkSpill(call, reg);
    }
}
#endif
