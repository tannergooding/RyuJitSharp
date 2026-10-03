// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public string? siRegVarName(nuint offs, nuint size, uint reg)
    {
#if DEBUG
        if (!_compiler.opts.compScopeInfo)
        {
            return null;
        }

        if (_compiler.info.compVarScopesCount == 0)
        {
            return null;
        }

        noway_assert(_genTrnslLocalVarCount == 0 || genTrnslLocalVarInfo is not null);

        var rangeEnd = unchecked(offs + size);
        for (var i = 0; i < _genTrnslLocalVarCount; i++)
        {
            ref var localVarInfo = ref genTrnslLocalVarInfo[i];
            if (localVarInfo.tlviVarLoc.vlIsInReg(unchecked((regNumber)reg)) &&
                localVarInfo.tlviAvailable &&
                ((nuint)localVarInfo.tlviStartPC <= rangeEnd) &&
                (unchecked((nuint)localVarInfo.tlviStartPC + localVarInfo.tlviLength) > offs))
            {
                return localVarInfo.tlviName;
            }
        }
#endif
        return null;
    }

    public string? siStackVarName(nuint offs, nuint size, uint reg, uint stkOffs)
    {
#if DEBUG
        if (!_compiler.opts.compScopeInfo)
        {
            return null;
        }

        if (_compiler.info.compVarScopesCount == 0)
        {
            return null;
        }

        noway_assert(_genTrnslLocalVarCount == 0 || genTrnslLocalVarInfo is not null);

        var rangeEnd = unchecked(offs + size);
        for (var i = 0; i < _genTrnslLocalVarCount; i++)
        {
            ref var localVarInfo = ref genTrnslLocalVarInfo[i];
            if (localVarInfo.tlviVarLoc.vlIsOnStack(unchecked((regNumber)reg), unchecked((int)stkOffs)) &&
                localVarInfo.tlviAvailable &&
                ((nuint)localVarInfo.tlviStartPC <= rangeEnd) &&
                (unchecked((nuint)localVarInfo.tlviStartPC + localVarInfo.tlviLength) > offs))
            {
                return localVarInfo.tlviName;
            }
        }
#endif
        return null;
    }
}
#endif
