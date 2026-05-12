// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class simdHandlesCache
    {
        public unsafe CORINFO_CLASS_HANDLE PlaneHandle;
        public unsafe CORINFO_CLASS_HANDLE QuaternionHandle;
        public unsafe CORINFO_CLASS_HANDLE Vector2Handle;
        public unsafe CORINFO_CLASS_HANDLE Vector3Handle;
        public unsafe CORINFO_CLASS_HANDLE Vector4Handle;
        public unsafe CORINFO_CLASS_HANDLE VectorHandle;
    }
}
#endif
