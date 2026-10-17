// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class Arm64InstructionSetLookupTests
{
    [TestCase("Vector", ManagedInstructionSet.InstructionSet_VectorT)]
    [TestCase("Vector`1", ManagedInstructionSet.InstructionSet_VectorT)]
    [TestCase("Vector64", ManagedInstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector64`1", ManagedInstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector64WithSuffix", ManagedInstructionSet.InstructionSet_Vector64)]
    [TestCase("Vector128", ManagedInstructionSet.InstructionSet_Vector128)]
    [TestCase("Vector128`1", ManagedInstructionSet.InstructionSet_Vector128)]
    [TestCase("Vector128WithSuffix", ManagedInstructionSet.InstructionSet_Vector128)]
    public static void VectorClassNamesFollowNativePrefixDispatch(string name, ManagedInstructionSet expected)
    {
        var compiler = NewCompiler();
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
        var compiler = NewCompiler();
        var className = Encoding.UTF8.GetBytes(name);

        Assert.That(
            LookupInstructionSet(compiler, className),
            Is.EqualTo(ManagedInstructionSet.InstructionSet_ILLEGAL));
    }

    [Test]
    public static void NonVectorInstructionSetLookupIsUnchanged()
    {
        var compiler = NewCompiler();

        Assert.That(
            LookupInstructionSet(compiler, "AdvSimd"u8),
            Is.EqualTo(ManagedInstructionSet.InstructionSet_AdvSimd));
    }

    [TestCase("AdvSimd", ManagedInstructionSet.InstructionSet_AdvSimd_Arm64)]
    [TestCase("Aes", ManagedInstructionSet.InstructionSet_Aes_Arm64)]
    [TestCase("ArmBase", ManagedInstructionSet.InstructionSet_ArmBase_Arm64)]
    [TestCase("Crc32", ManagedInstructionSet.InstructionSet_Crc32_Arm64)]
    [TestCase("Dp", ManagedInstructionSet.InstructionSet_Dp_Arm64)]
    [TestCase("Rdm", ManagedInstructionSet.InstructionSet_Rdm_Arm64)]
    [TestCase("Sha1", ManagedInstructionSet.InstructionSet_Sha1_Arm64)]
    [TestCase("Sha256", ManagedInstructionSet.InstructionSet_Sha256_Arm64)]
    [TestCase("Sve", ManagedInstructionSet.InstructionSet_Sve_Arm64)]
    [TestCase("Sve2", ManagedInstructionSet.InstructionSet_Sve2_Arm64)]
    [TestCase("Sha3", ManagedInstructionSet.InstructionSet_Sha3_Arm64)]
    [TestCase("Sm4", ManagedInstructionSet.InstructionSet_Sm4_Arm64)]
    [TestCase("SveAes", ManagedInstructionSet.InstructionSet_SveAes_Arm64)]
    [TestCase("SveSha3", ManagedInstructionSet.InstructionSet_SveSha3_Arm64)]
    [TestCase("SveSm4", ManagedInstructionSet.InstructionSet_SveSm4_Arm64)]
    public static void NestedArm64ClassNamesMapEverySupportedInstructionSet(
        string enclosingClassName, ManagedInstructionSet expected)
    {
        var compiler = NewCompiler();

        Assert.That(
            LookupIsa(compiler, "Arm64"u8, Encoding.UTF8.GetBytes(enclosingClassName), default),
            Is.EqualTo(expected));
    }

    [TestCase("Vector64")]
    [TestCase("Vector128")]
    public static void NestedArm64ClassNamesWithoutSpecificVariantsReturnNone(string enclosingClassName)
    {
        var compiler = NewCompiler();

        Assert.That(
            LookupIsa(compiler, "Arm64"u8, Encoding.UTF8.GetBytes(enclosingClassName), default),
            Is.EqualTo(ManagedInstructionSet.InstructionSet_NONE));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupInstructionSet")]
    private static extern ManagedInstructionSet LookupInstructionSet(Compiler compiler, ReadOnlySpan<byte> className);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupIsa")]
    private static extern ManagedInstructionSet LookupIsa(Compiler compiler, ReadOnlySpan<byte> className,
        ReadOnlySpan<byte> innerEnclosingClassName, ReadOnlySpan<byte> outerEnclosingClassName);

    private static Compiler NewCompiler()
    {
        Assume.That(Configuration.TARGET_ARM64 && Configuration.FEATURE_HW_INTRINSICS);
        return (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
    }
}
