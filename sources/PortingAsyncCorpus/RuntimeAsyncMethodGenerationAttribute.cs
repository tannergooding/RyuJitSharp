// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RuntimeAsyncMethodGenerationAttribute(bool runtimeAsync) : Attribute
{
    public bool RuntimeAsync { get; } = runtimeAsync;
}
