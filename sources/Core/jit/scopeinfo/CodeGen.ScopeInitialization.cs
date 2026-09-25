// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool siInFuncletRegion;
    private IL_OFFSET siLastEndOffs;

    private void siInit()
    {
        checkICodeDebugInfo();
        assert(_compiler.opts.compScopeInfo);

        if (_compiler.info.compVarScopesCount > 0)
        {
            siInFuncletRegion = false;
        }

        siLastEndOffs = 0;
        _compiler.compResetScopeLists();
    }
}
