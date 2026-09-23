// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace RyuJitSharp;

public static partial class VNFuncExtensions
{
    internal static ValueNumStore.VNFOpAttrib GetAttributes(VNFunc func) => s_attribs[(int)func];
}
