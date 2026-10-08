// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static bool getInlinePInvokeCheckEnabled()
    {
#if DEBUG
        return JitConfig.JitPInvokeCheckEnabled is not 0;
#else
        return false;
#endif
    }

    public static genTreeOps LargeOpOpcode()
    {
#if DEBUG
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::s_gtNodeSizes native allocation sizes are not ported.");
#else
        return GT_CALL;
#endif
    }
}
