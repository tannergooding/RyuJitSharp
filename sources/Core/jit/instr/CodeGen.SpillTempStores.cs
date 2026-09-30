// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_ST_RV(instruction ins, TempDsc temp, int offset, regNumber reg, var_types type)
    {
        Emitter.emitIns_S_R(ins, type.EmitActualSize, reg, temp.tdTempNum, offset);
    }
}
