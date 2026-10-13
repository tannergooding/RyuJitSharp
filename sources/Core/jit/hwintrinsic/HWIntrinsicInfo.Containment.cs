// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static bool SupportsContainment(NamedIntrinsic id)
    {
        if (Configuration.TARGET_XARCH)
        {
            return (lookupFlags(id) & HW_Flag_NoContainment) == 0;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            return (lookupFlags(id) & HW_Flag_SupportsContainment) != 0;
        }

        throw new System.NotImplementedException("Hardware-intrinsic containment is not ported for this target.");
    }

    public static bool IsVectorCreateScalar(NamedIntrinsic id) => id is NI_Vector_CreateScalar;

    public static bool IsVectorCreateScalarUnsafe(NamedIntrinsic id) => id is NI_Vector_CreateScalarUnsafe;

    public static bool IsVectorCreate(NamedIntrinsic id) => id is NI_Vector_Create;

    public static bool IsVectorGetElement(NamedIntrinsic id) => id is NI_Vector_GetElement;

    public static bool IsVectorToScalar(NamedIntrinsic id) => id is NI_Vector_ToScalar;

    public static bool IsFmaIntrinsic(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_FmaIntrinsic) != 0;

    public static bool CopiesUpperBits(NamedIntrinsic id) =>
        Configuration.TARGET_XARCH && ((lookupFlags(id) & HW_Flag_CopyUpperBits) != 0);

    public static bool IsPermuteVar2x(NamedIntrinsic id) =>
        Configuration.TARGET_XARCH && ((lookupFlags(id) & HW_Flag_PermuteVar2x) != 0);
}
