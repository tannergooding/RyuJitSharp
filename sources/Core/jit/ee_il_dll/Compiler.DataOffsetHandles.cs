// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public static unsafe CORINFO_FIELD_HANDLE eeFindJitDataOffs(uint dataOffs)
    {
        assert(dataOffs < 0x40000000);
        return (CORINFO_FIELD_HANDLE)(nuint)((dataOffs << (int)iaut_SHIFT) | (uint)iaut_DATA_OFFSET);
    }

    public static unsafe bool eeIsJitDataOffs(CORINFO_FIELD_HANDLE field)
    {
        var value = unchecked((uint)(nuint)field);
        if ((CORINFO_FIELD_HANDLE)(nuint)value != field)
        {
            return false;
        }

        return (value & (uint)iaut_MASK) == (uint)iaut_DATA_OFFSET;
    }

    public static unsafe int eeGetJitDataOffs(CORINFO_FIELD_HANDLE field)
    {
        if (eeIsJitDataOffs(field))
        {
            var dataOffs = unchecked((uint)(nuint)field);
            assert((CORINFO_FIELD_HANDLE)(nuint)dataOffs == field);
            assert(dataOffs < 0x40000000);
            return unchecked((int)(nint)field) >> (int)iaut_SHIFT;
        }

        return -1;
    }
}
