// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool siInFuncletRegion;
    private IL_OFFSET siLastEndOffs;

    private void siInit()
    {
        // siVarLoc uses the EE's enum and storage directly, so only its enclosing size can differ.
        assert(Unsafe.SizeOf<siVarLoc>() == Unsafe.SizeOf<ICorDebugInfo.VarLoc>());
        assert(_compiler.opts.compScopeInfo);

        if (_compiler.info.compVarScopesCount > 0)
        {
            siInFuncletRegion = false;
        }

        siLastEndOffs = 0;
        _compiler.compResetScopeLists();
    }
}
