// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG || SMGEN_COMPILE
using System;

namespace RyuJitSharp;

public static partial class SM_OPCODEExtensions
{
    extension(SM_OPCODE opcode)
    {
        public string Name
        {
            get
            {
                assert(opcode < SM_COUNT);
                return s_names[(int)(opcode)];
            }
        }
    }
}
#endif
