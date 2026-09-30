// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_FS_ST(instruction ins, emitAttr size, TempDsc temp, uint offset)
    {
        Emitter.emitIns_S(ins, size, temp.tdTempNum, unchecked((int)offset));
    }
}
#endif
