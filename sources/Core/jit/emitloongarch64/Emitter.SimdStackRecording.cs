// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidVectorIndexLoongArch64(emitAttr datasize, emitAttr elemsize, nint index)
    {
        EnsureLoongArch64Target();
        EnsureLoongArch64Simd();

        assert(isValidVectorDatasize(datasize));
        assert(isValidVectorElemsize(elemsize));

        if (index < 0)
        {
            return false;
        }

        if (datasize == EA_8BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 8,
                EA_2BYTE => index < 4,
                EA_4BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        if (datasize == EA_16BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 16,
                EA_2BYTE => index < 8,
                EA_4BYTE => index < 4,
                EA_8BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        if (datasize == EA_32BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 32,
                EA_2BYTE => index < 16,
                EA_4BYTE => index < 8,
                EA_8BYTE => index < 4,
                EA_16BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        NO_WAY("unexpected vector data size");
        return false;
    }

    private static bool InvalidVectorElementSize()
    {
        NO_WAY("unexpected vector element size");
        return false;
    }
}
