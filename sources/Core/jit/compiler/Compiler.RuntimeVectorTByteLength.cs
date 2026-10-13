// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private uint getCompileTimeVectorTByteLength()
    {
        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            return Configuration.TARGET_WASM ? (uint)Configuration.FP_REGSIZE_BYTES : (uint)GetVectorTByteLength();
        }

        if (Configuration.TARGET_ARM)
        {
            NYI("getCompileTimeVectorTByteLength on ARM32");
            fatal(CORJIT_IMPLLIMITATION);
            throw new FatalJitException("getCompileTimeVectorTByteLength is unsupported on ARM32.");
        }

        if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            return unchecked((uint)GetVectorTByteLength());
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Compile-time Vector<T> length is not supported on this target.");
    }

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
