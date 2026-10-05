// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src)
    {
        assert(!dst.IsContained);
        assert(!src.IsContained || src.IsContainedIntOrIImmed);

        var intConst = src.IsContainedIntOrIImmed ? src.AsIntConCommon() : null;
        if (intConst is not null)
        {
            emitIns_R_I(ins, attr, dst.RegNum, intConst.IconValue);
        }
        else
        {
            emitIns_R_R(ins, attr, dst.RegNum, src.RegNum);
        }

        return dst.RegNum;
    }
}
#endif
