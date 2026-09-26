// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class ShadowParamVarInfo
    {
        public nint[]? AssignGroup;

        public int ShadowCopy = BAD_VAR_NUM;

        public static bool MayNeedShadowCopy(in LclVarDsc varDsc)
        {
#if WINDOWS_AMD64_ABI
            return varDsc.lvIsParam;
#else
            return varDsc.lvIsParam && !varDsc.lvIsRegArg;
#endif
        }
    }
}
