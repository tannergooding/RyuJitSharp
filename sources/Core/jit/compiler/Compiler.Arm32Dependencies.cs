// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Compiler
{
    private GenTreeOp fgMorphLongMul(GenTreeOp mul)
    {
        NYI("TARGET_ARM fgMorphLongMul");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "TARGET_ARM long multiplication is not ported.");
    }

    private GenTreeOp fgRecognizeAndMorphLongMul(GenTreeOp mul)
    {
        NYI("TARGET_ARM fgRecognizeAndMorphLongMul");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "TARGET_ARM long multiplication is not ported.");
    }
}
#endif
