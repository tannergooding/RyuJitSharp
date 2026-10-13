// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_FS_ST(instruction ins, emitAttr size, TempDsc temp, uint offset)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Xarch floating-point stack stores are not supported for this target.");
        }

        Emitter.emitIns_S(ins, size, temp.tdTempNum, unchecked((int)offset));
    }
}
