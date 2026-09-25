// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Compiler
{
    internal bool DoJitStressEvexEncoding()
    {
#if DEBUG
        return ((JitConfig.JitStressEvexEncoding != 0) || (JitConfig.JitStressRex2Encoding != 0))
            && canUseEvexEncoding();
#else
        return false;
#endif
    }

    internal bool DoJitStressRex2Encoding()
    {
#if DEBUG
        return (JitConfig.JitStressRex2Encoding != 0)
            && compOpportunisticallyDependsOn(InstructionSet_APX);
#else
        return false;
#endif
    }

    internal bool DoJitStressPromotedEvexEncoding()
    {
#if DEBUG
        return (JitConfig.JitStressPromotedEvexEncoding != 0)
            && compOpportunisticallyDependsOn(InstructionSet_APX);
#else
        return false;
#endif
    }
}
#endif
