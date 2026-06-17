// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    extension(regNumber regNum)
    {
#if HAS_FIXED_REGISTER_SET
        public bool IsFltReg => regNum is >= REG_FP_FIRST and <= REG_FP_LAST;

        public bool IsIntReg => regNum is >= REG_INT_FIRST and <= REG_INT_LAST;

#if FEATURE_MASKED_HW_INTRINSICS
        public bool IsMskReg => regNum is >= REG_MASK_FIRST and <= REG_MASK_LAST;
#else
        public bool IsMskReg => false;
#endif

        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(REG_COUNT));
                return s_names[(int)(regNum)];
            }
        }
#endif
    }
}
