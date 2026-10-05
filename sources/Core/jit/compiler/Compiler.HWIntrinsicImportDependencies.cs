// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && !TARGET_XARCH
namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe GenTree? impSpecialIntrinsic(
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_HANDLE clsHnd,
        CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType,
        var_types retType,
        byte simdSize,
        bool mustExpand)
    {
#if TARGET_ARM64
        return impSpecialIntrinsicArm64Core(intrinsic, clsHnd, method, in sig, in entryPoint,
            simdBaseType, retType, simdSize, mustExpand);
#elif TARGET_WASM
        return impSpecialIntrinsicWasmCore(intrinsic, clsHnd, method, in sig, in entryPoint,
            simdBaseType, retType, simdSize, mustExpand);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Hardware-intrinsic special import outside xarch is not ported.");
#endif
    }

    private GenTreeHWIntrinsic? impNonConstFallback(
        NamedIntrinsic intrinsic, var_types simdType, var_types simdBaseType)
    {
#if TARGET_ARM64
        return impNonConstFallbackArm64Core(intrinsic, simdType, simdBaseType);
#elif TARGET_WASM
        unreached();
        return null;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Hardware-intrinsic nonconstant fallback outside xarch is not ported.");
#endif
    }
}
#endif
