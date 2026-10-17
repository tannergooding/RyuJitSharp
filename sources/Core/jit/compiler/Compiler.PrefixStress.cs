// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal bool DoJitStressEvexEncoding()
    {
        return Configuration.DEBUG &&
            ((JitConfig.JitStressEvexEncoding != 0) || (JitConfig.JitStressRex2Encoding != 0)) &&
            canUseEvexEncoding();
    }

    internal bool DoJitStressRex2Encoding()
    {
        return Configuration.DEBUG && (JitConfig.JitStressRex2Encoding != 0) && canUseApxEncoding();
    }

    internal bool DoJitStressPromotedEvexEncoding()
    {
        return Configuration.DEBUG && (JitConfig.JitStressPromotedEvexEncoding != 0) && canUseApxEncoding();
    }
}
