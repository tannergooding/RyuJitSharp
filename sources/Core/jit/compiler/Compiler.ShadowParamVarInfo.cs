// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class ShadowParamVarInfo
    {
        public nint[]? AssignGroup;

        public int ShadowCopy = BAD_VAR_NUM;

        [Conditional("DEBUG")]
        public unsafe void Print()
        {
            var assignGroup = AssignGroup;
            jitprintf($"assignGroup [{FMT_PTR((void*)Unsafe.As<nint[]?, nint>(ref assignGroup))}]; shadowCopy: [{ShadowCopy}];\n");
        }

        public static bool MayNeedShadowCopy(in LclVarDsc varDsc)
        {
            if (Configuration.WINDOWS_AMD64_ABI)
            {
                return varDsc.lvIsParam;
            }

            return varDsc.lvIsParam && !varDsc.lvIsRegArg;
        }
    }
}
