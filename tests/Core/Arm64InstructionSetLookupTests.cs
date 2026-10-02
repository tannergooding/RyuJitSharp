// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class Arm64InstructionSetLookupTests
{
    [TestCase("Vector", CORINFO_InstructionSet.InstructionSet_VectorT)]
    [TestCase("Vector`1", CORINFO_InstructionSet.InstructionSet_VectorT)]
    [TestCase("Vector64", CORINFO_InstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector64`1", CORINFO_InstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector64WithSuffix", CORINFO_InstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector128", CORINFO_InstructionSet.InstructionSet_Vector128)]
    [TestCase("Vector128`1", CORINFO_InstructionSet.InstructionSet_Vector128)]
    [TestCase("Vector128WithSuffix", CORINFO_InstructionSet.InstructionSet_Vector128)]
    public static void VectorClassNamesFollowNativePrefixDispatch(string name, CORINFO_InstructionSet expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var className = Encoding.UTF8.GetBytes(name);

        Assert.That(LookupInstructionSet(compiler, className), Is.EqualTo(expected));
    }

    [TestCase("Vector6")]
    [TestCase("Vector65")]
    [TestCase("Vector12")]
    [TestCase("Vector127")]
    [TestCase("VectorX")]
    [TestCase("vector64")]
    [TestCase("VECTOR128")]
    [TestCase("Vector256")]
    [TestCase("NotVector64")]
    public static void UnrecognizedVectorClassNamesRemainIllegal(string name)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var className = Encoding.UTF8.GetBytes(name);

        Assert.That(
            LookupInstructionSet(compiler, className),
            Is.EqualTo(CORINFO_InstructionSet.InstructionSet_ILLEGAL));
    }

    [Test]
    public static void NonVectorInstructionSetLookupIsUnchanged()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.That(
            LookupInstructionSet(compiler, "AdvSimd"u8),
            Is.EqualTo(CORINFO_InstructionSet.InstructionSet_AdvSimd));
    }

    [TestCase("AdvSimd", CORINFO_InstructionSet.InstructionSet_AdvSimd_Arm64)]
    [TestCase("Aes", CORINFO_InstructionSet.InstructionSet_Aes_Arm64)]
    [TestCase("ArmBase", CORINFO_InstructionSet.InstructionSet_ArmBase_Arm64)]
    [TestCase("Crc32", CORINFO_InstructionSet.InstructionSet_Crc32_Arm64)]
    [TestCase("Dp", CORINFO_InstructionSet.InstructionSet_Dp_Arm64)]
    [TestCase("Rdm", CORINFO_InstructionSet.InstructionSet_Rdm_Arm64)]
    [TestCase("Sha1", CORINFO_InstructionSet.InstructionSet_Sha1_Arm64)]
    [TestCase("Sha256", CORINFO_InstructionSet.InstructionSet_Sha256_Arm64)]
    [TestCase("Sve", CORINFO_InstructionSet.InstructionSet_Sve_Arm64)]
    [TestCase("Sve2", CORINFO_InstructionSet.InstructionSet_Sve2_Arm64)]
    [TestCase("Sha3", CORINFO_InstructionSet.InstructionSet_Sha3_Arm64)]
    [TestCase("Sm4", CORINFO_InstructionSet.InstructionSet_Sm4_Arm64)]
    [TestCase("SveAes", CORINFO_InstructionSet.InstructionSet_SveAes_Arm64)]
    [TestCase("SveSha3", CORINFO_InstructionSet.InstructionSet_SveSha3_Arm64)]
    [TestCase("SveSm4", CORINFO_InstructionSet.InstructionSet_SveSm4_Arm64)]
    public static void NestedArm64ClassNamesMapEverySupportedInstructionSet(
        string enclosingClassName, CORINFO_InstructionSet expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.That(
            LookupIsa(compiler, "Arm64"u8, Encoding.UTF8.GetBytes(enclosingClassName), default),
            Is.EqualTo(expected));
    }

    [TestCase("Vector64")]
    [TestCase("Vector128")]
    public static void NestedArm64ClassNamesWithoutSpecificVariantsReturnNone(string enclosingClassName)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.That(
            LookupIsa(compiler, "Arm64"u8, Encoding.UTF8.GetBytes(enclosingClassName), default),
            Is.EqualTo(CORINFO_InstructionSet.InstructionSet_NONE));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupInstructionSet")]
    private static extern CORINFO_InstructionSet LookupInstructionSet(Compiler compiler, ReadOnlySpan<byte> className);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupIsa")]
    private static extern CORINFO_InstructionSet LookupIsa(Compiler compiler, ReadOnlySpan<byte> className,
        ReadOnlySpan<byte> innerEnclosingClassName, ReadOnlySpan<byte> outerEnclosingClassName);
}
#endif
