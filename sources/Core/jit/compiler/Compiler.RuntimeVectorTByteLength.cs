// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARM && !FEATURE_HW_INTRINSICS
    private uint getCompileTimeVectorTByteLength()
    {
        NYI("getCompileTimeVectorTByteLength on ARM32");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("getCompileTimeVectorTByteLength is unsupported on ARM32.");
    }
#endif

    public unsafe uint getRuntimeVectorTByteLength()
    {
        var compileTimeLength = getCompileTimeVectorTByteLength();

        if (compileTimeLength == SIZE_UNKNOWN)
        {
            assert(!IsAot);
            var vectorT = info.compCompHnd->getBuiltinClass(CLASSID_NUMERICS_VECTORT);
            assert(vectorT != null);
            var size = unchecked((uint)info.compCompHnd->getClassSize(vectorT));
            assert(size > 0);
            return size;
        }

        return compileTimeLength;
    }
}
